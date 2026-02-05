param(
    [string]$BotPath = "C:\\WowClassicGrindBot",
    [int]$Port = 47110,
    [int]$WaitSeconds = 3,
    [int]$EventLookbackSeconds = 90
)

$ErrorActionPreference = 'Stop'

function Get-PeMachine([string]$Path) {
    $fs = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try {
        $br = New-Object System.IO.BinaryReader($fs)

        $mz = $br.ReadUInt16()
        if ($mz -ne 0x5A4D) { return "Unknown" } # "MZ"

        $fs.Seek(0x3C, [System.IO.SeekOrigin]::Begin) | Out-Null
        $lfanew = $br.ReadInt32()
        if ($lfanew -le 0) { return "Unknown" }

        $fs.Seek($lfanew, [System.IO.SeekOrigin]::Begin) | Out-Null
        $peSig = $br.ReadUInt32()
        if ($peSig -ne 0x00004550) { return "Unknown" } # "PE\0\0"

        $machine = $br.ReadUInt16()
        switch ($machine) {
            0x014C { return "x86" }
            0x8664 { return "x64" }
            0x01C4 { return "ARM" }
            0xAA64 { return "ARM64" }
            default { return ("0x{0:X4}" -f $machine) }
        }
    } finally {
        $fs.Dispose()
    }
}

function Get-VcRuntimeInfo {
    $results = New-Object System.Collections.Generic.List[object]

    $keys =
    @(
        "HKLM:\\SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64",
        "HKLM:\\SOFTWARE\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x86",
        "HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x64",
        "HKLM:\\SOFTWARE\\WOW6432Node\\Microsoft\\VisualStudio\\14.0\\VC\\Runtimes\\x86"
    )

    foreach ($k in $keys) {
        if (-not (Test-Path $k)) { continue }
        try {
            $p = Get-ItemProperty $k
            $results.Add([pscustomobject]@{
                Key = $k
                Installed = $p.Installed
                Version = $p.Version
                Major = $p.Major
                Minor = $p.Minor
                Bld = $p.Bld
                Rbld = $p.Rbld
            }) | Out-Null
        } catch {
        }
    }

    return $results
}

$navExe = Join-Path $BotPath "Navigation\\AmeisenNavigationServer.exe"
$logDir = Join-Path $BotPath "logs\\diagnostics"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$stamp = (Get-Date).ToUniversalTime().ToString("yyyyMMdd_HHmmss")
$outPath = Join-Path $logDir "navserver_diagnose_${stamp}.txt"

Add-Content -Path $outPath -Value ("UTC: {0:u}" -f (Get-Date).ToUniversalTime())
Add-Content -Path $outPath -Value ("BotPath: {0}" -f $BotPath)
Add-Content -Path $outPath -Value ("Exe: {0}" -f $navExe)
Add-Content -Path $outPath -Value ("Port: {0}" -f $Port)
Add-Content -Path $outPath -Value ""

if (-not (Test-Path $navExe)) {
    Add-Content -Path $outPath -Value "ERROR: Navigation server executable not found."
    Write-Host "Navigation server executable not found: $navExe"
    Write-Host "Wrote: $outPath"
    exit 2
}

$exeItem = Get-Item $navExe
$machine = Get-PeMachine $navExe
$hash = (Get-FileHash -Algorithm SHA256 -Path $navExe).Hash
$ver = $exeItem.VersionInfo

Add-Content -Path $outPath -Value ("FileSize: {0} bytes" -f $exeItem.Length)
Add-Content -Path $outPath -Value ("PEMachine: {0}" -f $machine)
Add-Content -Path $outPath -Value ("SHA256: {0}" -f $hash)
Add-Content -Path $outPath -Value ("ProductVersion: {0}" -f $ver.ProductVersion)
Add-Content -Path $outPath -Value ("FileVersion: {0}" -f $ver.FileVersion)
Add-Content -Path $outPath -Value ""

$vc = Get-VcRuntimeInfo
Add-Content -Path $outPath -Value "VC++ Runtime Registry:"
if ($vc.Count -eq 0) {
    Add-Content -Path $outPath -Value "  (no VC runtime registry entries found)"
} else {
    foreach ($row in $vc) {
        Add-Content -Path $outPath -Value ("  {0} Installed={1} Version={2}" -f $row.Key, $row.Installed, $row.Version)
    }
}
Add-Content -Path $outPath -Value ""

$dumpbin = Get-Command dumpbin.exe -ErrorAction SilentlyContinue
if ($dumpbin -ne $null) {
    Add-Content -Path $outPath -Value ("dumpbin: {0}" -f $dumpbin.Source)
    Add-Content -Path $outPath -Value "dumpbin /dependents:"
    try {
        $deps = & $dumpbin.Source /dependents $navExe 2>&1
        $deps | ForEach-Object { Add-Content -Path $outPath -Value ("  {0}" -f $_) }
    } catch {
        Add-Content -Path $outPath -Value ("  ERROR: dumpbin failed: {0}" -f $_.Exception.Message)
    }
} else {
    Add-Content -Path $outPath -Value "dumpbin: not found (install Visual Studio Build Tools to enable /dependents scan)"
}
Add-Content -Path $outPath -Value ""

Write-Host "Starting navigation server to observe crash behavior..."
$startUtc = (Get-Date).ToUniversalTime()

$proc = $null
try {
    $proc = Start-Process -FilePath $navExe -WorkingDirectory (Split-Path -Parent $navExe) -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds ([Math]::Max(1, $WaitSeconds))
} catch {
    Add-Content -Path $outPath -Value ("ERROR: Failed to start process: {0}" -f $_.Exception.Message)
} finally {
    if ($proc -ne $null) {
        try {
            $proc.Refresh()
            $hasExited = $proc.HasExited
            $exitCode = if ($hasExited) { $proc.ExitCode } else { $null }
            Add-Content -Path $outPath -Value ("Process: PID={0} HasExited={1} ExitCode={2}" -f $proc.Id, $hasExited, $exitCode)

            if (-not $hasExited) {
                try { Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue } catch {}
                Add-Content -Path $outPath -Value "Process was still running; stopped for diagnostics."
            }
        } catch {
            Add-Content -Path $outPath -Value ("ERROR: Failed to query/stop process: {0}" -f $_.Exception.Message)
        }
    }
}

$endUtc = (Get-Date).ToUniversalTime()
$from = $startUtc.AddSeconds(-[Math]::Max(10, $EventLookbackSeconds))
$to = $endUtc.AddSeconds(10)

Add-Content -Path $outPath -Value ""
Add-Content -Path $outPath -Value ("EventLog window: {0:u} .. {1:u}" -f $from, $to)

function Append-Events([string]$logName, [string]$providerName, [int[]]$ids) {
    try {
        $filter = @{
            LogName = $logName
            ProviderName = $providerName
            Id = $ids
            StartTime = $from
            EndTime = $to
        }

        $events = Get-WinEvent -FilterHashtable $filter -ErrorAction SilentlyContinue | Select-Object -First 10
        if ($events -eq $null -or $events.Count -eq 0) {
            Add-Content -Path $outPath -Value ("{0}/{1}: (no events)" -f $logName, $providerName)
            return
        }

        Add-Content -Path $outPath -Value ("{0}/{1}:" -f $logName, $providerName)
        foreach ($e in $events) {
            Add-Content -Path $outPath -Value ("  [{0:u}] Id={1} Level={2} {3}" -f $e.TimeCreated.ToUniversalTime(), $e.Id, $e.LevelDisplayName, $e.Message)
            Add-Content -Path $outPath -Value ""
        }
    } catch {
        Add-Content -Path $outPath -Value ("{0}/{1}: ERROR reading events: {2}" -f $logName, $providerName, $_.Exception.Message)
    }
}

Append-Events -logName "Application" -providerName "Application Error" -ids @(1000, 1026)
Append-Events -logName "Application" -providerName "Windows Error Reporting" -ids @(1001)

Write-Host "Wrote diagnostics: $outPath"
Write-Host "If exit code is 0xC0000005, focus on missing runtimes or incompatible DLLs."
