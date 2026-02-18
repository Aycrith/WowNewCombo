<#
.SYNOPSIS
    Simple MEGA downloader using megatools or direct methods
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Url,
    
    [Parameter(Mandatory=$true)]
    [string]$OutputPath,
    
    [string]$Description = "File"
)

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MEGA Download Manager" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Downloading: $Description"
Write-Host "URL: $Url"
Write-Host "Output: $OutputPath"
Write-Host ""

# Create output directory if needed
$outputDir = Split-Path -Parent $OutputPath
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# Method 1: Try using megatools (lightweight alternative to MEGAcmd)
$megatoolsPath = "$env:LOCALAPPDATA\megatools\megadl.exe"

if (Test-Path $megatoolsPath) {
    Write-Host "Using megatools..." -ForegroundColor Green
    try {
        & $megatoolsPath --path $outputDir $Url
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[OK] Download completed!" -ForegroundColor Green
            return
        }
    }
    catch {
        Write-Host "[WARN] megatools failed: $_" -ForegroundColor Yellow
    }
}

# Method 2: Try MEGAcmd if available
$megacmdPath = "$env:LOCALAPPDATA\MEGAcmd"
if (Test-Path "$megacmdPath\MEGAcmdServer.exe") {
    Write-Host "Trying MEGAcmd..." -ForegroundColor Green
    
    # Stop any existing server
    Stop-Process -Name "MEGAcmdServer" -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    # Start fresh server
    Start-Process -FilePath "$megacmdPath\MEGAcmdServer.exe" -WindowStyle Hidden
    Start-Sleep -Seconds 5
    
    try {
        # Use mega-get command
        $megaExe = "$megacmdPath\mega-get.bat"
        
        Write-Host "Executing mega-get..." -ForegroundColor Cyan
        Write-Host "Command: mega-get `"$Url`" `"$OutputPath`"" -ForegroundColor Gray
        Write-Host ""
        
        # Run in cmd.exe for better compatibility
        $result = cmd /c "`"$megaExe`" `"$Url`" `"$OutputPath`" 2>&1"
        Write-Host $result
        
        if (Test-Path $OutputPath) {
            $size = (Get-Item $OutputPath).Length / 1MB
            if ($size -gt 1) {
                Write-Host "[OK] Download completed! Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
                return
            }
        }
    }
    catch {
        Write-Host "[WARN] MEGAcmd failed: $_" -ForegroundColor Yellow
    }
}

# Method 3: Install megatools (lightweight, reliable)
Write-Host ""
Write-Host "Installing megatools (recommended alternative)..." -ForegroundColor Yellow
Write-Host ""

try {
    # Download megatools
    $megatoolsUrl = "https://megatools.megous.com/builds/builds/megatools-1.11.1.20230212-win64.zip"
    $megatoolsZip = "$env:TEMP\megatools.zip"
    $megatoolsDir = "$env:LOCALAPPDATA\megatools"
    
    Write-Host "Downloading megatools..." -ForegroundColor Cyan
    Invoke-WebRequest -Uri $megatoolsUrl -OutFile $megatoolsZip -UseBasicParsing
    
    Write-Host "Extracting..." -ForegroundColor Cyan
    Expand-Archive -Path $megatoolsZip -DestinationPath $megatoolsDir -Force
    
    # Move files from subdirectory
    Get-ChildItem -Path "$megatoolsDir\megatools-*" -Recurse -File | ForEach-Object {
        Move-Item $_.FullName -Destination $megatoolsDir -Force
    }
    
    Write-Host ""
    Write-Host "[OK] megatools installed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Now downloading your file..." -ForegroundColor Cyan
    Write-Host ""
    
    # Download using megatools
    & "$megatoolsDir\megadl.exe" --path $outputDir $Url
    
    if (Test-Path $OutputPath) {
        $size = (Get-Item $OutputPath).Length / 1MB
        Write-Host ""
        Write-Host "[OK] Download completed! Size: $([math]::Round($size, 2)) MB" -ForegroundColor Green
        return
    }
}
catch {
    Write-Host "[ERROR] Failed to install megatools: $_" -ForegroundColor Red
}

# Method 4: Manual instructions
Write-Host ""
Write-Host "========================================" -ForegroundColor Red
Write-Host "  Automatic download failed" -ForegroundColor Red
Write-Host "========================================" -ForegroundColor Red
Write-Host ""
Write-Host "Please download manually:" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Open this URL in your browser:" -ForegroundColor White
Write-Host "   $Url" -ForegroundColor Cyan
Write-Host ""
Write-Host "2. Save the file to:" -ForegroundColor White
Write-Host "   $OutputPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "3. If you hit MEGA rate limit:" -ForegroundColor White
Write-Host "   - Wait 6 hours" -ForegroundColor Gray
Write-Host "   - Use a VPN to change IP" -ForegroundColor Gray
Write-Host "   - Or upgrade to MEGA Pro temporarily" -ForegroundColor Gray
Write-Host ""
