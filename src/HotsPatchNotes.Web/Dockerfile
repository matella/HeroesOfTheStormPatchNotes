# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files
COPY ["HotsPatchNotes.Web/HotsPatchNotes.Web.csproj", "HotsPatchNotes.Web/"]
COPY ["HotsPatchNotes.Shared/HotsPatchNotes.Shared.csproj", "HotsPatchNotes.Shared/"]

# Restore dependencies
RUN dotnet restore "HotsPatchNotes.Web/HotsPatchNotes.Web.csproj"

# Copy all source files
COPY . .

# Build and publish the application
WORKDIR "/src/HotsPatchNotes.Web"
RUN dotnet publish "HotsPatchNotes.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage with nginx
FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html

# Copy published Blazor WASM files
COPY --from=build /app/publish/wwwroot .

# Copy nginx configuration
COPY --from=build /src/HotsPatchNotes.Web/nginx.conf /etc/nginx/nginx.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
