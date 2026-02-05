# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["HotsPatchNotes.Api/HotsPatchNotes.Api.csproj", "HotsPatchNotes.Api/"]
COPY ["HotsPatchNotes.Shared/HotsPatchNotes.Shared.csproj", "HotsPatchNotes.Shared/"]

# Restore dependencies with caching
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "HotsPatchNotes.Api/HotsPatchNotes.Api.csproj"

# Copy all source files
COPY . .

# Build and publish the application
WORKDIR "/src/HotsPatchNotes.Api"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "HotsPatchNotes.Api.csproj" \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine AS final
WORKDIR /app

# Install curl for healthcheck
RUN apk add --no-cache curl

# Create non-root user (Alpine uses addgroup/adduser)
RUN addgroup -S appuser && adduser -S appuser -G appuser

# Create directory for database with proper permissions
RUN mkdir -p /app/data && chown -R appuser:appuser /app/data

# Copy published application with proper ownership
COPY --from=build --chown=appuser:appuser /app/publish .

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 5001

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:5001 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/hots.db" \
    DOTNET_EnableDiagnostics=0

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=40s --retries=3 \
    CMD curl -f http://localhost:5001/api/heroes/roles || exit 1

ENTRYPOINT ["dotnet", "HotsPatchNotes.Api.dll"]
