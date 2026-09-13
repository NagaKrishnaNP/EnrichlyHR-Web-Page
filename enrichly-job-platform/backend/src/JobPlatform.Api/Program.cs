using System.Text;
using JobPlatform.Api.Auth;
using JobPlatform.Api.Data;
using JobPlatform.Api.Models.Entities;
using JobPlatform.Api.Services;
using JobPlatform.Api.Services.Execution;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg
    .ReadFrom.Configuration(ctx.Configuration)
    .WriteTo.Console());

// --- Configuration ---
var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("Missing ConnectionStrings:Default configuration.");

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Missing Jwt configuration.");

// --- Database ---
// A fixed ServerVersion (rather than ServerVersion.AutoDetect) is used deliberately: AutoDetect
// opens a connection to the database immediately, which means `dotnet ef migrations add` and
// other design-time tooling would fail whenever the database isn't reachable (e.g. during a
// Docker image build). Bump this if you deploy against a different MySQL major version.
var serverVersion = new MySqlServerVersion(new Version(8, 0, 36));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseMySql(connectionString, serverVersion, mySqlOptions =>
        mySqlOptions.EnableRetryOnFailure(5, TimeSpan.FromSeconds(5), null)));

// --- Identity ---
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

// --- Auth ---
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    });

builder.Services.AddAuthorization();

// --- App services ---
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IJobExecutor, HttpJobExecutor>();
builder.Services.AddHttpClient("job-executor");

var workerConfig = builder.Configuration.GetSection("Worker");
if (workerConfig.GetValue("EnableScheduler", true))
    builder.Services.AddHostedService<JobSchedulerService>();
if (workerConfig.GetValue("EnableWorker", true))
    builder.Services.AddHostedService<JobWorkerService>();
if (workerConfig.GetValue("EnableReaper", true))
    builder.Services.AddHostedService<StaleExecutionReaperService>();

// --- Web ---
builder.Services.AddControllers().AddJsonOptions(o =>
    o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Enrichly Job Automation Platform API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header
    });
    c.AddSecurityRequirement(new()
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("Default", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Apply migrations on startup, with retries in case the DB container isn't ready yet ---
await MigrateWithRetryAsync(app);

app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("Default");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

app.Run();

static async Task MigrateWithRetryAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    const int maxAttempts = 10;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await db.Database.MigrateAsync();
            logger.LogInformation("Database migrations applied successfully.");
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning("Database not ready yet (attempt {Attempt}/{Max}): {Message}. Retrying in 3s...",
                attempt, maxAttempts, ex.Message);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Exposed for WebApplicationFactory-based integration tests.
public partial class Program { }
