# ╔══════════════════════════════════════════════════════════════════╗
# ║        8 BIT LAB ENGINEERING — CODE SIGNING SCRIPT             ║
# ║  Creates a self-signed certificate and signs the .exe          ║
# ║  so Windows Firewall shows "8 Bit Lab Engineering" as          ║
# ║  publisher instead of "Unknown"                                ║
# ╚══════════════════════════════════════════════════════════════════╝

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  8 BIT LAB ENGINEERING - Code Signer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# STEP 1: Check if certificate already exists
$certName = "8 Bit Lab Engineering"
$existingCert = Get-ChildItem Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -like "*$certName*" }

if ($existingCert) {
    Write-Host "[OK] Certificate already exists: $($existingCert.Subject)" -ForegroundColor Green
    $cert = $existingCert | Select-Object -First 1
} else {
    # STEP 2: Create new self-signed code signing certificate (valid 5 years)
    Write-Host "[...] Creating code signing certificate for '$certName'..." -ForegroundColor Yellow
    $cert = New-SelfSignedCertificate `
        -Type CodeSigningCert `
        -Subject "CN=$certName, O=$certName, L=Germany, C=DE" `
        -CertStoreLocation Cert:\CurrentUser\My `
        -NotAfter (Get-Date).AddYears(5) `
        -KeyUsage DigitalSignature `
        -FriendlyName "$certName Code Signing"

    Write-Host "[OK] Certificate created! Thumbprint: $($cert.Thumbprint)" -ForegroundColor Green

    # STEP 3: Trust the certificate (add to Trusted Root so Windows doesn't warn)
    Write-Host "[...] Adding certificate to Trusted Root store..." -ForegroundColor Yellow
    $store = New-Object System.Security.Cryptography.X509Certificates.X509Store("Root", "CurrentUser")
    $store.Open("ReadWrite")
    $store.Add($cert)
    $store.Close()
    Write-Host "[OK] Certificate trusted on this machine!" -ForegroundColor Green
}

# STEP 4: Find the .exe to sign
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $scriptDir "bin\Debug\Work Time Counter.exe"

if (-not (Test-Path $exePath)) {
    $exePath = Join-Path $scriptDir "bin\Release\Work Time Counter.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Host "[!] Could not find Work Time Counter.exe in bin\Debug or bin\Release" -ForegroundColor Red
    Write-Host "    Please build the project first in Visual Studio, then run this script again." -ForegroundColor Red
    Write-Host ""
    Read-Host "Press Enter to exit"
    exit
}

Write-Host ""
Write-Host "[...] Signing: $exePath" -ForegroundColor Yellow

# STEP 5: Sign the .exe
Set-AuthenticodeSignature -FilePath $exePath -Certificate $cert -TimestampServer "http://timestamp.digicert.com"

# STEP 6: Verify
$sig = Get-AuthenticodeSignature -FilePath $exePath
if ($sig.Status -eq "Valid") {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Green
    Write-Host "  SUCCESS! App is now signed!" -ForegroundColor Green
    Write-Host "  Publisher: $certName" -ForegroundColor Green
    Write-Host "  Status: $($sig.Status)" -ForegroundColor Green
    Write-Host "========================================" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[!] Signing status: $($sig.Status)" -ForegroundColor Yellow
    Write-Host "    The certificate is self-signed, so Windows may show" -ForegroundColor Yellow
    Write-Host "    the publisher name but still flag it as not fully trusted" -ForegroundColor Yellow
    Write-Host "    on OTHER computers. On YOUR computer it will show correctly." -ForegroundColor Yellow
}

Write-Host ""
Read-Host "Press Enter to exit"
