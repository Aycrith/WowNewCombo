# WowClassicGrindBot Diagnostic Script
# Run this to check current state of the installation

Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  WOWCLASSICGRINDBOT DIAGNOSTIC REPORT" -ForegroundColor Cyan
Write-Host "  Generated: $(Get-Date)" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host ""

# Check WoW Process
Write-Host "[1] WoW PROCESS" -ForegroundColor Yellow
$wow = Get-Process -Name "WowClassic*" -ErrorAction SilentlyContinue
if ($wow) {
    Write-Host "    ✓ WoW is running (PID: $($wow.Id))" -ForegroundColor Green
} else {
    Write-Host "    ✗ WoW is NOT running" -ForegroundColor Red
}
Write-Host ""

# Check Bot Processes
Write-Host "[2] BOT PROCESSES" -ForegroundColor Yellow
$blazor = Get-Process -Name "BlazorServer*" -ErrorAction SilentlyContinue
$nav = Get-Process -Name "AmeisenNav*" -ErrorAction SilentlyContinue
if ($blazor) {
    Write-Host "    ✓ BlazorServer running (PID: $($blazor.Id))" -ForegroundColor Green
} else {
    Write-Host "    ✗ BlazorServer NOT running" -ForegroundColor Red
}
if ($nav) {
    Write-Host "    ✓ Navigation Server running (PID: $($nav.Id))" -ForegroundColor Green
} else {
    Write-Host "    ✗ Navigation Server NOT running" -ForegroundColor Red
}
Write-Host ""

# Check Config Files
Write-Host "[3] CONFIGURATION FILES" -ForegroundColor Yellow
$configPath = "C:\WowClassicGrindBot\BlazorServer\bin\Release\net10.0"

$files = @(
    @{Name="data_config.json"; Required=$true},
    @{Name="addon_config.json"; Required=$true},
    @{Name="frame_config.json"; Required=$true},
    @{Name="appsettings.json"; Required=$true}
)

foreach ($file in $files) {
    $path = Join-Path $configPath $file.Name
    if (Test-Path $path) {
        Write-Host "    ✓ $($file.Name) exists" -ForegroundColor Green
    } else {
        if ($file.Required) {
            Write-Host "    ✗ $($file.Name) MISSING (REQUIRED)" -ForegroundColor Red
        } else {
            Write-Host "    - $($file.Name) missing (optional)" -ForegroundColor Gray
        }
    }
}
Write-Host ""

# Check Addons
Write-Host "[4] WOW ADDONS" -ForegroundColor Yellow
$addonPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns"

$addons = @("DataToColor", "BindPadMinimal", "BindPad", "BindPad_DISABLED")
foreach ($addon in $addons) {
    $path = Join-Path $addonPath $addon
    if (Test-Path $path) {
        Write-Host "    ✓ $addon exists" -ForegroundColor Green
    } else {
        Write-Host "    - $addon not found" -ForegroundColor Gray
    }
}
Write-Host ""

# Check BindPadMinimal XML
Write-Host "[5] BINDPADMINIMAL XML VALIDATION" -ForegroundColor Yellow
$xmlPath = "C:\Program Files (x86)\World of Warcraft\_anniversary_\Interface\AddOns\BindPadMinimal\BindPadMinimal.xml"
if (Test-Path $xmlPath) {
    $content = Get-Content $xmlPath -Raw
    $bytes = [System.IO.File]::ReadAllBytes($xmlPath)
    
    Write-Host "    File size: $($bytes.Length) bytes"
    
    # Check for BOM
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
        Write-Host "    ✗ Has UTF-8 BOM (might cause issues)" -ForegroundColor Red
    } else {
        Write-Host "    ✓ No BOM detected" -ForegroundColor Green
    }
    
    # Check for required elements
    if ($content -match "BindPadMacro") {
        Write-Host "    ✓ Contains BindPadMacro" -ForegroundColor Green
    } else {
        Write-Host "    ✗ Missing BindPadMacro" -ForegroundColor Red
    }
    
    if ($content -match "</Ui>") {
        Write-Host "    ✓ Has closing </Ui> tag" -ForegroundColor Green
    } else {
        Write-Host "    ✗ Missing closing </Ui> tag" -ForegroundColor Red
    }
    
    # Try XML parse
    try {
        [xml]$xml = $content
        Write-Host "    ✓ XML parses correctly" -ForegroundColor Green
    } catch {
        Write-Host "    ✗ XML parse error: $($_.Exception.Message)" -ForegroundColor Red
    }
} else {
    Write-Host "    ✗ BindPadMinimal.xml not found" -ForegroundColor Red
}
Write-Host ""

# Check MPQ Files
Write-Host "[6] MPQ FILES" -ForegroundColor Yellow
$mpqPath = "C:\WowClassicGrindBot\Json\MPQ"
if (Test-Path $mpqPath) {
    $mpqFiles = Get-ChildItem $mpqPath -Filter "*.MPQ"
    foreach ($mpq in $mpqFiles) {
        $sizeMB = [math]::Round($mpq.Length / 1MB, 0)
        Write-Host "    ✓ $($mpq.Name) ($sizeMB MB)" -ForegroundColor Green
    }
} else {
    Write-Host "    ✗ MPQ folder not found" -ForegroundColor Red
}
Write-Host ""

# Check Navigation Config
Write-Host "[7] NAVIGATION CONFIG" -ForegroundColor Yellow
$navConfig = "C:\WowClassicGrindBot\Navigation\config.cfg"
if (Test-Path $navConfig) {
    $content = Get-Content $navConfig -Raw
    if ($content -match "sMmapsPath=(.+)") {
        $mmapsPath = $matches[1].Trim()
        Write-Host "    MMAPS Path: $mmapsPath"
        if (Test-Path $mmapsPath) {
            $count = (Get-ChildItem $mmapsPath).Count
            Write-Host "    ✓ MMAPS folder exists ($count files)" -ForegroundColor Green
        } else {
            Write-Host "    ✗ MMAPS folder NOT FOUND" -ForegroundColor Red
        }
    }
} else {
    Write-Host "    ✗ Navigation config not found" -ForegroundColor Red
}
Write-Host ""

# Summary
Write-Host "=" * 70 -ForegroundColor Cyan
Write-Host "  CRITICAL ISSUE: frame_config.json is missing" -ForegroundColor Red
Write-Host "  This requires successful Auto Configuration from the web UI" -ForegroundColor Yellow
Write-Host "  Web UI: http://localhost:5000/FrameConfiguration" -ForegroundColor Cyan
Write-Host "=" * 70 -ForegroundColor Cyan
