# ── Stage 1: Build ──────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine AS build
WORKDIR /src

# Copy csproj and restore (layer caching)
COPY src/EduConnect.Api/EduConnect.Api.csproj ./src/EduConnect.Api/
RUN dotnet restore src/EduConnect.Api/EduConnect.Api.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish src/EduConnect.Api/EduConnect.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Stage 2: Runtime ───────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS runtime
WORKDIR /app

# Security: create non-root user (but don't switch yet)
RUN addgroup -S appgroup && adduser -S appuser -G appgroup

COPY --from=build /app/publish .

# Create logs directory and hand /app to appuser, then drop privileges
RUN mkdir -p /app/logs && chown -R appuser:appgroup /app
USER appuser

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget --no-verbose --tries=1 --spider http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "EduConnect.Api.dll"]
