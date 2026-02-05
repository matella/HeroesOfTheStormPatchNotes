#!/bin/bash

# Script to generate self-signed certificates for Docker containers

echo "Generating self-signed certificates for Docker..."

# Create directories
mkdir -p ~/.aspnet/https
mkdir -p certs

# Generate API certificate (PFX format for ASP.NET Core)
echo "Generating API certificate..."
dotnet dev-certs https -ep ~/.aspnet/https/aspnetapp.pfx -p password --trust

# Generate Web certificate (PEM format for nginx)
echo "Generating Web certificate..."
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout certs/key.pem \
  -out certs/cert.pem \
  -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"

# Set permissions
chmod 644 certs/*.pem
chmod 644 ~/.aspnet/https/aspnetapp.pfx

echo "✅ Certificates generated successfully!"
echo ""
echo "Certificate locations:"
echo "  - API: ~/.aspnet/https/aspnetapp.pfx"
echo "  - Web: ./certs/cert.pem and ./certs/key.pem"
echo ""
echo "You can now run: docker-compose up -d"
