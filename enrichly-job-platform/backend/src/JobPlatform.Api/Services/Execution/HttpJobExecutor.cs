using System.Text;
using System.Text.Json;
using JobPlatform.Api.Models.Entities;

namespace JobPlatform.Api.Services.Execution;

/// <summary>
/// Executes a job's HTTP action. Every job in this build is "make an HTTP request", which
/// covers the assignment's example scenarios (call an API, trigger a webhook, sync data via
/// an API call). Output is truncated so a chatty endpoint can't blow up the database row.
/// </summary>
public class HttpJobExecutor : IJobExecutor
{
    private const int MaxOutputLength = 8000;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HttpJobExecutor> _logger;

    public HttpJobExecutor(IHttpClientFactory httpClientFactory, ILogger<HttpJobExecutor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("job-executor");

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(job.TimeoutSeconds));

        try
        {
            using var request = new HttpRequestMessage(new HttpMethod(job.HttpMethod), job.TargetUrl);

            if (!string.IsNullOrWhiteSpace(job.HeadersJson))
            {
                var headers = JsonSerializer.Deserialize<Dictionary<string, string>>(job.HeadersJson);
                if (headers is not null)
                {
                    foreach (var (key, value) in headers)
                    {
                        request.Headers.TryAddWithoutValidation(key, value);
                    }
                }
            }

            if (!string.IsNullOrEmpty(job.Body) &&
                !job.HttpMethod.Equals("GET", StringComparison.OrdinalIgnoreCase) &&
                !job.HttpMethod.Equals("HEAD", StringComparison.OrdinalIgnoreCase))
            {
                request.Content = new StringContent(job.Body, Encoding.UTF8, "application/json");
            }

            var response = await client.SendAsync(request, timeoutCts.Token);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var truncated = Truncate(body);

            if (response.IsSuccessStatusCode)
            {
                return new ExecutionResult(true, (int)response.StatusCode, truncated, null);
            }

            return new ExecutionResult(
                false,
                (int)response.StatusCode,
                truncated,
                $"Request completed but returned a non-success status code: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return new ExecutionResult(false, null, null, $"Request timed out after {job.TimeoutSeconds}s.");
        }
        catch (HttpRequestException ex)
        {
            // Covers "external API unavailable" / DNS failures / connection refused / dropped connections.
            return new ExecutionResult(false, null, null, $"Network error calling target URL: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error executing job {JobId}", job.Id);
            return new ExecutionResult(false, null, null, $"Unexpected error: {ex.Message}");
        }
    }

    private static string Truncate(string value) =>
        value.Length <= MaxOutputLength ? value : value[..MaxOutputLength] + "... [truncated]";
}
