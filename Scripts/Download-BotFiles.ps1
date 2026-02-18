<#
.SYNOPSIS
    Download WowClassicGrindBot Required Files Using MEGAcmd
    
.DESCRIPTION
    This script helps download the required MPQ and MMAP files for WowClassicGrindBot
    using MEGAcmd to bypass MEGA rate limiting. It supports resume capability.
    
.PARAMETER FileType
    Which files to download: MPQ (for V1 pathfinding) or MMAP (for V3 pathfinding)
    
.PARAMETER Version
    WoW version: Vanilla, TBC, or WOTLK
    
.EXAMPLE
    .\Download-BotFiles.ps1 -FileType MPQ -Version TBC
    
.EXAMPLE
    .\Download-BotFiles.ps1 -FileType MMAP -Version Vanilla
#>

param(
    [ValidateSet("MPQ", "MMAP", "Both")]
    [string]$FileType = "MPQ",
    
    [ValidateSet("Vanilla", "TBC", "WOTLK")]
    [string]$Version = "TBC"
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  WowClassicGrindBot File Downloader" -ForegroundColor Cyan
Write-Host "  Uses MEGAcmd to bypass rate limits" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""

# File definitions with MEGA links from the repository
$Files = @{
    MPQ = @{
        Vanilla = @{
            Name = "common-2.MPQ"
            URL = "https://mega.nz/file/vXQCBCha#m7COhB9HQd86a5iNAT0-fMLsc-BtoTRO1eIBJNrdTH8"
            Size = "1.7 GB"
            Destination = "C:\WowClassicGrindBot\Json\MPQ\common-2.MPQ"
        }
        TBC = @{
            Name = "expansion.MPQ"
            URL = "https://mega.nz/file/Of4i2YQS#egDGj-SXi9RigG-_8kPITihFsLom2L1IFF-ltnB3wmU"
            Size = "1.8 GB"
            Destination = "C:\WowClassicGrindBot\Json\MPQ\expansion.MPQ"
        }
        WOTLK = @{
            Name = "lichking.MPQ"
            URL = "https://mega.nz/file/vDYWSTrK#fvaiuHpd-FTVsQT4ghGLK6QJLZyA87c1rlBEeu1_Btk"
            Size = "2.5 GB"
            Destination = "C:\WowClassicGrindBot\Json\MPQ\lichking.MPQ"
        }
    }
    MMAP = @{
        Vanilla_TBC = @{
            Name = "mmaps_v15_0_1_530_571_50_angle.7z"
            URL = "https://mega.nz/file/7HgkHIyA#c_gzUeTadecWY0JDY3KT39ktfPGLs2vzt_90bMvhszk"
            Size = "~2 GB"
            Destination = "C:\WowClassicGrindBot\Navigation\mmaps_v15.7z"
        }
        Vanilla_TBC_WOTLK = @{
            Name = "mmaps_vanilla_tbc_wotlk.7z"
            URL = "https://mega.nz/file/zWQ2XIKI#9EKWOPyyTMfY1LACkcP_wioZ0poVIuaGh2xcRh4V9dw"
            Size = "~3 GB"
            Destination = "C:\WowClassicGrindBot\Navigation\mmaps_wotlk.7z"
        }
    }
}

# Check if MEGAcmd is installed
$megacmdPath = "$env:LOCALAPPDATA\MEGAcmd\MEGAcmdShell.exe"
$megagetPath = "$env:LOCALAPPDATA\MEGAcmd\mega-get.bat"

if (-not (Test-Path $megacmdPath)) {
    Write-Host "[!] MEGAcmd is not installed!" -ForegroundColor Red
    Write-Host ""
    Write-Host "MEGAcmd is required to download files from MEGA without rate limits."
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  1. Install MEGAcmd automatically (recommended)"
    Write-Host "  2. Manual installation instructions"
    Write-Host "  3. Alternative: Manual download via browser"
    Write-Host ""
    
    $choice = Read-Host "Enter your choice (1/2/3)"
    
    switch ($choice) {
        "1" {
            Write-Host ""
            Write-Host "Downloading MEGAcmd installer..." -ForegroundColor Green
            
            $installerUrl = "https://mega.nz/MEGAcmdSetup64.exe"
            $installerPath = "$env:TEMP\MEGAcmdSetup64.exe"
            
            try {
                Invoke-WebRequest -Uri $installerUrl -OutFile $installerPath -UseBasicParsing
                
                Write-Host "Installing MEGAcmd..." -ForegroundColor Green
                Write-Host "  - The installer will run silently"
                Write-Host "  - Please wait a moment..."
                Write-Host ""
                
                Start-Process -FilePath $installerPath -ArgumentList "/S" -Wait
                
                # Wait for installation to complete
                Start-Sleep -Seconds 5
                
                if (Test-Path $megacmdPath) {
                    Write-Host "[OK] MEGAcmd installed successfully!" -ForegroundColor Green
                    Write-Host ""
                    Write-Host "Please restart this script to continue downloading."
                    pause
                    exit 0
                } else {
                    throw "Installation completed but MEGAcmd not found"
                }
            }
            catch {
                Write-Host "[ERROR] Failed to install MEGAcmd: $_" -ForegroundColor Red
                exit 1
            }
        }
        "2" {
            Write-Host ""
            Write-Host "Manual Installation Instructions:" -ForegroundColor Yellow
            Write-Host "  1. Download from: https://mega.nz/cmd"
            Write-Host "  2. Run the installer: MEGAcmdSetup64.exe"
            Write-Host "  3. Restart this script after installation"
            Write-Host ""
            pause
            exit 0
        }
        "3" {
            Write-Host ""
            Write-Host "Manual Download URLs:" -ForegroundColor Yellow
            Write-Host ""
            
            if ($FileType -eq "MPQ" -or $FileType -eq "Both") {
                $file = $Files.MPQ[$Version]
                Write-Host "  $($file.Name) ($($file.Size))" -ForegroundColor White
                Write-Host "  $($file.URL)" -ForegroundColor Cyan
                Write-Host "  Save to: $($file.Destination)"
                Write-Host ""
            }
            
            if ($FileType -eq "MMAP" -or $FileType -eq "Both") {
                Write-Host "  MMAP Files:" -ForegroundColor White
                foreach ($key in $Files.MMAP.Keys) {
                    $file = $Files.MMAP[$key]
                    Write-Host "    $($file.Name) ($($file.Size))" -ForegroundColor Gray
                    Write-Host "    $($file.URL)" -ForegroundColor Cyan
                    Write-Host ""
                }
            }
            
            Write-Host "After downloading manually, place files in the shown locations."
            pause
            exit 0
        }
        default {
            Write-Host "Invalid choice. Exiting."
            exit 1
        }
    }
}

# Function to download file using MEGAcmd
function Download-MegaFile {
    param(
        [string]$Url,
        [string]$Destination,
        [string]$Name,
        [string]$Size
    )
    
    Write-Host ""
    Write-Host "Downloading: $Name ($Size)" -ForegroundColor Green
    Write-Host "  From: $Url"
    Write-Host "  To:   $Destination"
    Write-Host ""
    
    # Create destination directory if it doesn't exist
    $destDir = Split-Path -Parent $Destination
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    
    # Check if file already exists
    if (Test-Path $Destination) {
        Write-Host "[!] File already exists: $Destination" -ForegroundColor Yellow
        $overwrite = Read-Host "Overwrite? (Y/N)"
        if ($overwrite -ne "Y") {
            Write-Host "Skipped."
            return $true
        }
        Remove-Item $Destination -Force
    }
    
    # Download using mega-get
    Write-Host "Starting download (this may take a while)..." -ForegroundColor Cyan
    Write-Host "  - MEGAcmd supports resume if interrupted"
    Write-Host "  - Press Ctrl+C to pause, re-run script to resume"
    Write-Host ""
    
    try {
        # Use mega-get command
        $megaget = "$env:LOCALAPPDATA\MEGAcmd\mega-get.bat"
        
        # Extract file ID from MEGA URL
        if ($Url -match "mega\.nz/file/([^#]+)#(.+)") {
            $fileId = $matches[1]
            $key = $matches[2]
            $megaUrl = "https://mega.nz/#!${fileId}!${key}"
        } else {
            throw "Invalid MEGA URL format"
        }
        
        # Run mega-get
        $process = Start-Process -FilePath $megaget -ArgumentList "$megaUrl `"$Destination`"" -NoNewWindow -PassThru -Wait
        
        if ($process.ExitCode -eq 0 -and (Test-Path $Destination)) {
            Write-Host ""
            Write-Host "[OK] Downloaded successfully!" -ForegroundColor Green
            
            # Show file size
            $fileSize = (Get-Item $Destination).Length / 1GB
            Write-Host "  File size: $([math]::Round($fileSize, 2)) GB"
            
            return $true
        } else {
            throw "mega-get failed with exit code $($process.ExitCode)"
        }
    }
    catch {
        Write-Host ""
        Write-Host "[ERROR] Download failed: $_" -ForegroundColor Red
        Write-Host ""
        Write-Host "Troubleshooting:" -ForegroundColor Yellow
        Write-Host "  1. Check your internet connection"
        Write-Host "  2. Verify MEGA link is still valid"
        Write-Host "  3. Try manual download: $Url"
        Write-Host "  4. Re-run this script to resume incomplete download"
        Write-Host ""
        return $false
    }
}

# Determine which files to download
$filesToDownload = @()

if ($FileType -eq "MPQ" -or $FileType -eq "Both") {
    $filesToDownload += $Files.MPQ[$Version]
}

if ($FileType -eq "MMAP") {
    if ($Version -eq "WOTLK") {
        $filesToDownload += $Files.MMAP.Vanilla_TBC_WOTLK
    } else {
        $filesToDownload += $Files.MMAP.Vanilla_TBC
    }
} elseif ($FileType -eq "Both") {
    $filesToDownload += $Files.MMAP.Vanilla_TBC
}

# Confirm download
Write-Host "Files to download:" -ForegroundColor Yellow
$totalSize = 0
foreach ($file in $filesToDownload) {
    Write-Host "  - $($file.Name) ($($file.Size))"
    # Estimate total size (rough)
    if ($file.Size -match "(\d+\.?\d*)") {
        $totalSize += [double]$matches[1]
    }
}
Write-Host ""
Write-Host "Estimated total: ~$([math]::Round($totalSize, 1)) GB" -ForegroundColor Cyan
Write-Host ""

$confirm = Read-Host "Start download? (Y/N)"
if ($confirm -ne "Y") {
    Write-Host "Cancelled."
    exit 0
}

# Download files
$success = $true
foreach ($file in $filesToDownload) {
    $result = Download-MegaFile -Url $file.URL -Destination $file.Destination -Name $file.Name -Size $file.Size
    if (-not $result) {
        $success = $false
        break
    }
}

Write-Host ""
Write-Host "=============================================" -ForegroundColor Cyan

if ($success) {
    Write-Host "  Download Complete!" -ForegroundColor Green
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Files have been downloaded to:" -ForegroundColor Green
    foreach ($file in $filesToDownload) {
        Write-Host "  $($file.Destination)"
    }
    
    # Extract MMAP if needed
    if ($FileType -eq "MMAP" -or $FileType -eq "Both") {
        Write-Host ""
        Write-Host "Next steps for MMAP files:" -ForegroundColor Yellow
        Write-Host "  1. Extract the .7z file using 7-Zip"
        Write-Host "  2. Move the 'mmaps' folder to C:\WowClassicGrindBot\Navigation\"
        Write-Host "  3. Start the navigation server with StartNavigationServer.bat"
    }
} else {
    Write-Host "  Download Failed" -ForegroundColor Red
    Write-Host "=============================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Some files failed to download." -ForegroundColor Red
    Write-Host "You can re-run this script to resume incomplete downloads."
}

Write-Host ""
