# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["HotsPatchNotes.Api/HotsPatchNotes.Api.csproj", "HotsPatchNotes.Api/"]
COPY ["HotsPatchNotes.Shared/HotsPatchNotes.Shared.csproj", "HotsPatchNotes.Shared/"]

# Restore dependencies
RUN dotnet restore "HotsPatchNotes.Api/HotsPatchNotes.Api.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/HotsPatchNotes.Api"
RUN dotnet build "HotsPatchNotes.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "HotsPatchNotes.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser

# Create directory for database with proper permissions
RUN mkdir -p /app/data && chown -R appuser:appuser /app/data

# Copy published application
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appuser /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 5001
EXPOSE 7001

# Environment variables
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:5001

# Set database path to volume
ENV ConnectionStrings__DefaultConnection="Data Source=/app/data/hots.db"

ENTRYPOINT ["dotnet", "HotsPatchNotes.Api.dll"]
