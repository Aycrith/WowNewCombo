<#
.SYNOPSIS
    Advanced MEGA Download Solution with Multiple Bypass Methods
    
.DESCRIPTION
    This script provides multiple methods to download files from MEGA when rate-limited:
    1. ProtonVPN CLI for IP rotation (free tier available)
    2. Cloudflare WARP for IP masking
    3. aria2c multi-connection download
    4. Session rotation
    
.PARAMETER FileToDownload
    Which file to download: Common2MPQ, MMAPs, or Both

.EXAMPLE
    .\Download-MegaAdvanced.ps1 -FileToDownload Common2MPQ
#>

param(
    [ValidateSet("Common2MPQ", "MMAPs", "Both")]
    [string]$FileToDownload = "Both"
)

$ErrorActionPreference = "Stop"

# File definitions
$Files = @{
    Common2MPQ = @{
        Name = "common-2.MPQ"
        URL = "https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8"
        Size = "1.7 GB"
        Destination = "C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ"
        Description = "Vanilla MPQ for V1 pathfinding"
    }
    MMAPs = @{
        Name = "mmaps_v15_0_1_530_571_50_angle.7z"
        URL = "https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk"
        Size = "~2 GB"
        Destination = "C:\WowClassicGrindBot\Navigation\mmaps.7z"
        Description = "MMAP archive for V3 pathfinding"
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  Advanced MEGA Download Solution" -ForegroundColor Cyan
Write-Host "  Multiple Rate Limit Bypass Methods" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# Check what we need to download
$downloads = @()
if ($FileToDownload -eq "Common2MPQ" -or $FileToDownload -eq "Both") {
    if (-not (Test-Path $Files.Common2MPQ.Destination) -or (Get-Item $Files.Common2MPQ.Destination -ErrorAction SilentlyContinue).Length -lt 1GB) {
        $downloads += $Files.Common2MPQ
    } else {
        Write-Host "[OK] $($Files.Common2MPQ.Name) already exists" -ForegroundColor Green
    }
}
if ($FileToDownload -eq "MMAPs" -or $FileToDownload -eq "Both") {
    if (-not (Test-Path $Files.MMAPs.Destination) -or (Get-Item $Files.MMAPs.Destination -ErrorAction SilentlyContinue).Length -lt 100MB) {
        $downloads += $Files.MMAPs
    } else {
        Write-Host "[OK] $($Files.MMAPs.Name) already exists" -ForegroundColor Green
    }
}

if ($downloads.Count -eq 0) {
    Write-Host ""
    Write-Host "All files already downloaded!" -ForegroundColor Green
    exit 0
}

Write-Host "Files to download:" -ForegroundColor Yellow
foreach ($file in $downloads) {
    Write-Host "  - $($file.Name) ($($file.Size))" -ForegroundColor White
}
Write-Host ""

# Method selection
Write-Host "Available bypass methods:" -ForegroundColor Yellow
Write-Host "  1. Cloudflare WARP (Easy - Recommended)" -ForegroundColor Green
Write-Host "     Free VPN/proxy that changes your IP instantly"
Write-Host ""
Write-Host "  2. ProtonVPN CLI (More complex)" -ForegroundColor Yellow
Write-Host "     Free VPN with multiple server locations"
Write-Host ""
Write-Host "  3. Manual VPN Method" -ForegroundColor Yellow
Write-Host "     Use any VPN you already have installed"
Write-Host ""
Write-Host "  4. aria2c Multi-Connection Download" -ForegroundColor Yellow
Write-Host "     Fast downloader that may partially bypass limits"
Write-Host ""
Write-Host "  5. Wait for MEGA Reset (6 hours)" -ForegroundColor Gray
Write-Host "     Do nothing - just wait for rate limit to expire"
Write-Host ""

$choice = Read-Host "Select method (1-5)"

switch ($choice) {
    "1" {
        # Cloudflare WARP method
        Write-Host ""
        Write-Host "=== Cloudflare WARP Method ===" -ForegroundColor Cyan
        
        $warpPath = "$env:ProgramFiles\Cloudflare\Cloudflare WARP\warp-cli.exe"
        
        if (-not (Test-Path $warpPath)) {
            Write-Host "Installing Cloudflare WARP..." -ForegroundColor Yellow
            
            $warpInstaller = "$env:TEMP\Cloudflare_WARP_Release-x64.msi"
            $warpUrl = "https://1111-releases.cloudflareclient.com/windows/Cloudflare_WARP_Release-x64.msi"
            
            Write-Host "Downloading WARP installer..." -ForegroundColor Cyan
            Invoke-WebRequest -Uri $warpUrl -OutFile $warpInstaller -UseBasicParsing
            
            Write-Host "Installing WARP (this may take a minute)..." -ForegroundColor Cyan
            Start-Process msiexec.exe -ArgumentList "/i", $warpInstaller, "/quiet", "/norestart" -Wait
            
            # Wait for installation
            Start-Sleep -Seconds 10
            
            if (Test-Path $warpPath) {
                Write-Host "[OK] Cloudflare WARP installed!" -ForegroundColor Green
            } else {
                throw "WARP installation failed"
            }
        } else {
            Write-Host "[OK] Cloudflare WARP already installed" -ForegroundColor Green
        }
        
        Write-Host ""
        Write-Host "Enabling WARP to change your IP..." -ForegroundColor Cyan
        
        # Register WARP
        & $warpPath register 2>$null
        Start-Sleep -Seconds 2
        
        # Connect to WARP
        & $warpPath connect
        Start-Sleep -Seconds 5
        
        # Verify connection
        $status = & $warpPath status
        if ($status -match "Connected") {
            Write-Host "[OK] WARP connected - your IP has been changed!" -ForegroundColor Green
            Write-Host ""
            Write-Host "Now opening download links in browser..." -ForegroundColor Cyan
            Write-Host "You should be able to download without rate limits!" -ForegroundColor Yellow
            Write-Host ""
            
            foreach ($file in $downloads) {
                Write-Host "Opening: $($file.Name)" -ForegroundColor White
                Start-Process $file.URL
                Write-Host "  Save to: $($file.Destination)"
                Start-Sleep -Seconds 2
            }
            
            Write-Host ""
            Write-Host "After downloads complete:" -ForegroundColor Yellow
            Write-Host "  1. Move files to the correct locations shown above"
            Write-Host "  2. Run: warp-cli disconnect (to disconnect WARP)"
            Write-Host ""
        } else {
            throw "Failed to connect to WARP"
        }
    }
    
    "2" {
        # ProtonVPN method
        Write-Host ""
        Write-Host "=== ProtonVPN Method ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "ProtonVPN offers a free tier that can be used to bypass MEGA rate limits."
        Write-Host ""
        Write-Host "Steps:" -ForegroundColor Yellow
        Write-Host "  1. Sign up at: https://protonvpn.com/free-vpn" -ForegroundColor Cyan
        Write-Host "  2. Download and install ProtonVPN"
        Write-Host "  3. Connect to any free server (US, Netherlands, or Japan)"
        Write-Host "  4. Re-run this script and select option 3 (Manual VPN)"
        Write-Host ""
        
        Start-Process "https://protonvpn.com/free-vpn"
        
        Write-Host "Press any key after you've set up ProtonVPN..."
        pause
    }
    
    "3" {
        # Manual VPN method
        Write-Host ""
        Write-Host "=== Manual VPN Method ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "Make sure you're connected to a VPN that changes your public IP."
        Write-Host ""
        
        # Get current IP
        Write-Host "Checking current IP..." -ForegroundColor Cyan
        try {
            $ip = (Invoke-RestMethod -Uri "https://api.ipify.org" -UseBasicParsing)
            Write-Host "Your current public IP: $ip" -ForegroundColor White
        } catch {
            Write-Host "Couldn't determine IP (this is fine)" -ForegroundColor Yellow
        }
        
        Write-Host ""
        $confirm = Read-Host "Are you connected to a VPN? (Y/N)"
        
        if ($confirm -eq "Y") {
            Write-Host ""
            Write-Host "Opening download links..." -ForegroundColor Cyan
            
            foreach ($file in $downloads) {
                Write-Host "Opening: $($file.Name)" -ForegroundColor White
                Start-Process $file.URL
                Write-Host "  Save to: $($file.Destination)"
                Start-Sleep -Seconds 2
            }
            
            Write-Host ""
            Write-Host "Download the files, then move them to the correct locations." -ForegroundColor Yellow
        } else {
            Write-Host "Please connect to a VPN first, then re-run this script."
        }
    }
    
    "4" {
        # aria2c method
        Write-Host ""
        Write-Host "=== aria2c Multi-Connection Method ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "aria2c uses multiple connections which can sometimes bypass rate limits."
        Write-Host "However, this may not work with MEGA's encryption."
        Write-Host ""
        
        # Check for aria2c
        $aria2 = Get-Command aria2c -ErrorAction SilentlyContinue
        
        if (-not $aria2) {
            Write-Host "Installing aria2c via winget..." -ForegroundColor Yellow
            winget install aria2.aria2 --accept-package-agreements --accept-source-agreements
            
            # Refresh PATH
            $env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + [System.Environment]::GetEnvironmentVariable("Path","User")
        }
        
        Write-Host ""
        Write-Host "Note: MEGA's encrypted downloads may not work well with aria2c." -ForegroundColor Yellow
        Write-Host "Consider using the WARP method (option 1) instead."
        Write-Host ""
        pause
    }
    
    "5" {
        # Wait method
        Write-Host ""
        Write-Host "=== Wait for Rate Limit Reset ===" -ForegroundColor Cyan
        Write-Host ""
        Write-Host "MEGA's rate limit typically resets after 6 hours."
        Write-Host ""
        
        $resetTime = (Get-Date).AddHours(6)
        Write-Host "Estimated reset time: $resetTime" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "After waiting, open these URLs to download:" -ForegroundColor Cyan
        
        foreach ($file in $downloads) {
            Write-Host ""
            Write-Host "  $($file.Name):" -ForegroundColor White
            Write-Host "  $($file.URL)"
            Write-Host "  Save to: $($file.Destination)"
        }
        
        Write-Host ""
        Write-Host "You can also set a reminder:" -ForegroundColor Yellow
        $remind = Read-Host "Set Windows reminder for 6 hours from now? (Y/N)"
        
        if ($remind -eq "Y") {
            # Create a scheduled task to open downloads
            $action = New-ScheduledTaskAction -Execute "powershell.exe" -Argument "-NoProfile -Command `"Start-Process 'https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8'; Start-Process 'https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk'`""
            $trigger = New-ScheduledTaskTrigger -Once -At $resetTime
            Register-ScheduledTask -TaskName "MEGA_Download_Reminder" -Action $action -Trigger $trigger -Description "Open MEGA download links after rate limit reset" -Force
            
            Write-Host "[OK] Reminder set for $resetTime" -ForegroundColor Green
        }
    }
    
    default {
        Write-Host "Invalid choice. Exiting."
        exit 1
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  After Download Complete" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "1. Verify files are in correct locations:" -ForegroundColor Yellow
Write-Host "   - common-2.MPQ -> C:\WowClassicGrindBot\Json\MPQ\"
Write-Host "   - mmaps.7z     -> C:\WowClassicGrindBot\Navigation\"
Write-Host ""
Write-Host "2. Extract MMAP archive:" -ForegroundColor Yellow
Write-Host "   Right-click mmaps.7z -> 7-Zip -> Extract Here"
Write-Host "   Move 'mmaps' folder to C:\WowClassicGrindBot\Navigation\"
Write-Host ""
Write-Host "3. Start pathfinding:" -ForegroundColor Yellow
Write-Host "   C:\WowClassicGrindBot\StartAll.bat"
Write-Host ""
