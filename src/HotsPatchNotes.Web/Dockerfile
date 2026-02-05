# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["HotsPatchNotes.Web/HotsPatchNotes.Web.csproj", "HotsPatchNotes.Web/"]
COPY ["HotsPatchNotes.Shared/HotsPatchNotes.Shared.csproj", "HotsPatchNotes.Shared/"]

# Restore dependencies with caching
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "HotsPatchNotes.Web/HotsPatchNotes.Web.csproj"

# Copy all source files
COPY . .

# Publish the application
WORKDIR "/src/HotsPatchNotes.Web"
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "HotsPatchNotes.Web.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false

# Runtime stage with nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Install wget for healthcheck
RUN apk add --no-cache wget

# Copy published Blazor WASM files
COPY --from=build /app/publish/wwwroot .

# Copy nginx configuration
COPY --from=build /src/HotsPatchNotes.Web/nginx.conf /etc/nginx/nginx.conf

# Run as non-root
RUN chown -R nginx:nginx /usr/share/nginx/html && \
    chown -R nginx:nginx /var/cache/nginx && \
    chown -R nginx:nginx /var/log/nginx && \
    touch /var/run/nginx.pid && \
    chown -R nginx:nginx /var/run/nginx.pid

USER nginx

EXPOSE 80

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:80 || exit 1

CMD ["nginx", "-g", "daemon off;"]
