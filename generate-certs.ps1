# Script to generate self-signed certificates for Docker containers

Write-Host "Generating self-signed certificates for Docker..." -ForegroundColor Green

# Create directories
$httpsDir = Join-Path $env:USERPROFILE ".aspnet\https"
$certsDir = "certs"

New-Item -ItemType Directory -Force -Path $httpsDir | Out-Null
New-Item -ItemType Directory -Force -Path $certsDir | Out-Null

# Generate API certificate (PFX format for ASP.NET Core)
Write-Host "Generating API certificate..." -ForegroundColor Yellow
dotnet dev-certs https -ep "$httpsDir\aspnetapp.pfx" -p password --trust

# Generate Web certificate (PEM format for nginx)
Write-Host "Generating Web certificate..." -ForegroundColor Yellow

# Check if OpenSSL is available
$opensslPath = Get-Command openssl -ErrorAction SilentlyContinue

if ($opensslPath) {
    openssl req -x509 -nodes -days 365 -newkey rsa:2048 `
        -keyout "$certsDir\key.pem" `
        -out "$certsDir\cert.pem" `
        -subj "/C=US/ST=State/L=City/O=Organization/CN=localhost"
} else {
    Write-Host "OpenSSL not found. Generating certificate using PowerShell..." -ForegroundColor Yellow
    
    # Create self-signed certificate using PowerShell
    $cert = New-SelfSignedCertificate -DnsName "localhost" `
        -CertStoreLocation "cert:\CurrentUser\My" `
        -NotAfter (Get-Date).AddYears(1) `
        -KeyAlgorithm RSA `
        -KeyLength 2048
    
    # Export to PEM format
    $certPath = "$certsDir\cert.pem"
    $keyPath = "$certsDir\key.pem"
    
    # Export certificate
    $certBytes = $cert.Export([System.Security.Cryptography.X509Certificates.X509ContentType]::Cert)
    $certPem = "-----BEGIN CERTIFICATE-----`n" + [Convert]::ToBase64String($certBytes, [System.Base64FormattingOptions]::InsertLineBreaks) + "`n-----END CERTIFICATE-----"
    Set-Content -Path $certPath -Value $certPem
    
    Write-Host "Note: Private key export requires manual steps or OpenSSL" -ForegroundColor Yellow
    Write-Host "For production, install OpenSSL or use proper certificates" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "✅ Certificates generated successfully!" -ForegroundColor Green
Write-Host ""
Write-Host "Certificate locations:" -ForegroundColor Cyan
Write-Host "  - API: $httpsDir\aspnetapp.pfx" -ForegroundColor White
Write-Host "  - Web: .\certs\cert.pem and .\certs\key.pem" -ForegroundColor White
Write-Host ""
Write-Host "You can now run: docker-compose up -d" -ForegroundColor Green
