# Fix-BindPadMinimal.ps1
# This script recreates the BindPadMinimal addon with proper encoding

$addonPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\BindPadMinimal"

Write-Host "Fixing BindPadMinimal Addon..." -ForegroundColor Cyan
Write-Host ""

# Ensure directory exists
if (-not (Test-Path $addonPath)) {
    New-Item -ItemType Directory -Path $addonPath -Force | Out-Null
    Write-Host "Created addon directory" -ForegroundColor Green
}

# Create TOC file (ASCII encoding, no BOM)
$tocContent = @"
## Interface: 20505
## Title: BindPadMinimal
## Version: 1.0
## Author: WowGrindBot
## Notes: Minimal BindPad replacement providing BindPadMacro button for DataToColor

BindPadMinimal.xml
"@

$tocPath = Join-Path $addonPath "BindPadMinimal.toc"
[System.IO.File]::WriteAllText($tocPath, $tocContent, [System.Text.Encoding]::ASCII)
Write-Host "Created BindPadMinimal.toc (ASCII encoding)" -ForegroundColor Green

# Create XML file (ASCII encoding, no BOM, Unix line endings)
# Using the simplest possible valid WoW XML
$xmlContent = "<Ui xmlns=""http://www.blizzard.com/wow/ui/"">`n<Button name=""BindPadMacro"" inherits=""SecureActionButtonTemplate""/>`n<Button name=""BindPadKey"" inherits=""SecureActionButtonTemplate""/>`n</Ui>"

$xmlPath = Join-Path $addonPath "BindPadMinimal.xml"
[System.IO.File]::WriteAllText($xmlPath, $xmlContent, [System.Text.Encoding]::ASCII)
Write-Host "Created BindPadMinimal.xml (ASCII encoding)" -ForegroundColor Green

# Verify the files
Write-Host ""
Write-Host "Verification:" -ForegroundColor Yellow

# Check XML is valid
try {
    [xml]$test = Get-Content $xmlPath -Raw
    Write-Host "  ✓ XML parses correctly" -ForegroundColor Green
} catch {
    Write-Host "  ✗ XML parse error: $($_.Exception.Message)" -ForegroundColor Red
}

# Check for BOM
$bytes = [System.IO.File]::ReadAllBytes($xmlPath)
if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
    Write-Host "  ✗ File has BOM (unexpected)" -ForegroundColor Red
} else {
    Write-Host "  ✓ No BOM (correct)" -ForegroundColor Green
}

# Show file contents
Write-Host ""
Write-Host "XML File Contents:" -ForegroundColor Yellow
Get-Content $xmlPath
Write-Host ""

# Show hex dump of first 50 bytes
Write-Host "First 50 bytes (hex):" -ForegroundColor Yellow
$bytes = [System.IO.File]::ReadAllBytes($xmlPath)
$hex = ($bytes[0..([Math]::Min(49, $bytes.Length-1))] | ForEach-Object { "{0:X2}" -f $_ }) -join " "
Write-Host $hex
Write-Host ""

Write-Host "=" * 60 -ForegroundColor Cyan
Write-Host "Done! Now in WoW:" -ForegroundColor Green
Write-Host "  1. Type /reload" -ForegroundColor White
Write-Host "  2. Type /run print(BindPadMacro and 'EXISTS' or 'NIL')" -ForegroundColor White
Write-Host "  3. Type /dcactions" -ForegroundColor White
Write-Host "=" * 60 -ForegroundColor Cyan
