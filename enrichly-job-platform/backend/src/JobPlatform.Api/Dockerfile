# Build context is the `backend/` directory (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY .config/dotnet-tools.json .config/dotnet-tools.json
RUN dotnet tool restore

COPY src/JobPlatform.Api/JobPlatform.Api.csproj src/JobPlatform.Api/
RUN dotnet restore src/JobPlatform.Api/JobPlatform.Api.csproj

COPY src/JobPlatform.Api/ src/JobPlatform.Api/
RUN dotnet build src/JobPlatform.Api/JobPlatform.Api.csproj -c Release
# Generate the initial EF Core migration at build time if one hasn't already been committed.
# This keeps the sandbox this project was authored in (no local .NET SDK / no network access)
# from blocking on a chicken-and-egg problem: real migration files need the real `dotnet ef`
# tool to produce correctly, and that tool needs a .NET SDK, which the *build environment* has
# even though the authoring environment didn't. Once a migration exists on disk (e.g. after you
# run this build once, or if you generate it locally and commit it) it's reused as-is.
RUN if [ -z "$(find src/JobPlatform.Api/Data/Migrations -maxdepth 1 -name '*.cs' 2>/dev/null)" ]; then \
      echo "No migrations found on disk - generating InitialCreate with dotnet-ef..." && \
      cd src/JobPlatform.Api && \
      dotnet tool run dotnet-ef migrations add InitialCreate --output-dir Data/Migrations --startup-project . --project .; \
    else \
      echo "Using existing migrations from source control."; \
    fi

RUN dotnet publish src/JobPlatform.Api/JobPlatform.Api.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "JobPlatform.Api.dll"]
