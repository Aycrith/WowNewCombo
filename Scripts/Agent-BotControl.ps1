#Requires -Version 5.1
<#
.SYNOPSIS
  Unified CLI control plane for WowClassicGrindBot.

.DESCRIPTION
  Provides a one-command startup + validation workflow and reusable agent-friendly
  CLI actions for status, restart, stop, monitoring, and direct API calls.

.EXAMPLES
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action StartAndValidate -Profile BloodElf_Rogue_8-60_TBC.json
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action StartAndValidate -Profile BloodElf_Rogue_8-60_TBC.json -BypassActionBar
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action Status
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action Stop
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Agent-BotControl.ps1 -Action Api -ApiMethod GET -ApiPath /api/launch/status
#>
[CmdletBinding()]
param(
    [ValidateSet("Start", "StartAndValidate", "Validate", "Status", "Stop", "Restart", "Monitor", "Api", "CollectEvidence", "Soak", "LiveSession", "Doctor", "GameCmd", "FlagsProfile", "WatchNav", "NavTriage", "ValidateReroute", "ValidateNoProgress", "ValidateCombat")]
    [string]$Action = "StartAndValidate",

    [string]$Profile = "BloodElf_Rogue_8-60_TBC.json",
    [string]$BotRoot = "",
    [string]$BaseUrl = "http://localhost:5000",
    [int]$WebPort = 5000,
    [int]$NavigationPort = 47110,
    [int]$StartupTimeoutSeconds = 120,
    [int]$ReadinessTimeoutSeconds = 120,
    [int]$ValidationTimeoutSeconds = 30,
    [int]$MonitorIntervalSeconds = 10,
    [int]$SoakMinutes = 20,
    [int]$WindowMinutes = 10,
    [int]$MaxPatchLoops = 5,
    [string]$ArtifactTag = "",
    [int]$EvidenceIntervalSeconds = 150,
    [int]$ShortValidationSeconds = 240,
    [ValidateSet("current", "stable-live", "triage-baseline", "triage-hazard", "triage-predictive")]
    [string]$NavProfile = "stable-live",
    [switch]$NoAutoNavProfile,
    [switch]$RestoreFlagsOnExit = $true,
    [string]$Command = "",
    [ValidateSet("reload", "bindings", "actions", "actionbar", "dcflush", "dcnumberkeys", "none")]
    [string]$Verify = "none",
    [int]$MaxCommandRetries = 3,
    [int]$WatchSeconds = 120,
    [int]$WatchCadenceMs = 1000,
    [int]$TriageMinutesPerProfile = 3,
    [string]$TriageProfiles = "triage-baseline,triage-hazard,triage-predictive,current",
    [switch]$AutoRepairReadiness = $true,

    [switch]$AllowStartWithWarnings,
    [switch]$BypassKeyBindings,
    [switch]$BypassActionBar,
    [switch]$SkipCharacterGate,
    [switch]$StartMonitor,
    [switch]$StopServices,
    [switch]$SkipBuild,

    [ValidateSet("GET", "POST", "PUT", "DELETE")]
    [string]$ApiMethod = "GET",
    [string]$ApiPath = "",
    [string]$ApiBody = "",
    [int]$ApiTimeoutSeconds = 30
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($BotRoot))
{
    $BotRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..")).Path
}
else
{
    $BotRoot = (Resolve-Path -LiteralPath $BotRoot).Path
}

$script:LogsDir = Join-Path $BotRoot "logs"
New-Item -ItemType Directory -Path $script:LogsDir -Force | Out-Null
$script:RunStamp = Get-Date -Format "yyyyMMdd-HHmmss"
$script:RunTag = "agentctl-$script:RunStamp"
$script:SessionTag = if ([string]::IsNullOrWhiteSpace($ArtifactTag)) { "live-session-$script:RunStamp" } else { $ArtifactTag }
$script:SessionArtifactDir = Join-Path $script:LogsDir $script:SessionTag
$script:DtcReloadRecoveryAttempted = $false
$script:FeatureFlagSnapshotPath = $null
$script:FeatureFlagSnapshotActive = $false
$script:FeatureFlagProfileApplied = $null
$script:ApiBaseCandidates = @()
$script:PreferredApiBase = $null

function Write-Info([string]$Message)
{
    Write-Host "[INFO ] $Message" -ForegroundColor Gray
}

function Write-Ok([string]$Message)
{
    Write-Host "[ OK  ] $Message" -ForegroundColor Green
}

function Write-WarnLine([string]$Message)
{
    Write-Host "[WARN ] $Message" -ForegroundColor Yellow
}

function Write-ErrLine([string]$Message)
{
    Write-Host "[ERR  ] $Message" -ForegroundColor Red
}

function Get-ApiBaseCandidates
{
    param([Parameter(Mandatory = $true)][string]$InputBaseUrl)

    $candidates = New-Object System.Collections.Generic.List[string]
    $baseValue = $InputBaseUrl.Trim().TrimEnd('/')
    if (-not [string]::IsNullOrWhiteSpace($baseValue))
    {
        [void]$candidates.Add($baseValue)
    }

    $uri = $null
    if ([Uri]::TryCreate($baseValue, [UriKind]::Absolute, [ref]$uri))
    {
        $uriHost = $uri.Host.ToLowerInvariant()
        if ($uriHost -eq "localhost")
        {
            [void]$candidates.Add(("{0}://127.0.0.1:{1}" -f $uri.Scheme, $uri.Port))
        }
        elseif ($uriHost -eq "127.0.0.1")
        {
            [void]$candidates.Add(("{0}://localhost:{1}" -f $uri.Scheme, $uri.Port))
        }
    }

    return @($candidates | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
}

function Get-OrderedApiBases
{
    $ordered = New-Object System.Collections.Generic.List[string]
    if (-not [string]::IsNullOrWhiteSpace($script:PreferredApiBase))
    {
        [void]$ordered.Add($script:PreferredApiBase)
    }

    foreach ($candidate in @($script:ApiBaseCandidates))
    {
        if ([string]::IsNullOrWhiteSpace($candidate))
        {
            continue
        }

        if (-not $ordered.Contains($candidate))
        {
            [void]$ordered.Add($candidate)
        }
    }

    return @($ordered)
}

function Test-TransientApiError
{
    param([string]$Message)

    if ([string]::IsNullOrWhiteSpace($Message))
    {
        return $false
    }

    $patterns = @(
        "Only one usage of each socket address",
        "actively refused",
        "No connection could be made",
        "Unable to connect",
        "configured HttpClient.Timeout",
        "HttpClient.Timeout",
        "timed out",
        "connection was forcibly closed",
        "reset by peer",
        "A connection attempt failed"
    )

    foreach ($pattern in $patterns)
    {
        if ($Message.IndexOf($pattern, [StringComparison]::OrdinalIgnoreCase) -ge 0)
        {
            return $true
        }
    }

    return $false
}

$script:ApiBaseCandidates = Get-ApiBaseCandidates -InputBaseUrl $BaseUrl
if (@($script:ApiBaseCandidates).Count -eq 0)
{
    throw "Unable to resolve valid API base URL candidates from '$BaseUrl'"
}
$script:PreferredApiBase = $script:ApiBaseCandidates[0]

function Get-JsonDepth([object]$Obj, [int]$Depth = 8)
{
    return ($Obj | ConvertTo-Json -Depth $Depth)
}

function Ensure-SessionArtifactDir
{
    if (-not (Test-Path -LiteralPath $script:SessionArtifactDir))
    {
        New-Item -ItemType Directory -Path $script:SessionArtifactDir -Force | Out-Null
    }

    return $script:SessionArtifactDir
}

function Get-SafeArtifactName([string]$Name)
{
    $invalid = [System.IO.Path]::GetInvalidFileNameChars()
    $safe = $Name
    foreach ($c in $invalid)
    {
        $safe = $safe.Replace([string]$c, "_")
    }

    return $safe
}

function Write-ArtifactText
{
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$Content
    )

    $dir = Ensure-SessionArtifactDir
    $path = Join-Path $dir (Get-SafeArtifactName -Name $Name)
    Set-Content -LiteralPath $path -Value $Content -Encoding UTF8
    return $path
}

function Write-ArtifactJson
{
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowNull()][object]$Object
    )

    $dir = Ensure-SessionArtifactDir
    $path = Join-Path $dir (Get-SafeArtifactName -Name $Name)
    $Object | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $path -Encoding UTF8
    return $path
}

function Read-FeatureFlagsJson
{
    $flagsPath = Get-FeatureFlagsFilePath
    if (-not (Test-Path -LiteralPath $flagsPath))
    {
        return $null
    }

    return (Get-Content -LiteralPath $flagsPath -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Get-FeatureFlagEffectiveSubset
{
    param([AllowNull()][object]$FlagsDocument = $null)

    if ($null -eq $FlagsDocument)
    {
        $FlagsDocument = Read-FeatureFlagsJson
    }

    if ($null -eq $FlagsDocument)
    {
        return $null
    }

    $featuresRoot = $FlagsDocument
    if ($FlagsDocument.PSObject.Properties["Features"])
    {
        $featuresRoot = $FlagsDocument.Features
    }

    return [ordered]@{
        HazardAvoidance = $featuresRoot.HazardAvoidance
        StuckSensitivity = $featuresRoot.StuckSensitivity
        PathSmoothing = $featuresRoot.PathSmoothing
        InputSecurity = $featuresRoot.InputSecurity
    }
}

function New-SessionManifestBaseline
{
    return [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Action = $Action
        Profile = $Profile
        Branch = (& git rev-parse --abbrev-ref HEAD 2>$null)
        Commit = (& git rev-parse --short HEAD 2>$null)
        SoakMinutes = $SoakMinutes
        WindowMinutes = $WindowMinutes
        EvidenceIntervalSeconds = $EvidenceIntervalSeconds
        ShortValidationSeconds = $ShortValidationSeconds
        MaxPatchLoops = $MaxPatchLoops
        RuntimeLogSnapshot = Get-LatestRuntimeLogFileSnapshot
    }
}

function Write-SessionManifest
{
    param(
        [Parameter(Mandatory = $true)][hashtable]$Baseline,
        [switch]$IncludeRuntimeState
    )

    $manifest = [ordered]@{}
    foreach ($entry in $Baseline.GetEnumerator())
    {
        $manifest[$entry.Key] = $entry.Value
    }

    $manifest.RequestedNavProfile = $NavProfile
    $manifest.AppliedNavProfile = $(if ([string]::IsNullOrWhiteSpace("$script:FeatureFlagProfileApplied")) { "current" } else { $script:FeatureFlagProfileApplied })
    $manifest.RuntimeFlagsFilePath = Get-FeatureFlagsFilePath
    $manifest.ResolvedEffectiveFlags = Get-FeatureFlagEffectiveSubset

    if ($IncludeRuntimeState)
    {
        $botStatus = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
        $sessionStats = Invoke-AgentApiSafe -Method GET -Path "/api/session/stats" -TimeoutSec 5

        $manifest.RuntimeMode = $(if ($botStatus.Success -and $null -ne $botStatus.Result.RuntimeMode) { "$($botStatus.Result.RuntimeMode)" } else { $null })
        $manifest.AgentAvailable = $(if ($botStatus.Success -and $null -ne $botStatus.Result.AgentAvailable) { [bool]$botStatus.Result.AgentAvailable } else { $null })
        $manifest.BotStatus = $botStatus
        $manifest.SessionStats = $sessionStats
    }

    [void](Write-ArtifactJson -Name "session-manifest.json" -Object $manifest)
}

function Write-ActionFailureArtifact
{
    param(
        [Parameter(Mandatory = $true)][string]$ActionName,
        [Parameter(Mandatory = $true)][System.Management.Automation.ErrorRecord]$ErrorRecord
    )

    try
    {
        Ensure-SessionArtifactDir | Out-Null

        $botStatus = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
        $sessionStats = Invoke-AgentApiSafe -Method GET -Path "/api/session/stats" -TimeoutSec 5
        $artifactName = "{0}-{1}-failure.json" -f $script:RunTag, (Get-SafeArtifactName -Name $ActionName.ToLowerInvariant())
        $line = $null
        try { $line = $ErrorRecord.InvocationInfo.ScriptLineNumber } catch { }

        [void](Write-ArtifactJson -Name $artifactName -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Action = $ActionName
            Profile = $Profile
            RequestedNavProfile = $NavProfile
            AppliedNavProfile = $(if ([string]::IsNullOrWhiteSpace("$script:FeatureFlagProfileApplied")) { "current" } else { $script:FeatureFlagProfileApplied })
            RuntimeFlagsFilePath = Get-FeatureFlagsFilePath
            ResolvedEffectiveFlags = Get-FeatureFlagEffectiveSubset
            ErrorMessage = $ErrorRecord.Exception.Message
            ScriptLineNumber = $line
            ScriptStackTrace = $ErrorRecord.ScriptStackTrace
            RuntimeMode = $(if ($botStatus.Success -and $null -ne $botStatus.Result.RuntimeMode) { "$($botStatus.Result.RuntimeMode)" } else { $null })
            AgentAvailable = $(if ($botStatus.Success -and $null -ne $botStatus.Result.AgentAvailable) { [bool]$botStatus.Result.AgentAvailable } else { $null })
            BotStatus = $botStatus
            SessionStats = $sessionStats
        }))
    }
    catch
    {
        Write-WarnLine "Failed to write action failure artifact: $($_.Exception.Message)"
    }
}

function New-ActionSummary
{
    param([Parameter(Mandatory = $true)][string]$ActionName)

    return [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Action = $ActionName
        Profile = $Profile
        RequestedNavProfile = $NavProfile
        AppliedNavProfile = $(if ([string]::IsNullOrWhiteSpace("$script:FeatureFlagProfileApplied")) { "current" } else { $script:FeatureFlagProfileApplied })
        RuntimeFlagsFilePath = Get-FeatureFlagsFilePath
        SessionArtifactDir = $script:SessionArtifactDir
        Passed = $false
        FailureReasons = @()
    }
}

function Add-ActionFailureReason
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Summary,
        [string]$Reason
    )

    if ([string]::IsNullOrWhiteSpace($Reason))
    {
        return
    }

    $existing = @($Summary["FailureReasons"])
    if ($existing -contains $Reason)
    {
        return
    }

    $Summary["FailureReasons"] = @($existing + $Reason)
}

function Write-ActionSummaryArtifact
{
    param(
        [Parameter(Mandatory = $true)][string]$ActionName,
        [AllowNull()][object]$Summary
    )

    if ($null -eq $Summary)
    {
        return
    }

    Ensure-SessionArtifactDir | Out-Null
    $artifactName = "{0}-{1}-summary.json" -f $script:RunTag, (Get-SafeArtifactName -Name $ActionName.ToLowerInvariant())
    [void](Write-ArtifactJson -Name $artifactName -Object $Summary)
}

function Get-ApiSafeResultOrNull
{
    param([AllowNull()][object]$Response)

    if ($null -ne $Response -and [bool]$Response.Success -and $null -ne $Response.Result)
    {
        return $Response.Result
    }

    return $null
}

function Read-JsonArtifact
{
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path) -or -not (Test-Path -LiteralPath $Path))
    {
        return $null
    }

    return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
}

function Measure-WindowMaximum
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Windows,
        [Parameter(Mandatory = $true)][string]$PropertyName
    )

    $values = @()
    foreach ($window in $Windows)
    {
        try
        {
            $raw = $window.$PropertyName
            if ($null -ne $raw)
            {
                $values += [double]$raw
            }
        }
        catch { }
    }

    if ($values.Count -eq 0)
    {
        return 0.0
    }

    return [double](($values | Measure-Object -Maximum).Maximum)
}

function Invoke-AgentApiSafe
{
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowNull()][object]$Body = $null,
        [int]$TimeoutSec = 15
    )

    try
    {
        return [ordered]@{
            Success = $true
            Result = (Invoke-AgentApi -Method $Method -Path $Path -Body $Body -TimeoutSec $TimeoutSec)
            Error = $null
        }
    }
    catch
    {
        return [ordered]@{
            Success = $false
            Result = $null
            Error = $_.Exception.Message
        }
    }
}

function Save-ApiSnapshot
{
    param(
        [Parameter(Mandatory = $true)][string]$ApiPath,
        [Parameter(Mandatory = $true)][string]$FileStem,
        [int]$TimeoutSec = 10
    )

    $response = Invoke-AgentApiSafe -Method GET -Path $ApiPath -TimeoutSec $TimeoutSec
    if ($response.Success)
    {
        $saved = Write-ArtifactJson -Name $FileStem -Object ([ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                Path = $ApiPath
                Success = $true
                Data = $response.Result
            })
        Write-Info "Saved API snapshot: $ApiPath -> $saved"
        return $true
    }

    $savedErr = Write-ArtifactJson -Name $FileStem -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Path = $ApiPath
            Success = $false
            Error = $response.Error
        })
    Write-WarnLine "API snapshot failed: $ApiPath (saved error to $savedErr)"
    return $false
}

function Get-PortStateSnapshot
{
    return [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        WebPort = [ordered]@{
            Port = $WebPort
            Listening = (Test-PortListening -Port $WebPort)
            Pid = (Get-ListeningPid -Port $WebPort)
        }
        NavigationPort = [ordered]@{
            Port = $NavigationPort
            Listening = (Test-PortListening -Port $NavigationPort)
            Pid = (Get-ListeningPid -Port $NavigationPort)
        }
        Processes = [ordered]@{
            BlazorServer = @((Get-Process -Name "BlazorServer" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id))
            AmeisenNavigationServer = @((Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id))
            WowClassic = @((Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id))
        }
    }
}

function Save-LatestLogTail
{
    param(
        [Parameter(Mandatory = $true)][string]$Pattern,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$Lines = 300
    )

    $filesResp = Invoke-AgentApiSafe -Method GET -Path "/api/logs/files" -TimeoutSec 8
    if (-not $filesResp.Success -or $null -eq $filesResp.Result -or $null -eq $filesResp.Result.Files)
    {
        Write-WarnLine "Unable to list logs for $Label"
        return
    }

    $file = $filesResp.Result.Files |
        Where-Object { "$($_.Relative)" -like $Pattern -or "$($_.Name)" -like $Pattern } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $file)
    {
        Write-WarnLine "No log file matched '$Pattern' for $Label"
        return
    }

    $encoded = [Uri]::EscapeDataString("$($file.Relative)")
    $tailResp = Invoke-AgentApiSafe -Method GET -Path "/api/logs/tail?file=$encoded&lines=$Lines" -TimeoutSec 12
    if (-not $tailResp.Success)
    {
        Write-WarnLine "Failed to tail log '$($file.Relative)'"
        return
    }

    [void](Write-ArtifactJson -Name ("{0}-meta.json" -f $Label) -Object $file)
    [void](Write-ArtifactText -Name ("{0}-tail.txt" -f $Label) -Content "$($tailResp.Result.Content)")
    Write-Info "Saved log tail for $Label from $($file.Relative)"
}

function Get-LatestRuntimeLogFileSnapshot
{
    $filesResp = Invoke-AgentApiSafe -Method GET -Path "/api/logs/files" -TimeoutSec 8
    if (-not $filesResp.Success -or $null -eq $filesResp.Result -or $null -eq $filesResp.Result.Files)
    {
        return $null
    }

    $runtimeFile = $filesResp.Result.Files |
        Where-Object { "$($_.Relative)" -like "out*.log" -or "$($_.Name)" -like "out*.log" } |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1

    if ($null -eq $runtimeFile)
    {
        return $null
    }

    $contentRoot = "$($filesResp.Result.ContentRoot)"
    $relative = "$($runtimeFile.Relative)"
    $fullPath = $null
    if (-not [string]::IsNullOrWhiteSpace($contentRoot) -and -not [string]::IsNullOrWhiteSpace($relative))
    {
        $normalizedRelative = $relative -replace '/', '\'
        $fullPath = Join-Path $contentRoot $normalizedRelative
    }

    return [ordered]@{
        Name = "$($runtimeFile.Name)"
        Relative = $relative
        ContentRoot = $contentRoot
        FullPath = $fullPath
        SizeBytes = [long]$runtimeFile.SizeBytes
        LastWriteTimeUtc = "$($runtimeFile.LastWriteTimeUtc)"
    }
}

function Get-FileContentFromOffset
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [long]$StartOffset = 0
    )

    if (-not (Test-Path -LiteralPath $Path))
    {
        return $null
    }

    $fileInfo = Get-Item -LiteralPath $Path -ErrorAction Stop
    $offset = [Math]::Max(0L, [Math]::Min([long]$StartOffset, [long]$fileInfo.Length))
    $stream = [System.IO.File]::Open($Path, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
    try
    {
        [void]$stream.Seek($offset, [System.IO.SeekOrigin]::Begin)
        $reader = New-Object System.IO.StreamReader($stream, [System.Text.Encoding]::UTF8, $true, 4096, $true)
        try
        {
            return $reader.ReadToEnd()
        }
        finally
        {
            $reader.Dispose()
        }
    }
    finally
    {
        $stream.Dispose()
    }
}

function Invoke-AgentApi
{
    param(
        [Parameter(Mandatory = $true)][string]$Method,
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowNull()][object]$Body = $null,
        [int]$TimeoutSec = 15
    )

    $methodUpper = $Method.ToUpperInvariant()
    $isGet = $methodUpper -eq "GET"
    $maxAttempts = if ($isGet) { 3 } else { 1 }
    $attemptsExecuted = 0
    $lastError = $null
    $attemptErrors = New-Object System.Collections.Generic.List[string]
    if ($isGet)
    {
        $bases = @(Get-OrderedApiBases)
    }
    else
    {
        $preferredBase = $script:PreferredApiBase
        if ([string]::IsNullOrWhiteSpace($preferredBase) -and @($script:ApiBaseCandidates).Count -gt 0)
        {
            $preferredBase = $script:ApiBaseCandidates[0]
        }

        $bases = @($preferredBase)
    }
    $bases = @($bases | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)

    if ($bases.Count -eq 0)
    {
        $bases = @($BaseUrl.Trim().TrimEnd('/'))
    }

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
    {
        $attemptsExecuted = $attempt
        $attemptHadTransientError = $false
        foreach ($base in $bases)
        {
            $uri = "$base$Path"

            try
            {
                if ($methodUpper -eq "GET")
                {
                    $result = Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $TimeoutSec
                }
                else
                {
                    if ($null -eq $Body)
                    {
                        $result = Invoke-RestMethod -Uri $uri -Method $methodUpper -TimeoutSec $TimeoutSec -ContentType "application/json" -Body "{}"
                    }
                    else
                    {
                        if ($Body -is [string])
                        {
                            $jsonBody = $Body
                        }
                        else
                        {
                            $jsonBody = $Body | ConvertTo-Json -Depth 12
                        }

                        $result = Invoke-RestMethod -Uri $uri -Method $methodUpper -TimeoutSec $TimeoutSec -ContentType "application/json" -Body $jsonBody
                    }
                }

                $script:PreferredApiBase = $base
                return $result
            }
            catch
            {
                $err = $_.Exception.Message
                $response = $null
                if ($null -ne $_.Exception -and $_.Exception.PSObject.Properties.Match("Response").Count -gt 0)
                {
                    $response = $_.Exception.Response
                }

                $responseBody = $null
                if ($null -ne $response)
                {
                    if ($response.PSObject.Methods.Match("GetResponseStream").Count -gt 0)
                    {
                        try
                        {
                            $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
                            $responseBody = $reader.ReadToEnd()
                        }
                        catch
                        {
                            # keep default error message
                        }
                    }
                    elseif ($response.PSObject.Properties.Match("Content").Count -gt 0 -and $null -ne $response.Content)
                    {
                        try
                        {
                            $responseBody = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                        }
                        catch
                        {
                            # keep default error message
                        }
                    }
                }

                if (-not [string]::IsNullOrWhiteSpace($responseBody))
                {
                    $err = "$err`n$responseBody"
                }

                if ($null -ne $_.ErrorDetails -and -not [string]::IsNullOrWhiteSpace($_.ErrorDetails.Message))
                {
                    $errorDetailsMessage = "$($_.ErrorDetails.Message)"
                    if ([string]::IsNullOrEmpty($err) -or -not $err.Contains($errorDetailsMessage, [System.StringComparison]::Ordinal))
                    {
                        $err = "$err`n$errorDetailsMessage"
                    }
                }

                $attemptErr = "Attempt $attempt [$base]: $err"
                [void]$attemptErrors.Add($attemptErr)
                $lastError = $err
                if (Test-TransientApiError -Message "$err")
                {
                    $attemptHadTransientError = $true
                }
            }
        }

        if (-not $attemptHadTransientError)
        {
            break
        }

        if ($attempt -lt $maxAttempts)
        {
            $delayMs = (200 * $attempt) + (Get-Random -Minimum 50 -Maximum 250)
            Start-Sleep -Milliseconds $delayMs
        }
    }

    $attemptSummary = @($attemptErrors) -join " || "
    throw "API $Method $Path failed after $attemptsExecuted attempt(s): $attemptSummary"
}

function Get-FeatureFlagsFilePath
{
    $runtimeCandidates = @(
        (Join-Path $BotRoot "BlazorServer\bin\Release\net10.0\runtime_feature_flags.json"),
        (Join-Path $BotRoot "BlazorServer\bin\Debug\net10.0\runtime_feature_flags.json"),
        (Join-Path $BotRoot "BlazorServer\runtime_feature_flags.json")
    )

    foreach ($candidate in $runtimeCandidates)
    {
        if (Test-Path -LiteralPath $candidate)
        {
            return $candidate
        }
    }

    return $runtimeCandidates[$runtimeCandidates.Count - 1]
}

function Get-FeatureFlagProfilePatch
{
    param(
        [Parameter(Mandatory = $true)][string]$ProfileName
    )

    switch ($ProfileName)
    {
        "current"
        {
            return @{}
        }
        "stable-live"
        {
            return [ordered]@{}
        }
        "triage-baseline"
        {
            return [ordered]@{}
        }
        "triage-hazard"
        {
            $patch = [ordered]@{}
            $patch["Features.HazardAvoidance.Enabled"] = $true
            return $patch
        }
        "triage-predictive"
        {
            return [ordered]@{
                "Features.HazardAvoidance.Enabled" = $false
                "Features.StuckSensitivity.Enabled" = $true
                "Features.StuckSensitivity.MinDistance" = 0.08
                "Features.StuckSensitivity.UnstuckAfterMs" = 2800
                "Features.StuckSensitivity.EnablePredictiveDetection" = $true
                "Features.StuckSensitivity.PredictiveRiskThreshold" = 75
                "Features.StuckSensitivity.ApproachTimeoutMultiplier" = 1.4
            }
        }
        default
        {
            throw "Unknown nav profile '$ProfileName'"
        }
    }
}

function Set-ObjectPathValue
{
    param(
        [Parameter(Mandatory = $true)][object]$Root,
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)]$Value
    )

    $parts = $Path.Split('.')
    $node = $Root
    for ($i = 0; $i -lt ($parts.Length - 1); $i++)
    {
        $part = $parts[$i]
        $prop = $node.PSObject.Properties[$part]
        if ($null -eq $prop)
        {
            $child = [pscustomobject]@{}
            $node | Add-Member -NotePropertyName $part -NotePropertyValue $child
            $node = $child
            continue
        }

        $node = $prop.Value
        if ($null -eq $node)
        {
            $child = [pscustomobject]@{}
            $prop.Value = $child
            $node = $child
        }
    }

    $leaf = $parts[$parts.Length - 1]
    $leafProp = $node.PSObject.Properties[$leaf]
    if ($null -eq $leafProp)
    {
        $node | Add-Member -NotePropertyName $leaf -NotePropertyValue $Value
    }
    else
    {
        $leafProp.Value = $Value
    }
}

function Save-FeatureFlagSnapshot
{
    $flagsPath = Get-FeatureFlagsFilePath
    if (-not (Test-Path -LiteralPath $flagsPath))
    {
        throw "Feature flags file not found: $flagsPath"
    }

    Ensure-SessionArtifactDir | Out-Null
    $raw = Get-Content -LiteralPath $flagsPath -Raw -Encoding UTF8
    $snapshotPath = Join-Path $script:SessionArtifactDir ("{0}-runtime_feature_flags.snapshot.json" -f $script:RunTag)
    Set-Content -LiteralPath $snapshotPath -Value $raw -Encoding UTF8

    $script:FeatureFlagSnapshotPath = $snapshotPath
    $script:FeatureFlagSnapshotActive = $true
    Write-Ok "Feature flag snapshot saved -> $snapshotPath"
    return $snapshotPath
}

function Wait-ForFeatureFlagsApplied
{
    param(
        [Parameter(Mandatory = $true)][hashtable]$ExpectedPatch,
        [int]$TimeoutSec = 10,
        [switch]$SkipIfApiUnavailable
    )

    if ($ExpectedPatch.Count -eq 0)
    {
        return $true
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $apiEverReachable = $false

    while ((Get-Date) -lt $deadline)
    {
        $resp = Invoke-AgentApiSafe -Method GET -Path "/api/features" -TimeoutSec 5
        if (-not $resp.Success -or $null -eq $resp.Result)
        {
            Start-Sleep -Milliseconds 500
            continue
        }

        $apiEverReachable = $true
        $featuresRoot = $resp.Result.Features
        if ($null -eq $featuresRoot)
        {
            Start-Sleep -Milliseconds 500
            continue
        }

        $allMatched = $true
        foreach ($entry in $ExpectedPatch.GetEnumerator())
        {
            $parts = $entry.Key.Split('.')
            if ($parts.Length -lt 2)
            {
                continue
            }

            $node = $resp.Result
            foreach ($part in $parts)
            {
                if ($null -eq $node)
                {
                    break
                }

                $prop = $node.PSObject.Properties[$part]
                if ($null -eq $prop)
                {
                    $node = $null
                    break
                }

                $node = $prop.Value
            }

            if ($null -eq $node)
            {
                $allMatched = $false
                break
            }

            $expected = $entry.Value
            if ($expected -is [double] -or $expected -is [single] -or $node -is [double] -or $node -is [single] -or $node -is [decimal])
            {
                if ([Math]::Abs(([double]$node) - ([double]$expected)) -gt 0.0001)
                {
                    $allMatched = $false
                    break
                }
            }
            elseif ("$node" -ne "$expected")
            {
                $allMatched = $false
                break
            }
        }

        if ($allMatched)
        {
            Write-Ok "Feature flags hot-reload applied"
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    if (-not $apiEverReachable -and $SkipIfApiUnavailable)
    {
        Write-WarnLine "Feature flag API unavailable; skipping hot-reload verification"
        return $false
    }

    throw "Timed out waiting for feature flags hot-reload verification."
}

function Apply-FeatureFlagProfile
{
    param(
        [Parameter(Mandatory = $true)][string]$ProfileName,
        [switch]$VerifyViaApi
    )

    $patch = Get-FeatureFlagProfilePatch -ProfileName $ProfileName
    $script:FeatureFlagProfileApplied = $ProfileName

    $flagsPath = Get-FeatureFlagsFilePath
    $json = Read-FeatureFlagsJson

    if ($patch.Count -gt 0)
    {
        if (-not $script:FeatureFlagSnapshotActive)
        {
            Save-FeatureFlagSnapshot | Out-Null
        }

        foreach ($entry in $patch.GetEnumerator())
        {
            Set-ObjectPathValue -Root $json -Path $entry.Key -Value $entry.Value
        }

        $json.LastModified = (Get-Date).ToUniversalTime().ToString("o")
        ($json | ConvertTo-Json -Depth 64) | Set-Content -LiteralPath $flagsPath -Encoding UTF8
    }

    [void](Write-ArtifactJson -Name ("{0}-flags-profile-{1}.json" -f $script:RunTag, $ProfileName) -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            RequestedProfile = $ProfileName
            Patch = $patch
            RuntimeFlagsFilePath = $flagsPath
            EffectiveFlags = (Get-FeatureFlagEffectiveSubset -FlagsDocument $(if ($patch.Count -gt 0) { $json } else { Read-FeatureFlagsJson }))
        }))

    if ($patch.Count -gt 0)
    {
        Write-Ok "Applied nav feature-flag profile '$ProfileName'"
    }
    else
    {
        Write-Info "Nav profile '$ProfileName' matches the checked-in baseline; no runtime flag changes applied"
    }

    if ($VerifyViaApi)
    {
        [void](Wait-ForFeatureFlagsApplied -ExpectedPatch $patch -TimeoutSec 12 -SkipIfApiUnavailable)
    }
}

function Restore-FeatureFlagSnapshot
{
    if (-not $script:FeatureFlagSnapshotActive -or [string]::IsNullOrWhiteSpace($script:FeatureFlagSnapshotPath))
    {
        return
    }

    $flagsPath = Get-FeatureFlagsFilePath
    if (-not (Test-Path -LiteralPath $script:FeatureFlagSnapshotPath))
    {
        Write-WarnLine "Feature flag snapshot missing; cannot restore: $script:FeatureFlagSnapshotPath"
        return
    }

    $raw = Get-Content -LiteralPath $script:FeatureFlagSnapshotPath -Raw -Encoding UTF8
    Set-Content -LiteralPath $flagsPath -Value $raw -Encoding UTF8
    Write-Ok "Restored runtime feature flags from snapshot"
    $script:FeatureFlagSnapshotActive = $false
    $script:FeatureFlagProfileApplied = $null

    try
    {
        $currentProfile = $script:FeatureFlagProfileApplied
        if ($null -ne $currentProfile)
        {
            # no-op placeholder
        }
    }
    catch { }
}

function Get-KeybindingDiagnosticsSafe
{
    return (Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/keybindings" -TimeoutSec 8)
}

function Get-ActionBarDiagnosticsSafe
{
    return (Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/actionbar" -TimeoutSec 8)
}

function Test-LaunchHandshakeRecovered
{
    $launchResp = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
    if (-not $launchResp.Success -or $null -eq $launchResp.Result)
    {
        return $false
    }

    $handshakeCheck = Get-LaunchCheck -Launch $launchResp.Result -Title "Addon Handshake"
    if ($null -eq $handshakeCheck)
    {
        return $false
    }

    $message = "$($handshakeCheck.Message)"
    return ($message -notlike "*GlobalTime=0*")
}

function Get-VerifiedCommandState
{
    param([Parameter(Mandatory = $true)][string]$VerifyKind)

    switch ($VerifyKind)
    {
        "bindings"
        {
            $resp = Get-KeybindingDiagnosticsSafe
            return [ordered]@{ Kind = $VerifyKind; Response = $resp }
        }
        "actions"
        {
            $resp = Get-ActionBarDiagnosticsSafe
            return [ordered]@{ Kind = $VerifyKind; Response = $resp }
        }
        "actionbar"
        {
            $resp = Get-ActionBarDiagnosticsSafe
            return [ordered]@{ Kind = $VerifyKind; Response = $resp }
        }
        "reload"
        {
            $frames = Invoke-AgentApiSafe -Method GET -Path "/api/test/frames" -TimeoutSec 10
            $snap = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 10
            return [ordered]@{ Kind = $VerifyKind; Frames = $frames; Snapshot = $snap }
        }
        "dcflush"
        {
            $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
            return [ordered]@{ Kind = $VerifyKind; Launch = $launch }
        }
        "dcnumberkeys"
        {
            $resp = Get-KeybindingDiagnosticsSafe
            return [ordered]@{ Kind = $VerifyKind; Response = $resp }
        }
        default
        {
            return [ordered]@{ Kind = $VerifyKind }
        }
    }
}

function Test-VerifiedCommandState
{
    param(
        [Parameter(Mandatory = $true)][string]$VerifyKind,
        [AllowNull()][object]$State,
        [AllowNull()][object]$BeforeState = $null
    )

    switch ($VerifyKind)
    {
        "none" { return $true }
        "reload"
        {
            $marker = $null
            if ($null -ne $State -and $null -ne $State.Frames -and $State.Frames.Success)
            {
                $marker = Get-TestFramesValidationMarker -FramesResponse $State.Frames.Result
            }

            $uiMapId = $null
            $level = $null
            if ($null -ne $State -and $null -ne $State.Snapshot -and $State.Snapshot.Success)
            {
                try
                {
                    $snap = $State.Snapshot.Result.Data.snapshot
                    if ($null -ne $snap)
                    {
                        $uiMapId = [int]$snap.UIMapId
                        $level = [int]$snap.Level
                    }
                }
                catch { }
            }

            return ($marker -eq 2000001) -or (($null -ne $uiMapId) -and ($null -ne $level) -and ($uiMapId -gt 0) -and ($level -gt 0))
        }
        "bindings"
        {
            if ($null -eq $State -or $null -eq $State.Response -or -not $State.Response.Success -or $null -eq $State.Response.Result)
            {
                return $false
            }

            $result = $State.Response.Result
            return [bool]$result.IsInitialized -and ([int]$result.TotalBindings -gt 0)
        }
        "dcnumberkeys"
        {
            if ($null -eq $State -or $null -eq $State.Response -or -not $State.Response.Success -or $null -eq $State.Response.Result)
            {
                return $false
            }

            $result = $State.Response.Result
            if ([bool]$result.IsInitialized -and ([int]$result.TotalBindings -gt 0))
            {
                return $true
            }

            if ($null -ne $BeforeState -and $null -ne $BeforeState.Response -and $BeforeState.Response.Success)
            {
                try
                {
                    return ([int]$result.MismatchCount -lt [int]$BeforeState.Response.Result.MismatchCount)
                }
                catch { }
            }

            return $false
        }
        "actions"
        {
            if ($null -eq $State -or $null -eq $State.Response -or -not $State.Response.Success -or $null -eq $State.Response.Result)
            {
                return $false
            }

            return [bool]$State.Response.Result.IsTextureInitialized
        }
        "actionbar"
        {
            if ($null -eq $State -or $null -eq $State.Response -or -not $State.Response.Success -or $null -eq $State.Response.Result)
            {
                return $false
            }

            return ([int]$State.Response.Result.IssueCount -eq 0)
        }
        "dcflush"
        {
            if ($null -eq $State -or $null -eq $State.Launch -or -not $State.Launch.Success -or $null -eq $State.Launch.Result)
            {
                return $false
            }

            $handshakeCheck = Get-LaunchCheck -Launch $State.Launch.Result -Title "Addon Handshake"
            if ($null -eq $handshakeCheck) { return $false }
            return ("$($handshakeCheck.Message)" -notlike "*GlobalTime=0*")
        }
        default
        {
            return $true
        }
    }
}

function Wait-ForVerifiedCommandEffect
{
    param(
        [Parameter(Mandatory = $true)][string]$VerifyKind,
        [AllowNull()][object]$BeforeState = $null,
        [int]$TimeoutSec = 8,
        [int]$PollMs = 1000
    )

    if ($VerifyKind -eq "none")
    {
        return [pscustomobject]@{ Success = $true; State = $null }
    }

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastState = $null
    while ((Get-Date) -lt $deadline)
    {
        $state = Get-VerifiedCommandState -VerifyKind $VerifyKind
        $lastState = $state
        if (Test-VerifiedCommandState -VerifyKind $VerifyKind -State $state -BeforeState $BeforeState)
        {
            return [pscustomobject]@{ Success = $true; State = $state }
        }

        Start-Sleep -Milliseconds $PollMs
    }

    return [pscustomobject]@{ Success = $false; State = $lastState }
}

function Invoke-LocalWowCommandFallback
{
    param(
        [Parameter(Mandatory = $true)][string]$SlashCommand
    )

    $scriptPath = Join-Path $BotRoot "send-wowcmd.ps1"
    if (-not (Test-Path -LiteralPath $scriptPath))
    {
        throw "Missing fallback sender script: $scriptPath"
    }

    & pwsh -NoProfile -ExecutionPolicy Bypass -File $scriptPath $SlashCommand
}

function Invoke-VerifiedSlashCommand
{
    param(
        [Parameter(Mandatory = $true)][string]$SlashCommand,
        [ValidateSet("reload", "bindings", "actions", "actionbar", "dcflush", "dcnumberkeys", "none")]
        [string]$VerifyKind = "none",
        [int]$Retries = 3,
        [switch]$BestEffort
    )

    if ($Retries -lt 1)
    {
        $Retries = 1
    }

    $beforeState = Get-VerifiedCommandState -VerifyKind $VerifyKind
    $attemptArtifacts = New-Object System.Collections.Generic.List[object]

    $dispatchPlans = @(
        [ordered]@{ Path = "api"; Body = @{ command = $SlashCommand; useBackgroundCompatibleInput = $false; preDelayMs = 200; postDelayMs = 500 } },
        [ordered]@{ Path = "api-bg"; Body = @{ command = $SlashCommand; useBackgroundCompatibleInput = $true; preDelayMs = 200; postDelayMs = 700 } },
        [ordered]@{ Path = "local"; Body = $null }
    )

    $planIndex = 0
    foreach ($plan in $dispatchPlans)
    {
        $planIndex++
        for ($retry = 1; $retry -le $Retries; $retry++)
        {
            $dispatchSuccess = $false
            $dispatchError = $null
            $dispatchResult = $null

            try
            {
                if ($plan.Path -eq "local")
                {
                    Invoke-LocalWowCommandFallback -SlashCommand $SlashCommand | Out-Null
                    $dispatchSuccess = $true
                    $dispatchResult = [ordered]@{ success = $true; dispatchPath = "send-wowcmd.ps1" }
                }
                else
                {
                    $resp = Invoke-AgentApiSafe -Method POST -Path "/api/diagnostics/fix/slash" -Body $plan.Body -TimeoutSec 20
                    $dispatchSuccess = [bool]$resp.Success -and ($null -ne $resp.Result) -and [bool]$resp.Result.Success
                    $dispatchResult = $resp
                    if (-not $dispatchSuccess)
                    {
                        $dispatchError = if ($resp.Success) { "$($resp.Result.Error)" } else { $resp.Error }
                    }
                }
            }
            catch
            {
                $dispatchError = $_.Exception.Message
                $dispatchSuccess = $false
            }

            $verifyOutcome = Wait-ForVerifiedCommandEffect -VerifyKind $VerifyKind -BeforeState $beforeState -TimeoutSec 8 -PollMs 1000
            $verified = [bool]$verifyOutcome.Success

            $attemptRecord = [ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                SlashCommand = $SlashCommand
                VerifyKind = $VerifyKind
                DispatchPlan = $plan.Path
                Retry = $retry
                DispatchSuccess = $dispatchSuccess
                DispatchError = $dispatchError
                Verified = $verified
                VerifyState = $verifyOutcome.State
                DispatchResult = $dispatchResult
            }
            [void]$attemptArtifacts.Add($attemptRecord)

            [void](Write-ArtifactJson -Name ("{0}-cmd-{1}-{2}-{3}.json" -f $script:RunTag, (Get-SafeArtifactName -Name $SlashCommand), $plan.Path, $retry) -Object $attemptRecord)

            if ($verified)
            {
                Write-Ok "Verified slash command $SlashCommand via $($plan.Path) (retry $retry)"
                return [pscustomobject]@{
                    Success = $true
                    Verified = $true
                    Attempts = $attemptArtifacts
                    DispatchPath = $plan.Path
                    VerifyKind = $VerifyKind
                }
            }

            if ($dispatchSuccess -and $VerifyKind -eq "none")
            {
                return [pscustomobject]@{
                    Success = $true
                    Verified = $true
                    Attempts = $attemptArtifacts
                    DispatchPath = $plan.Path
                    VerifyKind = $VerifyKind
                }
            }
        }
    }

    $message = "Slash command $SlashCommand did not verify (verify=$VerifyKind) after all dispatch paths"
    if ($BestEffort)
    {
        Write-WarnLine $message
        return [pscustomobject]@{
            Success = $false
            Verified = $false
            Attempts = $attemptArtifacts
            DispatchPath = $null
            VerifyKind = $VerifyKind
            Error = $message
        }
    }

    throw $message
}

function Wait-Until
{
    param(
        [Parameter(Mandatory = $true)][scriptblock]$Condition,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$TimeoutSec = 30,
        [int]$PollMs = 1000
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline)
    {
        if (& $Condition)
        {
            Write-Ok "$Label"
            return $true
        }

        Start-Sleep -Milliseconds $PollMs
    }

    Write-ErrLine "$Label (timeout ${TimeoutSec}s)"
    return $false
}

function Get-ListeningPid
{
    param([int]$Port)

    $rows = netstat -ano -p tcp | Select-String -Pattern ":$Port\s+.*LISTENING\s+(\d+)"
    foreach ($row in $rows)
    {
        if ($row -match "LISTENING\s+(\d+)")
        {
            return [int]$Matches[1]
        }
    }

    return $null
}

function Test-PortListening
{
    param([int]$Port)
    return $null -ne (Get-ListeningPid -Port $Port)
}

function Assert-ServiceReadinessGate
{
    param(
        [Parameter(Mandatory = $true)][string]$Context,
        [switch]$RequireApiHealth,
        [int]$TimeoutSec = 20
    )

    $deadline = (Get-Date).AddSeconds([Math]::Max(3, $TimeoutSec))
    $lastFailureReason = $null
    while ((Get-Date) -lt $deadline)
    {
        if (-not (Test-PortListening -Port $WebPort))
        {
            $lastFailureReason = "web port $WebPort is not listening"
            Start-Sleep -Milliseconds 750
            continue
        }

        if ($RequireApiHealth)
        {
            $health = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
            if (-not $health.Success -or $null -eq $health.Result)
            {
                $lastFailureReason = "/api/health unavailable ($($health.Error))"
                Start-Sleep -Milliseconds 750
                continue
            }
        }

        $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if (-not $launch.Success -or $null -eq $launch.Result)
        {
            $portListening = Test-PortListening -Port $NavigationPort
            if ($portListening)
            {
                return
            }

            $lastFailureReason = "launch status unavailable and navigation port $NavigationPort is not listening ($($launch.Error))"
            Start-Sleep -Milliseconds 750
            continue
        }

        $navigationAssessment = Get-LaunchNavigationAssessment -Launch $launch.Result
        if ([bool]$navigationAssessment.IsOk)
        {
            return
        }

        $lastFailureReason = if (-not [string]::IsNullOrWhiteSpace("$($navigationAssessment.BlockingReason)"))
        {
            "$($navigationAssessment.BlockingReason)"
        }
        else
        {
            "$($navigationAssessment.Message)"
        }

        Start-Sleep -Milliseconds 750
    }

    throw "Readiness gate failed ($Context): $lastFailureReason"
}

function Assert-ProfileRouteReadyGate
{
    param(
        [Parameter(Mandatory = $true)][string]$Context
    )

    $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
    if (-not $launch.Success -or $null -eq $launch.Result)
    {
        throw "Readiness gate failed ($Context): unable to read /api/launch/status ($($launch.Error))"
    }

    $profileCheck = Get-LaunchCheck -Launch $launch.Result -Title "Profile"
    $routeCheck = Get-LaunchCheck -Launch $launch.Result -Title "Route"
    $profileStatus = Get-LaunchStatusCode -Check $profileCheck
    $routeStatus = Get-LaunchStatusCode -Check $routeCheck

    if ($profileStatus -ne 2 -or $routeStatus -ne 2)
    {
        $profileMessage = if ($null -ne $profileCheck) { "$($profileCheck.Message)" } else { "missing Profile check" }
        $routeMessage = if ($null -ne $routeCheck) { "$($routeCheck.Message)" } else { "missing Route check" }
        throw "Readiness gate failed ($Context): profile/route not ready (Profile=$profileStatus '$profileMessage'; Route=$routeStatus '$routeMessage')"
    }
}

function Assert-CastingSnapshotReadyGate
{
    param(
        [Parameter(Mandatory = $true)][string]$Context,
        [int]$TimeoutSec = 20
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastReason = "navigation runtime not yet available"

    while ((Get-Date) -lt $deadline)
    {
        $runtime = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
        if ($runtime.Success -and $null -ne $runtime.Result)
        {
            if ($null -ne $runtime.Result.Casting)
            {
                return
            }

            $lastReason = "casting snapshot is null"
        }
        else
        {
            $lastReason = if ([string]::IsNullOrWhiteSpace("$($runtime.Error)")) { "navigation runtime request failed" } else { "$($runtime.Error)" }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Readiness gate failed ($Context): /api/diagnostics/navigation/runtime did not expose non-null casting payload within ${TimeoutSec}s ($lastReason)"
}

function Resolve-BlazorExecutable
{
    $candidate = Join-Path $BotRoot "BlazorServer\bin\Release\net10.0\BlazorServer.exe"
    if (Test-Path -LiteralPath $candidate)
    {
        return $candidate
    }

    throw "Missing BlazorServer binary: $candidate. Build with 'dotnet build MasterOfPuppets.sln -c Release'."
}

function Resolve-NavigationExecutable
{
    $defaultPath = Join-Path $BotRoot "Navigation\AmeisenNavigationServer.exe"
    if (Test-Path -LiteralPath $defaultPath)
    {
        return $defaultPath
    }

    $found = Get-ChildItem -Path (Join-Path $BotRoot "Navigation") -Recurse -Filter "AmeisenNavigationServer.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($null -ne $found)
    {
        return $found.FullName
    }

    throw "Could not find AmeisenNavigationServer.exe under '$BotRoot\Navigation'."
}

function Get-StartupStageValue([object]$Health)
{
    if ($null -eq $Health -or $null -eq $Health.Startup)
    {
        return -999
    }

    $stage = $Health.Startup.CurrentStage
    if ($stage -is [byte] -or
        $stage -is [sbyte] -or
        $stage -is [int16] -or
        $stage -is [uint16] -or
        $stage -is [int32] -or
        $stage -is [uint32] -or
        $stage -is [int64] -or
        $stage -is [uint64] -or
        $stage -is [single] -or
        $stage -is [double] -or
        $stage -is [decimal])
    {
        return [int]$stage
    }

    $name = "$stage"
    if ($name -match "^-?\d+$")
    {
        return [int]$name
    }
    if ($name -eq "Ready") { return 9 }
    if ($name -eq "Failed") { return -1 }
    return -998
}

function Get-StartupElapsedSeconds([object]$Health)
{
    try
    {
        if ($null -eq $Health -or $null -eq $Health.Startup -or $null -eq $Health.Startup.ElapsedTime)
        {
            return $null
        }

        $elapsedRaw = "$($Health.Startup.ElapsedTime)"
        if ([string]::IsNullOrWhiteSpace($elapsedRaw))
        {
            return $null
        }

        $elapsed = [TimeSpan]::Parse($elapsedRaw)
        return [int][Math]::Floor($elapsed.TotalSeconds)
    }
    catch
    {
        return $null
    }
}

function Get-TestFramesValidationMarker
{
    param([AllowNull()][object]$FramesResponse)

    try
    {
        if ($null -eq $FramesResponse -or $null -eq $FramesResponse.Data)
        {
            return $null
        }

        if ($null -eq $FramesResponse.Data.validationMarker)
        {
            return $null
        }

        return [int]$FramesResponse.Data.validationMarker
    }
    catch
    {
        return $null
    }
}

function Save-DtcRecoveryApiArtifact
{
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowNull()][object]$SafeResponse
    )

    [void](Write-ArtifactJson -Name $Name -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Success = $(if ($null -ne $SafeResponse) { [bool]$SafeResponse.Success } else { $false })
            Error = $(if ($null -ne $SafeResponse) { $SafeResponse.Error } else { "No response object" })
            Data = $(if ($null -ne $SafeResponse) { $SafeResponse.Result } else { $null })
        }))
}

function Test-DtcFrozenStartupSignature
{
    param(
        [switch]$IgnoreStageRequirement
    )

    $healthResp = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
    $frameStatusResp = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/status" -TimeoutSec 8
    $snapshotResp = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 10
    $framesResp = Invoke-AgentApiSafe -Method GET -Path "/api/test/frames" -TimeoutSec 10

    $wowRunning = $null -ne (Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue | Select-Object -First 1)
    $stageValue = $(if ($healthResp.Success) { Get-StartupStageValue -Health $healthResp.Result } else { -999 })
    $stageMatches = $IgnoreStageRequirement.IsPresent -or ($stageValue -eq 7)

    $addonNotVisible = $false
    if ($frameStatusResp.Success)
    {
        try
        {
            $addonNotVisible = [bool]$frameStatusResp.Result.AddonNotVisible
        }
        catch
        {
            $addonNotVisible = $false
        }
    }

    $snapshotZeroed = $false
    $uiMapId = $null
    $level = $null
    if ($snapshotResp.Success)
    {
        try
        {
            if ($null -ne $snapshotResp.Result.Data -and $null -ne $snapshotResp.Result.Data.snapshot)
            {
                $snap = $snapshotResp.Result.Data.snapshot
                $uiMapId = [int]$snap.UIMapId
                $level = [int]$snap.Level
                $snapshotZeroed = ($uiMapId -le 0) -and ($level -le 0)
            }
        }
        catch
        {
            $snapshotZeroed = $false
        }
    }

    $validationMarker = $(Get-TestFramesValidationMarker -FramesResponse $(if ($framesResp.Success) { $framesResp.Result } else { $null }))
    $framesMarkerMatches = ($null -eq $validationMarker) -or ($validationMarker -eq 0)

    $isFrozen = $wowRunning -and
        $healthResp.Success -and
        $stageMatches -and
        $frameStatusResp.Success -and
        $addonNotVisible -and
        $snapshotResp.Success -and
        $snapshotZeroed -and
        $framesMarkerMatches

    return [pscustomobject][ordered]@{
        IsFrozen = $isFrozen
        WowRunning = $wowRunning
        HealthReachable = [bool]$healthResp.Success
        StageValue = $stageValue
        RequireStage7 = (-not $IgnoreStageRequirement.IsPresent)
        StageMatches = $stageMatches
        AddonNotVisible = $addonNotVisible
        SnapshotZeroed = $snapshotZeroed
        SnapshotUIMapId = $uiMapId
        SnapshotLevel = $level
        FramesValidationMarker = $validationMarker
        FramesMarkerMatches = $framesMarkerMatches
        HealthResponse = $healthResp
        FrameStatusResponse = $frameStatusResp
        SnapshotResponse = $snapshotResp
        FramesResponse = $framesResp
    }
}

function Wait-ForDtcRecovery
{
    param(
        [int]$TimeoutSec = 30
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $lastProbe = $null

    while ((Get-Date) -lt $deadline)
    {
        $framesResp = Invoke-AgentApiSafe -Method GET -Path "/api/test/frames" -TimeoutSec 8
        $snapshotResp = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 8
        $diagResp = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/diagnostics" -TimeoutSec 8

        $marker = $(Get-TestFramesValidationMarker -FramesResponse $(if ($framesResp.Success) { $framesResp.Result } else { $null }))

        $uiMapId = $null
        $level = $null
        if ($snapshotResp.Success)
        {
            try
            {
                if ($null -ne $snapshotResp.Result.Data -and $null -ne $snapshotResp.Result.Data.snapshot)
                {
                    $snap = $snapshotResp.Result.Data.snapshot
                    $uiMapId = [int]$snap.UIMapId
                    $level = [int]$snap.Level
                }
            }
            catch
            {
                $uiMapId = $null
                $level = $null
            }
        }

        $detectedXOffset = $null
        if ($diagResp.Success)
        {
            try
            {
                $detectedXOffset = [int]$diagResp.Result.DetectedXOffset
            }
            catch
            {
                $detectedXOffset = $null
            }
        }

        $recovered = ($marker -eq 2000001) -or
            (($null -ne $uiMapId) -and ($null -ne $level) -and ($uiMapId -gt 0) -and ($level -gt 0)) -or
            (($null -ne $detectedXOffset) -and ($detectedXOffset -ge 0))

        $lastProbe = [pscustomobject][ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Recovered = $recovered
            FramesValidationMarker = $marker
            SnapshotUIMapId = $uiMapId
            SnapshotLevel = $level
            DetectedXOffset = $detectedXOffset
            FramesResponse = $framesResp
            SnapshotResponse = $snapshotResp
            DiagnosticsResponse = $diagResp
        }

        if ($recovered)
        {
            return [pscustomobject][ordered]@{
                Success = $true
                Probe = $lastProbe
            }
        }

        Start-Sleep -Seconds 1
    }

    return [pscustomobject][ordered]@{
        Success = $false
        Probe = $lastProbe
    }
}

function Invoke-DtcReloadFallbackScript
{
    $fallbackScript = Join-Path $BotRoot "send-reload.ps1"
    if (-not (Test-Path -LiteralPath $fallbackScript))
    {
        return [pscustomobject][ordered]@{
            Success = $false
            Error = "Fallback script not found: $fallbackScript"
            Output = $null
            ScriptPath = $fallbackScript
        }
    }

    try
    {
        $output = & pwsh -NoProfile -ExecutionPolicy Bypass -File $fallbackScript 2>&1 | Out-String
        return [pscustomobject][ordered]@{
            Success = $true
            Error = $null
            Output = $output
            ScriptPath = $fallbackScript
        }
    }
    catch
    {
        return [pscustomobject][ordered]@{
            Success = $false
            Error = $_.Exception.Message
            Output = $null
            ScriptPath = $fallbackScript
        }
    }
}

function Invoke-DtcReloadRecovery
{
    param(
        [string]$Reason = "unknown",
        [switch]$Force
    )

    if ($script:DtcReloadRecoveryAttempted)
    {
        Write-WarnLine "DTC /reload recovery already attempted once in this startup flow; skipping additional attempt"
        return [pscustomobject][ordered]@{
            Attempted = $false
            Recovered = $false
            SkippedReason = "already-attempted"
            Reason = $Reason
            Error = $null
            Signature = $null
            RecoveryProbe = $null
        }
    }

    $botStatusResp = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
    if ($botStatusResp.Success)
    {
        try
        {
            if ([bool]$botStatusResp.Result.IsActive)
            {
                Write-WarnLine "Skipping DTC /reload recovery because bot is already active"
                return [pscustomobject][ordered]@{
                    Attempted = $false
                    Recovered = $false
                    SkippedReason = "bot-active"
                    Reason = $Reason
                    Error = $null
                    Signature = $null
                    RecoveryProbe = $null
                }
            }
        }
        catch
        {
            # Ignore parse issues and continue with recovery attempt.
        }
    }

    $signature = Test-DtcFrozenStartupSignature -IgnoreStageRequirement:$Force.IsPresent
    if (-not $Force.IsPresent -and -not $signature.IsFrozen)
    {
        return [pscustomobject][ordered]@{
            Attempted = $false
            Recovered = $false
            SkippedReason = "signature-not-matched"
            Reason = $Reason
            Error = $null
            Signature = $signature
            RecoveryProbe = $null
        }
    }

    if ($Force.IsPresent -and -not $signature.IsFrozen)
    {
        Write-WarnLine "Forcing DTC /reload recovery despite non-startup signature (reason: $Reason)"
    }

    $script:DtcReloadRecoveryAttempted = $true

    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-before-health.json" -f $script:RunTag) -SafeResponse $signature.HealthResponse
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-before-frameconfig-status.json" -f $script:RunTag) -SafeResponse $signature.FrameStatusResponse
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-before-snapshot.json" -f $script:RunTag) -SafeResponse $signature.SnapshotResponse
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-before-frames.json" -f $script:RunTag) -SafeResponse $signature.FramesResponse
    $beforeDiag = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/diagnostics" -TimeoutSec 10
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-before-diagnostics.json" -f $script:RunTag) -SafeResponse $beforeDiag

    Write-WarnLine "Attempting one-time DTC /reload recovery ($Reason)"

    $reloadResp = Invoke-AgentApiSafe -Method POST -Path "/api/diagnostics/fix/reload" -Body @{} -TimeoutSec 20
    [void](Write-ArtifactJson -Name ("{0}-dtc-reload-result.json" -f $script:RunTag) -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Reason = $Reason
            Success = [bool]$reloadResp.Success
            Error = $reloadResp.Error
            Data = $reloadResp.Result
            Signature = $signature
        }))

    if (-not $reloadResp.Success)
    {
        return [pscustomobject][ordered]@{
            Attempted = $true
            Recovered = $false
            SkippedReason = $null
            Reason = $Reason
            Error = $reloadResp.Error
            Signature = $signature
            RecoveryProbe = $null
        }
    }

    Start-Sleep -Seconds 8
    $recovery = Wait-ForDtcRecovery -TimeoutSec 30

    if (-not $recovery.Success)
    {
        Write-WarnLine "API /reload did not restore DTC within 30s; attempting send-reload.ps1 fallback"
        $fallbackReload = Invoke-DtcReloadFallbackScript
        [void](Write-ArtifactJson -Name ("{0}-dtc-reload-fallback-result.json" -f $script:RunTag) -Object ([ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                Reason = $Reason
                Success = [bool]$fallbackReload.Success
                Error = $fallbackReload.Error
                ScriptPath = $fallbackReload.ScriptPath
                Output = $fallbackReload.Output
            }))

        if ($fallbackReload.Success)
        {
            Start-Sleep -Seconds 8
            $fallbackRecovery = Wait-ForDtcRecovery -TimeoutSec 30
            if ($fallbackRecovery.Success)
            {
                $recovery = $fallbackRecovery
            }
        }
    }

    $afterHealth = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
    $afterFrameStatus = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/status" -TimeoutSec 8
    $afterSnapshot = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 10
    $afterFrames = Invoke-AgentApiSafe -Method GET -Path "/api/test/frames" -TimeoutSec 10
    $afterDiag = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/diagnostics" -TimeoutSec 10

    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-after-health.json" -f $script:RunTag) -SafeResponse $afterHealth
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-after-frameconfig-status.json" -f $script:RunTag) -SafeResponse $afterFrameStatus
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-after-snapshot.json" -f $script:RunTag) -SafeResponse $afterSnapshot
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-after-frames.json" -f $script:RunTag) -SafeResponse $afterFrames
    Save-DtcRecoveryApiArtifact -Name ("{0}-dtc-freeze-after-diagnostics.json" -f $script:RunTag) -SafeResponse $afterDiag

    return [pscustomobject][ordered]@{
        Attempted = $true
        Recovered = [bool]$recovery.Success
        SkippedReason = $null
        Reason = $Reason
        Error = $null
        Signature = $signature
        RecoveryProbe = $recovery.Probe
    }
}

function Should-ForceBlazorRestart
{
    return $Action -in @("Start", "StartAndValidate", "Restart", "LiveSession")
}

function Stop-StaleServerProcesses
{
    Write-Info "Cleaning stale bot server processes"

    $killed = New-Object System.Collections.Generic.List[string]
    $namedProcesses = @("BlazorServer", "HeadlessServer", "PathingAPI", "AmeisenNavigationServer")
    foreach ($name in $namedProcesses)
    {
        Get-Process -Name $name -ErrorAction SilentlyContinue | ForEach-Object {
            Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue
            [void]$killed.Add("${name}:$($_.Id)")
        }
    }

    $markers = @("BlazorServer.dll", "HeadlessServer.dll", "PathingAPI.dll")
    $dotnetProcesses = @()
    try
    {
        $dotnetProcesses = Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue
    }
    catch
    {
        $dotnetProcesses = @()
    }

    foreach ($proc in $dotnetProcesses)
    {
        $cmd = "$($proc.CommandLine)"
        if ([string]::IsNullOrWhiteSpace($cmd))
        {
            continue
        }

        foreach ($marker in $markers)
        {
            if ($cmd.IndexOf($marker, [StringComparison]::OrdinalIgnoreCase) -ge 0)
            {
                Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
                [void]$killed.Add("dotnet:$($proc.ProcessId):$marker")
                break
            }
        }
    }

    if ($killed.Count -gt 0)
    {
        Write-WarnLine ("Killed stale processes: " + ($killed -join ", "))
    }
    else
    {
        Write-Ok "No stale bot server processes found"
    }
}

function Invoke-ReleaseBuild
{
    if ($SkipBuild)
    {
        Write-WarnLine "Skipping Release build due -SkipBuild override"
        return
    }

    Write-Info "Building solution in Release mode to deploy latest binaries"
    Ensure-SessionArtifactDir | Out-Null
    $buildLog = Join-Path $script:SessionArtifactDir ("{0}-build-release.log" -f $script:RunTag)

    Push-Location $BotRoot
    try
    {
        & dotnet build MasterOfPuppets.sln -c Release 2>&1 | Tee-Object -FilePath $buildLog
        if ($LASTEXITCODE -ne 0)
        {
            throw "Release build failed (exit $LASTEXITCODE). See $buildLog"
        }
    }
    finally
    {
        Pop-Location
    }

    Write-Ok "Release build completed"
}

function Start-NavigationServer
{
    Write-Info "Ensuring navigation server is online (port $NavigationPort)"

    if (Test-PortListening -Port $NavigationPort)
    {
        Write-Ok "Navigation server port already listening"
        return
    }

    $navExe = Resolve-NavigationExecutable
    $navDir = Split-Path -Parent $navExe
    $stdout = Join-Path $script:LogsDir "$script:RunTag-nav-stdout.log"
    $stderr = Join-Path $script:LogsDir "$script:RunTag-nav-stderr.log"

    Start-Process -FilePath $navExe -WorkingDirectory $navDir -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Minimized | Out-Null

    $started = Wait-Until -Label "Navigation server started" -TimeoutSec 20 -PollMs 500 -Condition {
        Test-PortListening -Port $NavigationPort
    }

    if (-not $started)
    {
        throw "Navigation server failed to start. See $stderr"
    }
}

function Stop-BlazorServer
{
    $proc = Get-Process -Name "BlazorServer" -ErrorAction SilentlyContinue
    if ($proc)
    {
        Write-Info "Stopping BlazorServer (PID $($proc.Id))"
        Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
    }

    Start-Sleep -Seconds 3
}

function Start-BlazorServer
{
    Write-Info "Ensuring BlazorServer is online"
    $forceRestart = Should-ForceBlazorRestart

    $healthOk = $false
    try
    {
        $h = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 2
        if ($null -ne $h)
        {
            $healthOk = $true
        }
    }
    catch
    {
        $healthOk = $false
    }

    if ($healthOk -and -not $forceRestart)
    {
        Write-Ok "BlazorServer API already reachable"
    }
    else
    {
        if ($healthOk -and $forceRestart)
        {
            Write-Info "Restarting BlazorServer to clear stale in-memory state"
        }

        $blazorExe = Resolve-BlazorExecutable
        $blazorDir = Split-Path -Parent $blazorExe

        $portPid = Get-ListeningPid -Port $WebPort
        if ($null -ne $portPid)
        {
            $portProc = Get-Process -Id $portPid -ErrorAction SilentlyContinue
            if ($null -ne $portProc -and $portProc.ProcessName -ne "BlazorServer")
            {
                throw "Port $WebPort is already in use by '$($portProc.ProcessName)' (PID $portPid)."
            }
        }

        Stop-BlazorServer

        $stdout = Join-Path $script:LogsDir "$script:RunTag-blazor-stdout.log"
        $stderr = Join-Path $script:LogsDir "$script:RunTag-blazor-stderr.log"

        Start-Process -FilePath $blazorExe -WorkingDirectory $blazorDir -RedirectStandardOutput $stdout -RedirectStandardError $stderr -WindowStyle Minimized | Out-Null

        $started = Wait-Until -Label "BlazorServer API is reachable" -TimeoutSec $StartupTimeoutSeconds -PollMs 1000 -Condition {
            try
            {
                $null -ne (Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 2)
            }
            catch
            {
                $false
            }
        }

        if (-not $started)
        {
            throw "BlazorServer failed to start. See $stderr"
        }
    }

    $ready = Wait-Until -Label "Startup stage Ready" -TimeoutSec $StartupTimeoutSeconds -PollMs 1500 -Condition {
        try
        {
            $health = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 3
            (Get-StartupStageValue -Health $health) -ge 9
        }
        catch
        {
            $false
        }
    }

    if (-not $ready)
    {
        $healthSnapshot = $null
        try
        {
            $healthSnapshot = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 5
        }
        catch
        {
            $healthSnapshot = $null
        }

        $stageValue = Get-StartupStageValue -Health $healthSnapshot
        if ($stageValue -eq 7)
        {
            $stage7StallThresholdSec = 240
            $startupElapsedSec = Get-StartupElapsedSeconds -Health $healthSnapshot
            if ($null -ne $startupElapsedSec -and $startupElapsedSec -lt $stage7StallThresholdSec)
            {
                $graceSec = $stage7StallThresholdSec - $startupElapsedSec
                Write-WarnLine "Startup still in frame configuration at ${startupElapsedSec}s; waiting additional ${graceSec}s before DTC recovery/auto-config intervention"

                $ready = Wait-Until -Label "Startup stage Ready (stage 7 grace window)" -TimeoutSec $graceSec -PollMs 1500 -Condition {
                    try
                    {
                        $health = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 3
                        (Get-StartupStageValue -Health $health) -ge 9
                    }
                    catch
                    {
                        $false
                    }
                }

                if ($ready)
                {
                    return
                }

                try
                {
                    $healthSnapshot = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 5
                }
                catch
                {
                    $healthSnapshot = $null
                }

                $stageValue = Get-StartupStageValue -Health $healthSnapshot
            }
        }

        if ($stageValue -eq 7)
        {
            Write-WarnLine "Startup stalled in frame configuration (stage 7); attempting /api/frameconfig/auto-configure"

            $startupDtcFreeze = Test-DtcFrozenStartupSignature
            if ($startupDtcFreeze.FrameStatusResponse.Success)
            {
                [void](Write-ArtifactJson -Name ("{0}-frameconfig-status-precheck.json" -f $script:RunTag) -Object $startupDtcFreeze.FrameStatusResponse.Result)
            }

            if ($startupDtcFreeze.SnapshotResponse.Success -and
                $null -ne $startupDtcFreeze.SnapshotResponse.Result -and
                $startupDtcFreeze.SnapshotResponse.Result.Success)
            {
                [void](Write-ArtifactJson -Name ("{0}-frameconfig-snapshot-precheck.json" -f $script:RunTag) -Object $startupDtcFreeze.SnapshotResponse.Result)
            }

            if ($startupDtcFreeze.IsFrozen)
            {
                Write-WarnLine "Detected frozen DataToColor signature during stage 7; attempting one-time /reload recovery before failing"

                $dtcRecovery = Invoke-DtcReloadRecovery -Reason "startup-stage7-frameconfig"
                if (-not $dtcRecovery.Recovered)
                {
                    throw "DTC remained frozen after auto /reload; verify character is in-world and addon UI/pixels are visible."
                }
            }

            try
            {
                $frameStatus = Invoke-AgentApiSafe -Method GET -Path "/api/frameconfig/status" -TimeoutSec 8
                if ($frameStatus.Success)
                {
                    [void](Write-ArtifactJson -Name ("{0}-frameconfig-status-before.json" -f $script:RunTag) -Object $frameStatus.Result)
                }

                $autoCfg = Invoke-AgentApiSafe -Method POST -Path "/api/frameconfig/auto-configure" -Body @{} -TimeoutSec 180
                [void](Write-ArtifactJson -Name ("{0}-frameconfig-auto-configure.json" -f $script:RunTag) -Object ([ordered]@{
                        Success = $autoCfg.Success
                        Error = $autoCfg.Error
                        Data = $autoCfg.Result
                        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                    }))

                if (-not $autoCfg.Success)
                {
                    throw "Frame auto-config API call failed: $($autoCfg.Error)"
                }

                $ready = Wait-Until -Label "Startup stage Ready (after frame auto-config)" -TimeoutSec $StartupTimeoutSeconds -PollMs 1500 -Condition {
                    try
                    {
                        $health = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 3
                        (Get-StartupStageValue -Health $health) -ge 9
                    }
                    catch
                    {
                        $false
                    }
                }
            }
            catch
            {
                Write-WarnLine "Frame auto-config recovery attempt failed: $($_.Exception.Message)"
            }
        }
    }

    if (-not $ready)
    {
        throw "BlazorServer never reached StartupStage.Ready."
    }
}

function Get-LaunchCheck
{
    param(
        [Parameter(Mandatory = $true)][object]$Launch,
        [Parameter(Mandatory = $true)][string]$Title
    )

    if ($null -eq $Launch -or $null -eq $Launch.Checks)
    {
        return $null
    }

    return $Launch.Checks | Where-Object { $_.Title -eq $Title } | Select-Object -First 1
}

function Get-LaunchStatusCode([object]$Check)
{
    if ($null -eq $Check)
    {
        return -1
    }

    if ($null -eq $Check.Status)
    {
        return -1
    }

    $statusValue = $Check.Status
    if ($statusValue -is [System.IConvertible] -and $statusValue -isnot [string])
    {
        try
        {
            return [int]$statusValue
        }
        catch
        {
            # Fall through to text parsing.
        }
    }

    $parsedNumericStatus = 0
    $statusText = "$statusValue"
    if ([int]::TryParse($statusText, [ref]$parsedNumericStatus))
    {
        return $parsedNumericStatus
    }

    switch ($statusText)
    {
        "Pending" { return 1 }
        "Ok" { return 2 }
        "Warning" { return 3 }
        "Error" { return 4 }
        "Skipped" { return 5 }
        default { return 0 }
    }
}

function Get-LaunchNavigationAssessment
{
    param([AllowNull()][object]$Launch)

    $check = Get-LaunchCheck -Launch $Launch -Title "Navigation"
    if ($null -eq $check)
    {
        $portListening = Test-PortListening -Port $NavigationPort
        return [pscustomobject]@{
            Present = $false
            Source = "port-check"
            Status = $(if ($portListening) { "Ok" } else { "Unavailable" })
            StatusCode = $(if ($portListening) { 2 } else { -1 })
            Message = $(if ($portListening) { "Navigation port listening" } else { "Navigation port $NavigationPort is not listening" })
            IsBlocking = (-not $portListening)
            IsOk = $portListening
            BlockingReason = $(if ($portListening) { $null } else { "navigation port $NavigationPort is not listening" })
        }
    }

    $statusCode = Get-LaunchStatusCode -Check $check
    $statusText = if ($null -ne $check.Status) { "$($check.Status)" } else { "$statusCode" }
    $message = if ($null -ne $check.Message) { "$($check.Message)" } else { "" }
    $isBlocking = [bool](Get-OptionalPropertyValue -Object $check -Name "IsBlocking")
    $connectedHybrid = $message.IndexOf("RemoteV3 connected", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    $isOk = ($statusCode -eq 2) -or $connectedHybrid
    $blockingReason = $null
    if (-not $isOk)
    {
        $blockingReason = if (-not [string]::IsNullOrWhiteSpace($message))
        {
            $message
        }
        else
        {
            "launch navigation check status=$statusText"
        }
    }

    return [pscustomobject]@{
        Present = $true
        Source = "launch-check"
        Status = $statusText
        StatusCode = $statusCode
        Message = $message
        IsBlocking = ([bool]$isBlocking -or -not $isOk)
        IsOk = $isOk
        BlockingReason = $blockingReason
    }
}

function Get-OptionalPropertyValue
{
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $Object)
    {
        return $null
    }

    try
    {
        $prop = $Object.PSObject.Properties[$Name]
        if ($null -eq $prop)
        {
            return $null
        }

        return $prop.Value
    }
    catch
    {
        return $null
    }
}

function Get-LaunchActionBarBypassOverrideInfo
{
    param([AllowNull()][object]$Launch)

    $result = [ordered]@{
        Enabled = $false
        Reason = $null
        Source = $null
    }

    $overrides = Get-OptionalPropertyValue -Object $Launch -Name "Overrides"
    if ($null -eq $overrides)
    {
        return [pscustomobject]$result
    }

    $bypassContainer = Get-OptionalPropertyValue -Object $overrides -Name "Bypass"
    if ($null -eq $bypassContainer)
    {
        $bypassContainer = Get-OptionalPropertyValue -Object $overrides -Name "Bypasses"
    }

    if ($null -eq $bypassContainer)
    {
        return [pscustomobject]$result
    }

    $actionBarNode = Get-OptionalPropertyValue -Object $bypassContainer -Name "ActionBar"
    if ($null -eq $actionBarNode)
    {
        return [pscustomobject]$result
    }

    if ($actionBarNode -is [bool] -or ($actionBarNode -is [System.IConvertible] -and $actionBarNode -isnot [string]))
    {
        $result.Enabled = [bool]$actionBarNode
        $result.Reason = [string](Get-OptionalPropertyValue -Object $overrides -Name "Reason")
        $result.Source = [string](Get-OptionalPropertyValue -Object $overrides -Name "Source")
        return [pscustomobject]$result
    }

    $enabledValue = Get-OptionalPropertyValue -Object $actionBarNode -Name "Enabled"
    if ($null -eq $enabledValue)
    {
        $enabledValue = Get-OptionalPropertyValue -Object $actionBarNode -Name "IsEnabled"
    }

    if ($null -ne $enabledValue)
    {
        $result.Enabled = [bool]$enabledValue
    }
    else
    {
        try { $result.Enabled = [bool]$actionBarNode } catch { $result.Enabled = $false }
    }

    $reason = Get-OptionalPropertyValue -Object $actionBarNode -Name "Reason"
    if ($null -eq $reason)
    {
        $reason = Get-OptionalPropertyValue -Object $overrides -Name "Reason"
    }

    $source = Get-OptionalPropertyValue -Object $actionBarNode -Name "Source"
    if ($null -eq $source)
    {
        $source = Get-OptionalPropertyValue -Object $overrides -Name "Source"
    }

    $result.Reason = if ($null -ne $reason) { "$reason" } else { $null }
    $result.Source = if ($null -ne $source) { "$source" } else { $null }
    return [pscustomobject]$result
}

function Get-LaunchKeyBindingsBypassOverrideInfo
{
    param([AllowNull()][object]$Launch)

    $result = [ordered]@{
        Enabled = $false
        Reason = $null
        Source = $null
    }

    $overrides = Get-OptionalPropertyValue -Object $Launch -Name "Overrides"
    if ($null -eq $overrides)
    {
        return [pscustomobject]$result
    }

    $bypassContainer = Get-OptionalPropertyValue -Object $overrides -Name "Bypass"
    if ($null -eq $bypassContainer)
    {
        $bypassContainer = Get-OptionalPropertyValue -Object $overrides -Name "Bypasses"
    }

    if ($null -eq $bypassContainer)
    {
        return [pscustomobject]$result
    }

    $node = Get-OptionalPropertyValue -Object $bypassContainer -Name "KeyBindings"
    if ($null -eq $node)
    {
        return [pscustomobject]$result
    }

    if ($node -is [bool] -or ($node -is [System.IConvertible] -and $node -isnot [string]))
    {
        $result.Enabled = [bool]$node
        $result.Reason = [string](Get-OptionalPropertyValue -Object $overrides -Name "Reason")
        $result.Source = [string](Get-OptionalPropertyValue -Object $overrides -Name "Source")
        return [pscustomobject]$result
    }

    $enabledValue = Get-OptionalPropertyValue -Object $node -Name "Enabled"
    if ($null -eq $enabledValue)
    {
        $enabledValue = Get-OptionalPropertyValue -Object $node -Name "IsEnabled"
    }

    if ($null -ne $enabledValue)
    {
        $result.Enabled = [bool]$enabledValue
    }
    else
    {
        try { $result.Enabled = [bool]$node } catch { $result.Enabled = $false }
    }

    $reason = Get-OptionalPropertyValue -Object $node -Name "Reason"
    if ($null -eq $reason)
    {
        $reason = Get-OptionalPropertyValue -Object $overrides -Name "Reason"
    }

    $source = Get-OptionalPropertyValue -Object $node -Name "Source"
    if ($null -eq $source)
    {
        $source = Get-OptionalPropertyValue -Object $overrides -Name "Source"
    }

    $result.Reason = if ($null -ne $reason) { "$reason" } else { $null }
    $result.Source = if ($null -ne $source) { "$source" } else { $null }
    return [pscustomobject]$result
}

function Set-LaunchOverrides
{
    Write-Info "Applying launch overrides"

    $body = @{
        AllowStartWithWarnings = [bool]$AllowStartWithWarnings
        EmergencyBypassAll = $false
        Bypass = @{
            Route = $false
            ActionBar = [bool]$BypassActionBar
            KeyBindings = [bool]$BypassKeyBindings
        }
        Reason = "agent-cli-startup"
        Source = "Agent-BotControl"
    }

    $null = Invoke-AgentApi -Method POST -Path "/api/launch/overrides" -Body $body
    Write-Ok "Launch overrides applied"
}

function Set-ActionBarBypassOverride
{
    param(
        [Parameter(Mandatory = $true)][bool]$Enabled,
        [string]$Reason = "agent-cli-dynamic-actionbar"
    )

    $existingKeyBypass = [bool]$BypassKeyBindings
    try
    {
        $launchResp = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if ($launchResp.Success -and $null -ne $launchResp.Result)
        {
            $keyBypassInfo = Get-LaunchKeyBindingsBypassOverrideInfo -Launch $launchResp.Result
            $existingKeyBypass = [bool]$keyBypassInfo.Enabled
        }
    }
    catch { }

    $body = @{
        AllowStartWithWarnings = [bool]$AllowStartWithWarnings
        EmergencyBypassAll = $false
        Bypass = @{
            Route = $false
            ActionBar = $Enabled
            KeyBindings = $existingKeyBypass
        }
        Reason = $Reason
        Source = "Agent-BotControl"
    }

    $null = Invoke-AgentApi -Method POST -Path "/api/launch/overrides" -Body $body
}

function Set-KeyBindingsBypassOverride
{
    param(
        [Parameter(Mandatory = $true)][bool]$Enabled,
        [string]$Reason = "agent-cli-dynamic-keybindings"
    )

    $existingActionBarBypass = [bool]$BypassActionBar
    try
    {
        $launchResp = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if ($launchResp.Success -and $null -ne $launchResp.Result)
        {
            $actionBarBypassInfo = Get-LaunchActionBarBypassOverrideInfo -Launch $launchResp.Result
            $existingActionBarBypass = [bool]$actionBarBypassInfo.Enabled
        }
    }
    catch { }

    $body = @{
        AllowStartWithWarnings = [bool]$AllowStartWithWarnings
        EmergencyBypassAll = $false
        Bypass = @{
            Route = $false
            ActionBar = $existingActionBarBypass
            KeyBindings = $Enabled
        }
        Reason = $Reason
        Source = "Agent-BotControl"
    }

    $null = Invoke-AgentApi -Method POST -Path "/api/launch/overrides" -Body $body
}

function Clear-TransientActionBarBypassIfHealthy
{
    param(
        [AllowNull()][object]$Launch = $null
    )

    if ($BypassActionBar)
    {
        return $false
    }

    if ($null -eq $Launch)
    {
        $launchResp = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if (-not $launchResp.Success -or $null -eq $launchResp.Result)
        {
            return $false
        }

        $Launch = $launchResp.Result
    }

    $abBypassInfo = Get-LaunchActionBarBypassOverrideInfo -Launch $Launch
    if ($null -eq $abBypassInfo -or -not [bool]$abBypassInfo.Enabled)
    {
        return $false
    }

    if ("$($abBypassInfo.Reason)" -notlike "agent-cli-*")
    {
        return $false
    }

    $actionCheck = Get-LaunchCheck -Launch $Launch -Title "Action Bar"
    $actionStatus = Get-LaunchStatusCode -Check $actionCheck
    if ($actionStatus -ne 2)
    {
        return $false
    }

    Set-ActionBarBypassOverride -Enabled $false -Reason "agent-cli-clear-actionbar-bypass"
    Write-Info "Cleared transient Action Bar bypass override after launch checks recovered"
    return $true
}

function Clear-TransientKeyBindingsBypassIfHealthy
{
    param(
        [AllowNull()][object]$Launch = $null
    )

    if ($BypassKeyBindings)
    {
        return $false
    }

    if ($null -eq $Launch)
    {
        $launchResp = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if (-not $launchResp.Success -or $null -eq $launchResp.Result)
        {
            return $false
        }

        $Launch = $launchResp.Result
    }

    $kbBypassInfo = Get-LaunchKeyBindingsBypassOverrideInfo -Launch $Launch
    if ($null -eq $kbBypassInfo -or -not [bool]$kbBypassInfo.Enabled)
    {
        return $false
    }

    if ("$($kbBypassInfo.Reason)" -notlike "agent-cli-*")
    {
        return $false
    }

    $keyCheck = Get-LaunchCheck -Launch $Launch -Title "Key Bindings"
    $keyStatus = Get-LaunchStatusCode -Check $keyCheck
    if ($keyStatus -ne 2)
    {
        return $false
    }

    Set-KeyBindingsBypassOverride -Enabled $false -Reason "agent-cli-clear-keybindings-bypass"
    Write-Info "Cleared transient Key Bindings bypass override after launch checks recovered"
    return $true
}

function Try-CloseChatInputFromAutomation
{
    param(
        [string]$Context = "readiness",
        [int]$Attempts = 2
    )

    if ($Attempts -lt 1)
    {
        $Attempts = 1
    }

    for ($i = 1; $i -le $Attempts; $i++)
    {
        $snapshot = Get-CurrentSnapshot
        if ($null -eq $snapshot -or -not [bool]$snapshot.ChatInputVisible)
        {
            return $true
        }

        Write-WarnLine "Chat input is visible during $Context; attempting automated close (attempt $i/$Attempts)"

        try
        {
            $null = Invoke-AgentApiSafe -Method POST -Path "/api/diagnostics/fix/initstate" -Body @{} -TimeoutSec 15
        }
        catch { }

        Start-Sleep -Milliseconds 500

        $snapshot = Get-CurrentSnapshot
        if ($null -ne $snapshot -and -not [bool]$snapshot.ChatInputVisible)
        {
            Write-Ok "Closed chat input automatically during $Context"
            return $true
        }

        [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcflush" -VerifyKind "none" -Retries 1 -BestEffort)
        Start-Sleep -Milliseconds 500

        $snapshot = Get-CurrentSnapshot
        if ($null -ne $snapshot -and -not [bool]$snapshot.ChatInputVisible)
        {
            Write-Ok "Closed chat input automatically during $Context (slash fallback)"
            return $true
        }
    }

    return $false
}

function Try-ResolveBenignActionBarBlocker
{
    try
    {
        $diag = Invoke-AgentApi -Method GET -Path "/api/diagnostics/actionbar" -TimeoutSec 8
        if ($null -eq $diag -or [int]$diag.IssueCount -ne 1 -or $null -eq $diag.Issues -or $diag.Issues.Count -ne 1)
        {
            return $false
        }

        $issue = $diag.Issues[0]
        $spellName = "$($issue.SpellName)"
        $status = "$($issue.Status)"
        $canResolve = [bool]$issue.CanResolve
        $slot = [int]$issue.Slot

        if (-not $canResolve -or $status -ne "EmptySlot" -or $spellName -ne "Stealth")
        {
            return $false
        }

        Write-WarnLine "Detected persistent benign Action Bar blocker ($spellName slot $slot empty); attempting targeted place"

        try
        {
            $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/place" -TimeoutSec 15 -Body @{
                slot = $slot
                name = $spellName
            }
            Start-Sleep -Seconds 1
        }
        catch
        {
            Write-WarnLine "Targeted fix/place failed for $spellName slot ${slot}: $($_.Exception.Message)"
        }

        $recheck = Invoke-AgentApi -Method GET -Path "/api/diagnostics/actionbar" -TimeoutSec 8
        if ($null -ne $recheck -and [int]$recheck.IssueCount -eq 0)
        {
            Write-Ok "Action Bar issue resolved by targeted placement"
            return $true
        }

        Write-WarnLine "Applying Action Bar bypass override for persistent '$spellName' empty-slot blocker"
        Set-ActionBarBypassOverride -Enabled $true -Reason "agent-cli-stealth-slot-bypass"
        return $true
    }
    catch
    {
        Write-WarnLine "Action Bar blocker analysis failed: $($_.Exception.Message)"
        return $false
    }
}

function Try-ResolveStaleKeyBindingsBlocker
{
    try
    {
        $diag = Invoke-AgentApi -Method GET -Path "/api/diagnostics/keybindings" -TimeoutSec 8
        if ($null -eq $diag)
        {
            return $false
        }

        $total = [int]$diag.TotalBindings
        $mismatch = [int]$diag.MismatchCount
        $initialized = [bool]$diag.IsInitialized

        if ($initialized -and $total -gt 0 -and $mismatch -eq 0)
        {
            Write-WarnLine "Launch check reports stale Key Bindings timeout, but diagnostics are healthy; applying transient Key Bindings bypass override"
            Set-KeyBindingsBypassOverride -Enabled $true -Reason "agent-cli-keybindings-stale-timeout-bypass"
            return $true
        }
    }
    catch
    {
        Write-WarnLine "Key Bindings stale-blocker analysis failed: $($_.Exception.Message)"
    }

    return $false
}

function Invoke-ResolvableActionBarPlacements
{
    try
    {
        $diag = Invoke-AgentApi -Method GET -Path "/api/diagnostics/actionbar" -TimeoutSec 8
        if ($null -eq $diag -or $null -eq $diag.Issues)
        {
            return 0
        }

        $changes = 0
        foreach ($issue in @($diag.Issues))
        {
            try
            {
                if (-not [bool]$issue.CanResolve)
                {
                    continue
                }

                $slot = [int]$issue.Slot
                $spellName = "$($issue.SpellName)"
                if ([string]::IsNullOrWhiteSpace($spellName))
                {
                    continue
                }

                Write-Info "Attempting fix/place for action bar issue: slot=$slot spell=$spellName"
                $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/place" -TimeoutSec 15 -Body @{
                    slot = $slot
                    name = $spellName
                }
                $changes++
                Start-Sleep -Milliseconds 250
            }
            catch
            {
                Write-WarnLine "fix/place failed for slot $($issue.Slot) '$($issue.SpellName)': $($_.Exception.Message)"
            }
        }

        return $changes
    }
    catch
    {
        Write-WarnLine "Could not enumerate action bar issues for fix/place: $($_.Exception.Message)"
        return 0
    }
}

function Test-KeybindingsReady
{
    $resp = Get-KeybindingDiagnosticsSafe
    if (-not $resp.Success -or $null -eq $resp.Result)
    {
        return $false
    }

    return [bool]$resp.Result.IsInitialized -and ([int]$resp.Result.TotalBindings -gt 0)
}

function Test-ActionBarTexturesReady
{
    $resp = Get-ActionBarDiagnosticsSafe
    if (-not $resp.Success -or $null -eq $resp.Result)
    {
        return $false
    }

    return [bool]$resp.Result.IsTextureInitialized
}

function Test-ActionBarIssuesClear
{
    $resp = Get-ActionBarDiagnosticsSafe
    if (-not $resp.Success -or $null -eq $resp.Result)
    {
        return $false
    }

    return ([int]$resp.Result.IssueCount -eq 0)
}

function Load-BotProfile
{
    Write-Info "Loading profile '$Profile'"
    $res = Invoke-AgentApi -Method POST -Path "/api/bot/profile/load" -Body @{ FileName = $Profile }
    if ($null -eq $res -or -not $res.IsLoaded)
    {
        throw "Profile load failed for '$Profile'"
    }

    Write-Ok "Profile loaded ($($res.FileName))"
}

function Invoke-ReadinessFixes
{
    $snapshot = Get-CurrentSnapshot
    if ($null -ne $snapshot -and $snapshot.ChatInputVisible)
    {
        if (-not (Try-CloseChatInputFromAutomation -Context "startup fixes"))
        {
            throw "Chat input is currently open in WoW and could not be closed automatically. Close chat (Escape) and retry."
        }
    }

    Write-Info "Applying startup fixes (effect-verified readiness repair)"
    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/initstate" -Body @{} -TimeoutSec 30
    }
    catch
    {
        Write-WarnLine "fix/initstate did not complete: $($_.Exception.Message)"
    }

    Start-Sleep -Milliseconds 800

    [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcflush" -VerifyKind "dcflush" -Retries $MaxCommandRetries -BestEffort)

    $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
    $keyBlocked = $false
    $actionBlocked = $false
    $handshakeGlobalTimeZero = $false
    if ($launch.Success -and $null -ne $launch.Result)
    {
        $keyCheck = Get-LaunchCheck -Launch $launch.Result -Title "Key Bindings"
        $actionCheck = Get-LaunchCheck -Launch $launch.Result -Title "Action Bar"
        $handshakeCheck = Get-LaunchCheck -Launch $launch.Result -Title "Addon Handshake"
        $keyBlocked = ($null -ne $keyCheck) -and [bool]$keyCheck.IsBlocking
        $actionBlocked = ($null -ne $actionCheck) -and [bool]$actionCheck.IsBlocking
        if ($null -ne $handshakeCheck)
        {
            $handshakeGlobalTimeZero = "$($handshakeCheck.Message)" -like "*GlobalTime=0*"
        }
    }

    if ($keyBlocked -or -not (Test-KeybindingsReady))
    {
        try
        {
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcbindings" -VerifyKind "bindings" -Retries $MaxCommandRetries)
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcnumberkeys" -VerifyKind "dcnumberkeys" -Retries $MaxCommandRetries -BestEffort)
        }
        catch
        {
            Write-WarnLine "Keybinding repair did not verify on primary pass: $($_.Exception.Message)"
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dc" -VerifyKind "none" -Retries 1 -BestEffort)
            if ($handshakeGlobalTimeZero)
            {
                Write-WarnLine "Addon handshake is stale (GlobalTime=0); running keybinding fixes as best-effort pending handshake recovery"
            }
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcbindings" -VerifyKind "bindings" -Retries 1 -BestEffort)
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcnumberkeys" -VerifyKind "dcnumberkeys" -Retries 1 -BestEffort)
        }
    }

    if ($actionBlocked -or -not (Test-ActionBarTexturesReady))
    {
        try
        {
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcactions" -VerifyKind "actions" -Retries $MaxCommandRetries)
        }
        catch
        {
            Write-WarnLine "Action bar texture repair did not verify on primary pass: $($_.Exception.Message)"
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dc" -VerifyKind "none" -Retries 1 -BestEffort)
            if ($handshakeGlobalTimeZero)
            {
                Write-WarnLine "Addon handshake is stale (GlobalTime=0); running action bar fixes as best-effort pending handshake recovery"
            }
            [void](Invoke-VerifiedSlashCommand -SlashCommand "/dcactions" -VerifyKind "actions" -Retries 1 -BestEffort)
        }
    }

    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/syncbar" -Body @{} -TimeoutSec 30
        Write-Info "Applied fix/syncbar"
    }
    catch
    {
        Write-WarnLine "fix/syncbar failed: $($_.Exception.Message)"
    }

    $placed = Invoke-ResolvableActionBarPlacements
    if ($placed -gt 0)
    {
        Write-Info "Applied $placed targeted action bar placement fix(es)"
    }

    if (-not (Test-ActionBarIssuesClear))
    {
        [void](Try-ResolveBenignActionBarBlocker)
    }

    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/all" -Body @{} -TimeoutSec 60
        Write-Info "Executed fix/all as final catch-all"
    }
    catch
    {
        Write-WarnLine "fix/all did not complete: $($_.Exception.Message)"
    }

    $keyReady = Test-KeybindingsReady
    $actionTexturesReady = Test-ActionBarTexturesReady
    $actionIssuesClear = Test-ActionBarIssuesClear

    if (-not $BypassActionBar -and $actionIssuesClear)
    {
        try
        {
            $launchPostFix = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
            if ($launchPostFix.Success -and $null -ne $launchPostFix.Result)
            {
                $abBypassInfo = Get-LaunchActionBarBypassOverrideInfo -Launch $launchPostFix.Result
                if ($null -ne $abBypassInfo -and [bool]$abBypassInfo.Enabled -and "$($abBypassInfo.Reason)" -like "agent-cli-*")
                {
                    Set-ActionBarBypassOverride -Enabled $false -Reason "agent-cli-clear-actionbar-bypass"
                    Write-Info "Cleared transient Action Bar bypass override after successful repair"
                }
            }
        }
        catch
        {
            Write-WarnLine "Could not clear transient Action Bar bypass override: $($_.Exception.Message)"
        }
    }

    Write-Ok "Readiness repair sequence complete (Bindings=$keyReady, ActionTextures=$actionTexturesReady, ActionIssuesClear=$actionIssuesClear)"
}

function Wait-ForReadiness
{
    Write-Info "Waiting for launch readiness checks"

    $deadline = (Get-Date).AddSeconds($ReadinessTimeoutSeconds)
    $attempt = 0
    $lastBlocking = @()
    $actionBarBypassApplied = $false
    $keyBindingsBypassApplied = $false
    $dtcHandshakeReloadAttempted = $false
    $webSeenListening = $false
    $navSeenListening = $false

    while ((Get-Date) -lt $deadline)
    {
        $attempt++

        $webListening = Test-PortListening -Port $WebPort
        $navListening = Test-PortListening -Port $NavigationPort
        if ($webListening)
        {
            $webSeenListening = $true
        }
        if ($navListening)
        {
            $navSeenListening = $true
        }

        if (-not $webListening -or -not $navListening)
        {
            [void](Write-ArtifactJson -Name ("{0}-readiness-listener-state-attempt-{1}.json" -f $script:RunTag, $attempt) -Object ([ordered]@{
                    TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                    Attempt = $attempt
                    WebPort = $WebPort
                    WebListening = $webListening
                    NavigationPort = $NavigationPort
                    NavigationListening = $navListening
                    WebSeenListening = $webSeenListening
                    NavigationSeenListening = $navSeenListening
                }))

            $dropDetected = ($webSeenListening -and -not $webListening) -or ($navSeenListening -and -not $navListening)
            if ($dropDetected)
            {
                throw ("Launch readiness failed: web/nav listener dropped during readiness (attempt {0}, web={1}, nav={2})" -f $attempt, $webListening, $navListening)
            }

            Write-WarnLine ("Readiness check detected listener offline (attempt {0}, web={1}, nav={2}); reasserting services" -f $attempt, $webListening, $navListening)
            if (-not $webListening)
            {
                Start-BlazorServer
            }
            if (-not $navListening)
            {
                Start-NavigationServer
            }

            $webAfterReassert = Test-PortListening -Port $WebPort
            $navAfterReassert = Test-PortListening -Port $NavigationPort
            [void](Write-ArtifactJson -Name ("{0}-readiness-listener-reassert-attempt-{1}.json" -f $script:RunTag, $attempt) -Object ([ordered]@{
                    TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                    Attempt = $attempt
                    WebListeningAfterReassert = $webAfterReassert
                    NavigationListeningAfterReassert = $navAfterReassert
                }))

            if (-not $webAfterReassert -or -not $navAfterReassert)
            {
                throw ("Launch readiness failed: web/nav listener unavailable during readiness (attempt {0}, web={1}, nav={2})" -f $attempt, $webAfterReassert, $navAfterReassert)
            }

            $webSeenListening = $true
            $navSeenListening = $true
        }

        $launch = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 8
    if ($launch.CanStartBot)
    {
        [void](Clear-TransientKeyBindingsBypassIfHealthy -Launch $launch)
        [void](Clear-TransientActionBarBypassIfHealthy -Launch $launch)
        Write-Ok "Launch readiness satisfied"
        return $launch
    }

        $blocking = @($launch.Checks | Where-Object { $_.IsBlocking })
        $lastBlocking = @($blocking | ForEach-Object { "$($_.Title): $($_.Message)" })

        $keyCheck = Get-LaunchCheck -Launch $launch -Title "Key Bindings"
        $actionCheck = Get-LaunchCheck -Launch $launch -Title "Action Bar"
        $handshakeCheck = Get-LaunchCheck -Launch $launch -Title "Addon Handshake"
        $navCheck = Get-LaunchCheck -Launch $launch -Title "Navigation"

        $keyStatus = Get-LaunchStatusCode -Check $keyCheck
        $actionStatus = Get-LaunchStatusCode -Check $actionCheck
        $handshakeStatus = Get-LaunchStatusCode -Check $handshakeCheck
        $navStatus = Get-LaunchStatusCode -Check $navCheck
        $handshakeMessage = $(if ($null -ne $handshakeCheck -and $null -ne $handshakeCheck.Message) { "$($handshakeCheck.Message)" } else { "" })
        $handshakeGlobalTimeZero = (($handshakeStatus -eq 1) -or ($handshakeStatus -eq 4)) -and ($handshakeMessage -like "*GlobalTime=0*")

        if ($navStatus -eq 4 -and -not (Test-PortListening -Port $NavigationPort))
        {
            Write-WarnLine "Navigation check is failing and server port is down, restarting nav server"
            Start-NavigationServer
        }

        if ($handshakeStatus -eq 4 -or $keyStatus -eq 1 -or $actionStatus -eq 4 -or $handshakeGlobalTimeZero)
        {
            $snapshot = Get-CurrentSnapshot
            if ($null -ne $snapshot -and $snapshot.ChatInputVisible)
            {
                if (-not (Try-CloseChatInputFromAutomation -Context "readiness wait"))
                {
                    throw "Chat input became visible while waiting for readiness and could not be closed automatically. Close chat (Escape) and rerun StartAndValidate."
                }

                Start-Sleep -Milliseconds 500
                continue
            }

            Write-WarnLine "Readiness not complete (attempt $attempt); reapplying startup fixes"
            Invoke-ReadinessFixes

            if ($actionStatus -eq 4 -and -not $actionBarBypassApplied)
            {
                if (Try-ResolveBenignActionBarBlocker)
                {
                    $actionBarBypassApplied = $true
                }
            }

            if ($keyStatus -eq 4 -and -not $keyBindingsBypassApplied)
            {
                if (Try-ResolveStaleKeyBindingsBlocker)
                {
                    $keyBindingsBypassApplied = $true
                }
            }

            if ($handshakeGlobalTimeZero -and -not $dtcHandshakeReloadAttempted)
            {
                Write-WarnLine "Addon handshake appears frozen (GlobalTime=0); attempting one-time /reload recovery"
                $dtcRecovery = Invoke-DtcReloadRecovery -Reason "readiness-handshake-globaltime0" -Force
                $dtcHandshakeReloadAttempted = $true

                if ($dtcRecovery.Attempted -and $dtcRecovery.Recovered)
                {
                    Write-Info "DTC /reload recovery succeeded during readiness; reapplying startup fixes"
                    Invoke-ReadinessFixes
                }
                elseif ($dtcRecovery.Attempted)
                {
                    Write-WarnLine "DTC /reload recovery did not restore addon handshake during readiness"
                }
                elseif ($null -ne $dtcRecovery.SkippedReason)
                {
                    Write-WarnLine "Skipped DTC /reload readiness recovery: $($dtcRecovery.SkippedReason)"
                }
            }
        }

        Start-Sleep -Seconds 3
    }

    $message = if (@($lastBlocking).Count -gt 0) { @($lastBlocking) -join "; " } else { "Unknown readiness blockers" }
    throw "Launch readiness timeout after ${ReadinessTimeoutSeconds}s: $message"
}

function Get-CurrentSnapshot
{
    try
    {
        $snapshotResult = Invoke-AgentApi -Method GET -Path "/api/test/snapshot" -TimeoutSec 10
        if ($null -eq $snapshotResult -or -not $snapshotResult.Success)
        {
            return $null
        }

        return $snapshotResult.Data.snapshot
    }
    catch
    {
        return $null
    }
}

function Test-CharacterAlignmentSnapshot
{
    param([AllowNull()][object]$Snapshot)

    $issues = New-Object System.Collections.Generic.List[string]
    $isHardFail = $false

    if ($null -eq $Snapshot)
    {
        [void]$issues.Add("Player snapshot unavailable.")
        return [pscustomobject]@{
            IsValid = $false
            IsHardFail = $false
            IsDead = $false
            Issues = @($issues)
        }
    }

    if ([bool]$Snapshot.ChatInputVisible)
    {
        [void]$issues.Add("Chat input is open (press Escape first to avoid typing automation keys into chat).")
        $isHardFail = $true
    }

    if ([bool]$Snapshot.Swimming)
    {
        [void]$issues.Add("Character is swimming (likely ocean/off-route position).")
        $isHardFail = $true
    }

    if (($Snapshot.MapX -le 0) -or ($Snapshot.MapX -gt 100) -or ($Snapshot.MapY -le 0) -or ($Snapshot.MapY -gt 100))
    {
        [void]$issues.Add("Character map position appears out-of-bounds (MapX=$($Snapshot.MapX), MapY=$($Snapshot.MapY)).")
    }

    return [pscustomobject]@{
        IsValid = ($issues.Count -eq 0)
        IsHardFail = $isHardFail
        IsDead = [bool]$Snapshot.Dead
        Issues = @($issues)
    }
}

function Assert-CharacterAlignment
{
    if ($SkipCharacterGate)
    {
        Write-WarnLine "Character alignment gate skipped by flag"
        return
    }

    Write-Info "Checking active character alignment"

    $maxAttempts = 12
    $retryDelayMs = 1000
    $requiredStableValidSamples = 2
    $stableValidSamples = 0
    $finalSnapshot = $null
    $lastAssessment = $null
    $samples = New-Object System.Collections.Generic.List[object]

    for ($attempt = 1; $attempt -le $maxAttempts; $attempt++)
    {
        $snap = Get-CurrentSnapshot
        $assessment = Test-CharacterAlignmentSnapshot -Snapshot $snap
        $lastAssessment = $assessment

        [void]$samples.Add([ordered]@{
            Attempt = $attempt
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            SnapshotAvailable = ($null -ne $snap)
            UIMapId = $(if ($null -ne $snap) { $snap.UIMapId } else { $null })
            MapX = $(if ($null -ne $snap) { $snap.MapX } else { $null })
            MapY = $(if ($null -ne $snap) { $snap.MapY } else { $null })
            Dead = $(if ($null -ne $snap) { [bool]$snap.Dead } else { $false })
            Swimming = $(if ($null -ne $snap) { [bool]$snap.Swimming } else { $false })
            ChatInputVisible = $(if ($null -ne $snap) { [bool]$snap.ChatInputVisible } else { $false })
            IsValid = [bool]$assessment.IsValid
            IsHardFail = [bool]$assessment.IsHardFail
            Issues = @($assessment.Issues)
        })

        if ([bool]$assessment.IsHardFail)
        {
            [void](Write-ArtifactJson -Name ("{0}-character-alignment.json" -f $script:RunTag) -Object ([ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                Passed = $false
                FailureMode = "HardFail"
                RequiredStableValidSamples = $requiredStableValidSamples
                StableValidSamplesObserved = $stableValidSamples
                AttemptCount = $samples.Count
                TransientRecoveryObserved = $false
                Samples = @($samples.ToArray())
            }))
            throw (@($assessment.Issues) -join " ")
        }

        if ([bool]$assessment.IsValid)
        {
            $stableValidSamples++
            $finalSnapshot = $snap

            if ($stableValidSamples -ge $requiredStableValidSamples)
            {
                break
            }

            if ($attempt -lt $maxAttempts)
            {
                Write-Info "Character alignment snapshot looks valid; confirming with one additional sample"
                Start-Sleep -Milliseconds $retryDelayMs
            }

            continue
        }

        $stableValidSamples = 0
        if ($attempt -lt $maxAttempts)
        {
            Write-WarnLine "Character alignment snapshot invalid (attempt $attempt/$maxAttempts): $(@($assessment.Issues) -join ' ') Retrying."
            Start-Sleep -Milliseconds $retryDelayMs
        }
    }

    if ($null -eq $finalSnapshot -or $stableValidSamples -lt $requiredStableValidSamples)
    {
        $failureMessage = if ($null -ne $lastAssessment -and @($lastAssessment.Issues).Count -gt 0)
        {
            @($lastAssessment.Issues) -join " "
        }
        else
        {
            "Unable to read a stable player snapshot for alignment checks."
        }

        [void](Write-ArtifactJson -Name ("{0}-character-alignment.json" -f $script:RunTag) -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Passed = $false
            FailureMode = "RetryExhausted"
            RequiredStableValidSamples = $requiredStableValidSamples
            StableValidSamplesObserved = $stableValidSamples
            AttemptCount = $samples.Count
            TransientRecoveryObserved = [bool]($samples | Where-Object { -not $_.IsValid })
            Samples = @($samples.ToArray())
            Message = $failureMessage
        }))

        throw $failureMessage
    }

    [void](Write-ArtifactJson -Name ("{0}-character-alignment.json" -f $script:RunTag) -Object ([ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Passed = $true
        RequiredStableValidSamples = $requiredStableValidSamples
        StableValidSamplesObserved = $stableValidSamples
        AttemptCount = $samples.Count
        TransientRecoveryObserved = [bool]($samples | Where-Object { -not $_.IsValid })
        Samples = @($samples.ToArray())
        FinalSnapshot = [ordered]@{
            UIMapId = $finalSnapshot.UIMapId
            MapX = $finalSnapshot.MapX
            MapY = $finalSnapshot.MapY
            Dead = [bool]$finalSnapshot.Dead
            Swimming = [bool]$finalSnapshot.Swimming
            ChatInputVisible = [bool]$finalSnapshot.ChatInputVisible
        }
    }))

    if ([bool]$finalSnapshot.Dead)
    {
        Write-WarnLine "Character is dead; allowing startup so corpse-recovery GOAP can run autonomously."
    }

    Write-Ok "Character alignment checks passed (MapX=$([math]::Round($finalSnapshot.MapX, 2)), MapY=$([math]::Round($finalSnapshot.MapY, 2)), UIMapId=$($finalSnapshot.UIMapId), Attempts=$($samples.Count))"
}

function Start-Bot
{
    Write-Info "Starting bot"
    $res = Invoke-AgentApi -Method POST -Path "/api/bot/start" -Body @{}
    if ($null -eq $res -or -not $res.IsActive)
    {
        throw "Bot start failed: $(Get-JsonDepth -Obj $res -Depth 10)"
    }

    $status = Invoke-AgentApi -Method GET -Path "/api/bot/status" -TimeoutSec 5
    if ($null -eq $status -or -not $status.IsActive)
    {
        throw "Bot reported inactive immediately after start."
    }

    Write-Ok "Bot is active"
}

function Stop-BotAndServices
{
    Write-Info "Stopping bot/services"
    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/bot/stop" -Body @{}
    }
    catch
    {
        Write-WarnLine "Bot stop API unavailable: $($_.Exception.Message)"
    }

    if ($StopServices -or $Action -in @("Stop", "Restart"))
    {
        Stop-BlazorServer

        $nav = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
        if ($nav)
        {
            Write-Info "Stopping navigation server (PID $($nav.Id))"
            Stop-Process -Id $nav.Id -Force -ErrorAction SilentlyContinue
        }

        Stop-StaleServerProcesses
    }

    Write-Ok "Stop sequence complete"
}

function New-ValidationCheckResult
{
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][bool]$Passed,
        [string]$Message = "",
        [AllowNull()][object]$Data = $null
    )

    return [ordered]@{
        Name = $Name
        Passed = $Passed
        Message = $Message
        Data = $Data
    }
}

function Get-LatestServerLogReference
{
    try
    {
        $files = Invoke-AgentApi -Method GET -Path "/api/logs/files" -TimeoutSec 6
        if ($null -eq $files -or $null -eq $files.Files)
        {
            return $null
        }

        return $files.Files | Sort-Object LastWriteTimeUtc -Descending | Select-Object -First 1
    }
    catch
    {
        return $null
    }
}

function Invoke-SystemValidation
{
    Write-Info "Running system validation suite"
    $checks = New-Object System.Collections.Generic.List[object]

    try
    {
        $health = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 5
        $healthPass = $null -ne $health -and "$($health.Status)" -ne "FAILED"
        [void]$checks.Add((New-ValidationCheckResult -Name "Health endpoint" -Passed $healthPass -Message "Status=$($health.Status)" -Data $health))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Health endpoint" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $launch = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 8
        $pass = [bool]$launch.CanStartBot
        $blocking = @($launch.Checks | Where-Object { $_.IsBlocking } | ForEach-Object { "$($_.Title): $($_.Message)" })
        [void]$checks.Add((New-ValidationCheckResult -Name "Launch readiness" -Passed $pass -Message ($blocking -join "; ") -Data $launch))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Launch readiness" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $botStatus = Invoke-AgentApi -Method GET -Path "/api/bot/status" -TimeoutSec 5
        [void]$checks.Add((New-ValidationCheckResult -Name "Bot status" -Passed ([bool]$botStatus.IsActive) -Message "IsActive=$($botStatus.IsActive), Goal=$($botStatus.CurrentGoal)" -Data $botStatus))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Bot status" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $sessionStats = Invoke-AgentApi -Method GET -Path "/api/session/stats" -TimeoutSec 5
        $statsSource = "$($sessionStats.StatsSource)"
        $runtimeMode = "$($sessionStats.RuntimeMode)"
        $pass = -not [string]::IsNullOrWhiteSpace($statsSource) -and $statsSource -ne "unavailable"
        [void]$checks.Add((New-ValidationCheckResult -Name "Session stats" -Passed $pass -Message "Source=$statsSource, RuntimeMode=$runtimeMode, Kills=$($sessionStats.Kills), Deaths=$($sessionStats.Deaths)" -Data $sessionStats))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Session stats" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $statusTest = Invoke-AgentApi -Method GET -Path "/api/test/status" -TimeoutSec $ValidationTimeoutSeconds
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/status" -Passed ([bool]$statusTest.Success) -Message "$($statusTest.Message)" -Data $statusTest))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/status" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $frameTest = Invoke-AgentApi -Method GET -Path "/api/test/frames" -TimeoutSec $ValidationTimeoutSeconds
        $validFrames = 0
        if ($frameTest.Data -and $frameTest.Data.validFrameCount)
        {
            $validFrames = [int]$frameTest.Data.validFrameCount
        }

        $pass = [bool]$frameTest.Success -and $validFrames -ge 300
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/frames" -Passed $pass -Message "validFrameCount=$validFrames" -Data $frameTest))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/frames" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $snapshot = Invoke-AgentApi -Method GET -Path "/api/test/snapshot" -TimeoutSec $ValidationTimeoutSeconds
        $snapData = $snapshot.Data.snapshot
        $pass = [bool]$snapshot.Success -and $null -ne $snapData -and [int]$snapData.UIMapId -gt 0
        $msg = if ($null -ne $snapData) { "UIMapId=$($snapData.UIMapId), Dead=$($snapData.Dead), Swimming=$($snapData.Swimming)" } else { "No snapshot data" }
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/snapshot" -Passed $pass -Message $msg -Data $snapshot))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "Test/snapshot" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $goap = Invoke-AgentApi -Method GET -Path "/api/troubleshoot/goap" -TimeoutSec 10
        $pass = "$($goap.Status)" -ne "NotInitialized"
        $goalName = ""
        if ($goap.PSObject.Properties.Match("CurrentGoal").Count -gt 0 -and $null -ne $goap.CurrentGoal)
        {
            if ($goap.CurrentGoal.PSObject.Properties.Match("DisplayName").Count -gt 0)
            {
                $goalName = "$($goap.CurrentGoal.DisplayName)"
            }
            elseif ($goap.CurrentGoal.PSObject.Properties.Match("Name").Count -gt 0)
            {
                $goalName = "$($goap.CurrentGoal.Name)"
            }
        }

        [void]$checks.Add((New-ValidationCheckResult -Name "GOAP diagnostics" -Passed $pass -Message "Status=$($goap.Status), Goal=$goalName" -Data $goap))
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "GOAP diagnostics" -Passed $false -Message $_.Exception.Message))
    }

    try
    {
        $pattern = "Cannot consume scoped service 'Core.ConfigurableInput' from singleton"
        $logCandidates = New-Object System.Collections.Generic.List[string]

        $releaseDir = Join-Path $BotRoot "BlazorServer\bin\Release\net10.0"
        if (Test-Path -LiteralPath $releaseDir)
        {
            Get-ChildItem -LiteralPath $releaseDir -Filter "out*.log" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                ForEach-Object { [void]$logCandidates.Add($_.FullName) }
        }

        if (Test-Path -LiteralPath $script:LogsDir)
        {
            Get-ChildItem -LiteralPath $script:LogsDir -Filter "*.log" -ErrorAction SilentlyContinue |
                Sort-Object LastWriteTime -Descending |
                ForEach-Object { [void]$logCandidates.Add($_.FullName) }
        }

        if ($logCandidates.Count -eq 0)
        {
            [void]$checks.Add((New-ValidationCheckResult -Name "DI scope error scan" -Passed $false -Message "No log files found for scan"))
        }
        else
        {
            $content = $null
            $usedLog = $null
            foreach ($candidate in $logCandidates)
            {
                try
                {
                    $tailLines = Get-Content -LiteralPath $candidate -Tail 1200 -ErrorAction Stop
                    $content = $tailLines -join "`n"
                    $usedLog = $candidate
                    break
                }
                catch
                {
                    # Keep scanning if file is locked/unreadable.
                }
            }

            if ($null -eq $content)
            {
                [void]$checks.Add((New-ValidationCheckResult -Name "DI scope error scan" -Passed $false -Message "No readable logs (all candidates locked or inaccessible)"))
            }
            else
            {
                $pass = -not $content.Contains($pattern)
                [void]$checks.Add((New-ValidationCheckResult -Name "DI scope error scan" -Passed $pass -Message "Log=$usedLog" -Data @{ Log = $usedLog }))
            }
        }
    }
    catch
    {
        [void]$checks.Add((New-ValidationCheckResult -Name "DI scope error scan" -Passed $false -Message $_.Exception.Message))
    }

    $overallPass = -not ($checks | Where-Object { -not $_.Passed })
    $report = [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Action = $Action
        BaseUrl = $BaseUrl
        Profile = $Profile
        OverallPass = $overallPass
        Checks = $checks
    }

    $reportPath = Join-Path $script:LogsDir "$script:RunTag-validation.json"
    Set-Content -LiteralPath $reportPath -Value ($report | ConvertTo-Json -Depth 16) -Encoding UTF8

    if ($overallPass)
    {
        Write-Ok "Validation passed. Report: $reportPath"
    }
    else
    {
        Write-WarnLine "Validation found failures. Report: $reportPath"
    }

    return $report
}

function Show-Status
{
    Write-Info "Collecting status"

    $rows = @()
    $blazor = Get-Process -Name "BlazorServer" -ErrorAction SilentlyContinue
    $nav = Get-Process -Name "AmeisenNavigationServer" -ErrorAction SilentlyContinue
    $wow = Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue

    $rows += "Process BlazorServer: " + $(if ($blazor) { "RUNNING PID=$($blazor.Id)" } else { "STOPPED" })
    $rows += "Process NavServer:    " + $(if ($nav) { "RUNNING PID=$($nav.Id)" } else { "STOPPED" })
    $rows += "Process WoW:          " + $(if ($wow) { "RUNNING PID=$($wow.Id)" } else { "STOPPED" })
    $rows += "Port ${WebPort}:          " + $(if (Test-PortListening -Port $WebPort) { "LISTENING" } else { "CLOSED" })
    $rows += "Port ${NavigationPort}:       " + $(if (Test-PortListening -Port $NavigationPort) { "LISTENING" } else { "CLOSED" })
    $rows += "API preferred base:    $script:PreferredApiBase"

    foreach ($r in $rows) { Write-Host $r }

    try
    {
        $health = Invoke-AgentApi -Method GET -Path "/api/health" -TimeoutSec 4
        Write-Host ("API /health:        Status={0} Stage={1}" -f $health.Status, $health.Startup.CurrentStage)
    }
    catch
    {
        Write-Host "API /health:        UNAVAILABLE"
    }

    try
    {
        $launch = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 4
        Write-Host ("API /launch/status: CanStartBot={0}" -f $launch.CanStartBot)
        foreach ($check in $launch.Checks)
        {
            Write-Host ("  - {0}: {1} ({2})" -f $check.Title, $check.Status, $check.Message)
        }
    }
    catch
    {
        Write-Host "API /launch/status: UNAVAILABLE"
    }

    try
    {
        $bot = Invoke-AgentApi -Method GET -Path "/api/bot/status" -TimeoutSec 4
        Write-Host ("API /bot/status:    IsActive={0} Goal={1}" -f $bot.IsActive, $bot.CurrentGoal)
    }
    catch
    {
        Write-Host "API /bot/status:    UNAVAILABLE"
    }
}

function Invoke-GameCommandAction
{
    if ([string]::IsNullOrWhiteSpace($Command))
    {
        throw "-Command is required when -Action GameCmd is used."
    }

    $result = Invoke-VerifiedSlashCommand -SlashCommand $Command -VerifyKind $Verify -Retries $MaxCommandRetries
    $result | ConvertTo-Json -Depth 10
}

function Invoke-FlagsProfileAction
{
    Ensure-SessionArtifactDir | Out-Null
    Apply-FeatureFlagProfile -ProfileName $NavProfile -VerifyViaApi

    $resp = Invoke-AgentApiSafe -Method GET -Path "/api/features" -TimeoutSec 8
    if ($resp.Success)
    {
        [void](Write-ArtifactJson -Name ("{0}-flags-profile-verify.json" -f $script:RunTag) -Object $resp.Result)
        $resp.Result | ConvertTo-Json -Depth 16
        return
    }

    [void](Write-ArtifactJson -Name ("{0}-flags-profile-verify-error.json" -f $script:RunTag) -Object $resp)
    Write-WarnLine "Unable to verify /api/features after profile apply: $($resp.Error)"
}

function Invoke-Doctor
{
    param(
        [switch]$SkipEvidence,
        [switch]$ReturnDispositionOnly
    )

    Ensure-SessionArtifactDir | Out-Null
    if (-not $SkipEvidence)
    {
        Invoke-CollectEvidence -Stage "doctor-before"
    }

    $dtcSig = Test-DtcFrozenStartupSignature -IgnoreStageRequirement
    $dtcRecovered = $false
    if ($dtcSig.IsFrozen)
    {
        Write-WarnLine "Doctor detected frozen DTC signature; attempting /reload recovery"
        $dtcRecovery = Invoke-DtcReloadRecovery -Reason "doctor-dtc-frozen" -Force
        $dtcRecovered = [bool]$dtcRecovery.Recovered
        if (-not $dtcRecovered)
        {
            $disposition = [pscustomobject]@{
                Disposition = "Blocked"
                Reason = "DTCFrozen"
                DtcRecovered = $false
            }

            if (-not $SkipEvidence)
            {
                Invoke-CollectEvidence -Stage "doctor-blocked-dtc"
            }

            if ($ReturnDispositionOnly)
            {
                return $disposition
            }

            $disposition | ConvertTo-Json -Depth 8
            return
        }
    }

    Invoke-ReadinessFixes
    $launch = $null
    try
    {
        $launch = Wait-ForReadiness
    }
    catch
    {
        if (-not $SkipEvidence)
        {
            Invoke-CollectEvidence -Stage "doctor-blocked-readiness"
        }

        if ($ReturnDispositionOnly)
        {
            return [pscustomobject]@{
                Disposition = "Blocked"
                Reason = "ReadinessTimeout"
                Error = $_.Exception.Message
            }
        }

        throw
    }

    $actionBarBypassed = $false
    try
    {
        $launchPost = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 8
        [void](Clear-TransientKeyBindingsBypassIfHealthy -Launch $launchPost)
        [void](Clear-TransientActionBarBypassIfHealthy -Launch $launchPost)
        $launchPost = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 8
        $launch = $launchPost
        $actionBarBypassed = [bool](Get-LaunchActionBarBypassOverrideInfo -Launch $launchPost).Enabled
    }
    catch { }

    if (-not $SkipEvidence)
    {
        Invoke-CollectEvidence -Stage "doctor-after"
    }

    $finalDisposition = if ($actionBarBypassed) { "ReadyWithBypass" } else { "Ready" }
    $doctorResult = [pscustomobject]@{
        Disposition = $finalDisposition
        Reason = "Ready"
        DtcRecovered = $dtcRecovered
        Launch = $launch
    }

    if ($ReturnDispositionOnly)
    {
        return $doctorResult
    }

    $doctorResult | ConvertTo-Json -Depth 12
}

function Invoke-WatchNav
{
    param(
        [int]$DurationSeconds = $WatchSeconds,
        [int]$CadenceMs = $WatchCadenceMs,
        [int]$RequiredAcceptedFollowSamples = 0,
        [switch]$Quiet
    )

    Assert-ServiceReadinessGate -Context "watchnav-start" -RequireApiHealth
    Assert-ProfileRouteReadyGate -Context "watchnav-start"
    Assert-CastingSnapshotReadyGate -Context "watchnav-start"
    Ensure-SessionArtifactDir | Out-Null
    $sampleFile = Join-Path $script:SessionArtifactDir ("{0}-watchnav-samples.jsonl" -f $script:RunTag)
    $deadline = (Get-Date).AddSeconds([Math]::Max(1, $DurationSeconds))
    $samples = New-Object System.Collections.Generic.List[object]
    $acceptedFollowSamples = New-Object System.Collections.Generic.List[object]
    $rejectedSampleCountsByReason = [ordered]@{}

    while ((Get-Date) -lt $deadline)
    {
        $stamp = (Get-Date).ToUniversalTime().ToString("o")
        $bot = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
        $snap = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 8
        $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
        $nav = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
        $soak = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/soak/current" -TimeoutSec 8

        $sample = [ordered]@{
            TimestampUtc = $stamp
            BotStatus = $(if ($bot.Success) { $bot.Result } else { $null })
            Snapshot = $(if ($snap.Success -and $null -ne $snap.Result -and $snap.Result.Success) { $snap.Result.Data.snapshot } else { $null })
            LaunchStatus = $(if ($launch.Success) { $launch.Result } else { $null })
            NavigationRuntime = $(if ($nav.Success) { $nav.Result } else { $null })
            SoakCurrent = $(if ($soak.Success) { $soak.Result } else { $null })
            Errors = @(
                $(if (-not $bot.Success) { "bot:$($bot.Error)" }),
                $(if (-not $snap.Success) { "snapshot:$($snap.Error)" }),
                $(if (-not $launch.Success) { "launch:$($launch.Error)" }),
                $(if (-not $nav.Success) { "nav:$($nav.Error)" }),
                $(if (-not $soak.Success) { "soak:$($soak.Error)" })
            ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        }

        $evaluation = Get-RerouteReadinessEvaluation `
            -BotStatus $sample.BotStatus `
            -Snapshot $sample.Snapshot `
            -LaunchStatus $sample.LaunchStatus `
            -SessionStats $null
        $navigationAssessment = Get-LaunchNavigationAssessment -Launch $sample.LaunchStatus

        $sample["NavigationCheckStatus"] = $navigationAssessment.Status
        $sample["NavigationCheckMessage"] = $navigationAssessment.Message
        $sample["NavigationSource"] = $navigationAssessment.Source
        $sample["NavigationBlockingReason"] = $navigationAssessment.BlockingReason
        $sample["AcceptedRouteFollowSample"] = [bool]$evaluation.SamplePassed
        $sample["RejectedReason"] = $evaluation.RejectionReason
        $sample["LiveStateContaminated"] = [bool]$evaluation.LiveStateContaminated

        [void]$samples.Add($sample)
        if ([bool]$evaluation.SamplePassed)
        {
            [void]$acceptedFollowSamples.Add($sample)
        }
        elseif (-not [string]::IsNullOrWhiteSpace($evaluation.RejectionReason))
        {
            if (-not $rejectedSampleCountsByReason.Contains($evaluation.RejectionReason))
            {
                $rejectedSampleCountsByReason[$evaluation.RejectionReason] = 0
            }

            $rejectedSampleCountsByReason[$evaluation.RejectionReason] = [int]$rejectedSampleCountsByReason[$evaluation.RejectionReason] + 1
        }

        ($sample | ConvertTo-Json -Depth 24 -Compress) | Add-Content -LiteralPath $sampleFile -Encoding UTF8

        if (-not $Quiet)
        {
            $goal = if ($sample.BotStatus) { "$($sample.BotStatus.CurrentGoal)" } else { "n/a" }
            $maxDev = "n/a"
            $repeatRate = "n/a"
            $trigger = "n/a"
            $bypass = "n/a"

            if ($sample.NavigationRuntime)
            {
                if ($sample.NavigationRuntime.Soak -and $null -ne $sample.NavigationRuntime.Soak.CurrentWindowMaxRouteDeviation)
                {
                    $maxDev = [math]::Round([double]$sample.NavigationRuntime.Soak.CurrentWindowMaxRouteDeviation, 2)
                }
                if ($sample.NavigationRuntime.Soak -and $null -ne $sample.NavigationRuntime.Soak.CurrentWindowRepeatStuckRate)
                {
                    $repeatRate = [math]::Round([double]$sample.NavigationRuntime.Soak.CurrentWindowRepeatStuckRate, 4)
                }
                if ($sample.NavigationRuntime.StuckDetector -and $sample.NavigationRuntime.StuckDetector.LastTriggerReason)
                {
                    $trigger = "$($sample.NavigationRuntime.StuckDetector.LastTriggerReason)"
                }
                if ($sample.NavigationRuntime.Navigation)
                {
                    $bypass = "$($sample.NavigationRuntime.Navigation.FrontBypassAttemptCount)"
                }
            }

            Write-Info ("WatchNav: Goal={0} Accepted={1} MaxDev={2} RepeatRate={3} LastTrigger={4} FrontBypass={5}" -f $goal, [bool]$sample.AcceptedRouteFollowSample, $maxDev, $repeatRate, $trigger, $bypass)
        }

        if ($RequiredAcceptedFollowSamples -gt 0 -and $acceptedFollowSamples.Count -ge $RequiredAcceptedFollowSamples)
        {
            break
        }

        Start-Sleep -Milliseconds ([Math]::Max(250, $CadenceMs))
    }

    $goalFollowSamples = @($samples | Where-Object {
            $null -ne $_.BotStatus -and "$($_.BotStatus.CurrentGoal)".Trim() -like "Follow*"
        })
    $routeFollowSamples = @($acceptedFollowSamples)

    function Get-TriggerStats
    {
        param(
            [Parameter(Mandatory = $true)]$InputSamples
        )

        $enumeratedSamples = @()
        if ($null -ne $InputSamples)
        {
            if ($InputSamples -is [System.Collections.IEnumerable] -and $InputSamples -isnot [string])
            {
                foreach ($item in $InputSamples)
                {
                    $enumeratedSamples += $item
                }
            }
            else
            {
                $enumeratedSamples = @($InputSamples)
            }
        }
        $seen = @{}
        $uniqueTriggerCount = 0
        $timeoutTriggerCountLocal = 0

        foreach ($s in $enumeratedSamples)
        {
            try
            {
                if ($null -eq $s.NavigationRuntime -or $null -eq $s.NavigationRuntime.StuckDetector)
                {
                    continue
                }

                $st = $s.NavigationRuntime.StuckDetector
                if ($null -eq $st.LastTriggerUtc)
                {
                    continue
                }

                $stamp = "$($st.LastTriggerUtc)"
                if ([string]::IsNullOrWhiteSpace($stamp))
                {
                    continue
                }

                if (-not $seen.ContainsKey($stamp))
                {
                    $seen[$stamp] = $true
                    $uniqueTriggerCount++
                    if ("$($st.LastTriggerReason)" -eq "TimeoutNoProgress")
                    {
                        $timeoutTriggerCountLocal++
                    }
                }
            }
            catch { }
        }

        return [pscustomobject]@{
            UniqueTriggerCount = $uniqueTriggerCount
            TimeoutTriggerCount = $timeoutTriggerCountLocal
        }
    }

    function Get-DeviationStats
    {
        param(
            [Parameter(Mandatory = $true)]$InputSamples
        )

        $enumeratedSamples = @()
        if ($null -ne $InputSamples)
        {
            if ($InputSamples -is [System.Collections.IEnumerable] -and $InputSamples -isnot [string])
            {
                foreach ($item in $InputSamples)
                {
                    $enumeratedSamples += $item
                }
            }
            else
            {
                $enumeratedSamples = @($InputSamples)
            }
        }
        $instantValues = @()
        $fallbackValues = @()
        foreach ($s in $enumeratedSamples)
        {
            try
            {
                $v = $null
                if ($s.NavigationRuntime -and $s.NavigationRuntime.Soak -and $null -ne $s.NavigationRuntime.Soak.CurrentRouteDeviation)
                {
                    $v = [double]$s.NavigationRuntime.Soak.CurrentRouteDeviation
                    $instantValues += $v
                }
                elseif ($s.SoakCurrent -and $null -ne $s.SoakCurrent.CurrentRouteDeviation)
                {
                    $v = [double]$s.SoakCurrent.CurrentRouteDeviation
                    $instantValues += $v
                }
                elseif ($s.NavigationRuntime -and $s.NavigationRuntime.Soak -and $null -ne $s.NavigationRuntime.Soak.CurrentWindowMaxRouteDeviation)
                {
                    $v = [double]$s.NavigationRuntime.Soak.CurrentWindowMaxRouteDeviation
                    $fallbackValues += $v
                }
                elseif ($s.SoakCurrent -and $null -ne $s.SoakCurrent.CurrentWindowMaxRouteDeviation)
                {
                    $v = [double]$s.SoakCurrent.CurrentWindowMaxRouteDeviation
                    $fallbackValues += $v
                }
            }
            catch { }
        }

        $devValues = @($(if ($instantValues.Count -gt 0) { $instantValues } else { $fallbackValues }))
        $max = 0.0
        $avg = 0.0
        if ($devValues.Count -gt 0)
        {
            $max = ($devValues | Measure-Object -Maximum).Maximum
            $avg = ($devValues | Measure-Object -Average).Average
        }

        return [pscustomobject]@{
            Count = $devValues.Count
            Max = [double]$max
            Avg = [double]$avg
            Mode = $(if ($instantValues.Count -gt 0) { "CurrentRouteDeviation" } elseif ($fallbackValues.Count -gt 0) { "CurrentWindowMaxRouteDeviationFallback" } else { "None" })
        }
    }

    $allTriggerStats = Get-TriggerStats -InputSamples $samples
    $routeTriggerStats = Get-TriggerStats -InputSamples $routeFollowSamples
    $stuckTriggerCount = [int]$allTriggerStats.UniqueTriggerCount
    $timeoutTriggerCount = [int]$allTriggerStats.TimeoutTriggerCount
    $routeStuckTriggerCount = [int]$routeTriggerStats.UniqueTriggerCount
    $routeTimeoutTriggerCount = [int]$routeTriggerStats.TimeoutTriggerCount

    $allDevStats = Get-DeviationStats -InputSamples $samples
    $routeDevStats = Get-DeviationStats -InputSamples $routeFollowSamples
    $maxRouteDeviation = [double]$allDevStats.Max
    $avgRouteDeviation = [double]$allDevStats.Avg
    $routeMaxRouteDeviation = [double]$routeDevStats.Max
    $routeAvgRouteDeviation = [double]$routeDevStats.Avg

    $routeFollowDurationSeconds = 0
    if ($samples.Count -gt 0 -and $DurationSeconds -gt 0)
    {
        $totalSampleCount = [int]$samples.Count
        if ($totalSampleCount -lt 1)
        {
            $totalSampleCount = 1
        }

        $routeFollowDurationRatio = ([double]$DurationSeconds * [double]$routeFollowSamples.Count) / [double]$totalSampleCount
        $routeFollowDurationSeconds = [int][Math]::Round([double]$routeFollowDurationRatio, 0)
    }
    if ($routeFollowDurationSeconds -lt 0)
    {
        $routeFollowDurationSeconds = 0
    }

    $routeFrontBypassAttemptCount = @($routeFollowSamples | ForEach-Object {
            if ($_.NavigationRuntime -and $_.NavigationRuntime.Navigation) { [int]$_.NavigationRuntime.Navigation.FrontBypassAttemptCount }
        } | Measure-Object -Maximum).Maximum
    if ($null -eq $routeFrontBypassAttemptCount) { $routeFrontBypassAttemptCount = 0 }

    $allFrontBypassAttemptCount = @($samples | ForEach-Object {
            if ($_.NavigationRuntime -and $_.NavigationRuntime.Navigation) { [int]$_.NavigationRuntime.Navigation.FrontBypassAttemptCount }
        } | Measure-Object -Maximum).Maximum
    if ($null -eq $allFrontBypassAttemptCount) { $allFrontBypassAttemptCount = 0 }

    $invalidReason = $null
    if ($routeFollowSamples.Count -lt [Math]::Max(1, $RequiredAcceptedFollowSamples) -and $samples.Count -gt 0)
    {
        $goalCounts = @{}
        foreach ($s in $samples)
        {
            $goalKey = $null
            if ($s.NavigationRuntime -and -not [string]::IsNullOrWhiteSpace("$($s.NavigationRuntime.CurrentGoal)"))
            {
                $goalKey = "$($s.NavigationRuntime.CurrentGoal)"
            }
            elseif ($s.BotStatus -and -not [string]::IsNullOrWhiteSpace("$($s.BotStatus.currentGoal)"))
            {
                $goalKey = "$($s.BotStatus.currentGoal)"
            }

            if ([string]::IsNullOrWhiteSpace($goalKey))
            {
                continue
            }

            if (-not $goalCounts.ContainsKey($goalKey))
            {
                $goalCounts[$goalKey] = 0
            }
            $goalCounts[$goalKey] = [int]$goalCounts[$goalKey] + 1
        }

        if ($goalCounts.Count -gt 0)
        {
            $dominantGoal = $goalCounts.GetEnumerator() |
                Sort-Object -Property Value -Descending |
                Select-Object -First 1

            if ($null -ne $dominantGoal)
            {
                $invalidReason = "No FollowRoute samples observed; dominant goal='$($dominantGoal.Key)' ($($dominantGoal.Value)/$($samples.Count) samples)"
            }
        }

        if ([string]::IsNullOrWhiteSpace($invalidReason))
        {
            if ($RequiredAcceptedFollowSamples -gt 0)
            {
                $invalidReason = "Clean FollowRoute sample target was not reached before timeout"
            }
            else
            {
                $invalidReason = "No FollowRoute samples observed"
            }
        }
    }

    $summary = [pscustomobject]@{
        SampleFile = $sampleFile
        DurationSeconds = $DurationSeconds
        RequiredAcceptedRouteFollowSamples = $RequiredAcceptedFollowSamples
        SampleCount = $samples.Count
        RouteFollowSampleCount = $routeFollowSamples.Count
        GoalFollowSampleCount = $goalFollowSamples.Count
        RouteFollowSamplesObserved = ($routeFollowSamples.Count -gt 0)
        NavigationValidationValid = $(if ($RequiredAcceptedFollowSamples -gt 0) { $routeFollowSamples.Count -ge $RequiredAcceptedFollowSamples } else { $routeFollowSamples.Count -gt 0 })
        MixedGoalWindow = ($goalFollowSamples.Count -gt 0 -and $goalFollowSamples.Count -lt $samples.Count)
        RouteFollowEstimatedDurationSeconds = $routeFollowDurationSeconds
        StuckTriggersPerMinute = if ($DurationSeconds -gt 0) { [math]::Round(($stuckTriggerCount * 60.0) / $DurationSeconds, 3) } else { 0 }
        TimeoutNoProgressTriggerRatio = if ($stuckTriggerCount -gt 0) { [math]::Round(($timeoutTriggerCount * 1.0) / $stuckTriggerCount, 3) } else { 0 }
        RouteFollowStuckTriggersPerMinute = if ($routeFollowDurationSeconds -gt 0) { [math]::Round(($routeStuckTriggerCount * 60.0) / $routeFollowDurationSeconds, 3) } else { 0 }
        RouteFollowTimeoutNoProgressTriggerRatio = if ($routeStuckTriggerCount -gt 0) { [math]::Round(($routeTimeoutTriggerCount * 1.0) / $routeStuckTriggerCount, 3) } else { 0 }
        AvgRouteDeviation = [math]::Round([double]$avgRouteDeviation, 2)
        MaxRouteDeviation = [math]::Round([double]$maxRouteDeviation, 2)
        RouteFollowAvgRouteDeviation = [math]::Round([double]$routeAvgRouteDeviation, 2)
        RouteFollowMaxRouteDeviation = [math]::Round([double]$routeMaxRouteDeviation, 2)
        DeviationMetricMode = "$($allDevStats.Mode)"
        RouteFollowDeviationMetricMode = "$($routeDevStats.Mode)"
        FrontBypassAttemptCount = [int]$allFrontBypassAttemptCount
        RouteFollowFrontBypassAttemptCount = [int]$routeFrontBypassAttemptCount
        RejectedSampleCountsByReason = $rejectedSampleCountsByReason
        NavigationCheckStatus = $(if ($samples.Count -gt 0) { $samples[$samples.Count - 1].NavigationCheckStatus } else { $null })
        NavigationCheckMessage = $(if ($samples.Count -gt 0) { $samples[$samples.Count - 1].NavigationCheckMessage } else { $null })
        NavigationSource = $(if ($samples.Count -gt 0) { $samples[$samples.Count - 1].NavigationSource } else { $null })
        NavigationBlockingReason = $(if ($samples.Count -gt 0) { $samples[$samples.Count - 1].NavigationBlockingReason } else { $null })
        InvalidReason = $invalidReason
        LastSample = $(if ($samples.Count -gt 0) { $samples[$samples.Count - 1] } else { $null })
    }

    [void](Write-ArtifactJson -Name ("{0}-watchnav-summary.json" -f $script:RunTag) -Object $summary)
    return $summary
}

function Test-NavChurnDetected
{
    param([Parameter(Mandatory = $true)][object]$WatchSummary)

    if ($null -eq $WatchSummary)
    {
        return $false
    }

    $useRouteOnly = $false
    try
    {
        $useRouteOnly = [bool]$WatchSummary.NavigationValidationValid -and ([int]$WatchSummary.RouteFollowSampleCount -gt 0)
    }
    catch { }

    if ($useRouteOnly)
    {
        return ([double]$WatchSummary.RouteFollowStuckTriggersPerMinute -ge 2.0) -or
            ([double]$WatchSummary.RouteFollowMaxRouteDeviation -ge 20.0) -or
            ([int]$WatchSummary.RouteFollowFrontBypassAttemptCount -ge 3)
    }

    return ([double]$WatchSummary.StuckTriggersPerMinute -ge 2.0) -or
        ([double]$WatchSummary.MaxRouteDeviation -ge 20.0) -or
        ([int]$WatchSummary.FrontBypassAttemptCount -ge 3)
}

function Invoke-NavTriage
{
    Ensure-SessionArtifactDir | Out-Null
    $profiles = @($TriageProfiles.Split(',') | ForEach-Object { $_.Trim() } | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    if ($profiles.Count -eq 0)
    {
        throw "No triage profiles configured."
    }

    $results = New-Object System.Collections.Generic.List[object]
    foreach ($profileName in $profiles)
    {
        Write-Info "NavTriage: testing profile '$profileName' for $TriageMinutesPerProfile minute(s)"
        Apply-FeatureFlagProfile -ProfileName $profileName -VerifyViaApi

        $doctor = Invoke-Doctor -SkipEvidence -ReturnDispositionOnly
        if ($doctor.Disposition -eq "Blocked")
        {
            [void]$results.Add([pscustomobject]@{
                    Profile = $profileName
                    Blocked = $true
                    Doctor = $doctor
                    Score = 999999
                })
            continue
        }

        $botStatus = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
        if (-not $botStatus.Success -or -not [bool]$botStatus.Result.IsActive)
        {
            Start-Bot
            Start-Sleep -Seconds 2
        }

        $watch = Invoke-WatchNav -DurationSeconds ([Math]::Max(30, $TriageMinutesPerProfile * 60)) -CadenceMs $WatchCadenceMs -Quiet
        $null = Invoke-AgentApiSafe -Method POST -Path "/api/diagnostics/soak/flush" -Body @{} -TimeoutSec 20
        $null = Invoke-AgentApiSafe -Method POST -Path "/api/bot/stop" -Body @{} -TimeoutSec 10

        if ([int]$watch.RouteFollowSampleCount -le 0)
        {
            [void]$results.Add([pscustomobject]@{
                    Profile = $profileName
                    Blocked = $true
                    InvalidSample = $true
                    Doctor = $doctor
                    Watch = $watch
                    Score = 999998
                    Reason = "NoRouteFollowSamples"
                })
            continue
        }

        $score = ([double]$watch.StuckTriggersPerMinute * 100.0) +
            ([double]$watch.TimeoutNoProgressTriggerRatio * 50.0) +
            ([double]$watch.AvgRouteDeviation) +
            ([double]$watch.MaxRouteDeviation * 0.25) +
            ([double]$watch.FrontBypassAttemptCount * 10.0)

        [void]$results.Add([pscustomobject]@{
                Profile = $profileName
                Blocked = $false
                Doctor = $doctor
                Watch = $watch
                Score = [math]::Round($score, 3)
            })
    }

    $ranked = @($results | Sort-Object Score, Profile)
    $report = [pscustomobject]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Profiles = $profiles
        Results = $ranked
        RecommendedProfile = $(if ($ranked.Count -gt 0) { $ranked[0].Profile } else { $null })
    }

    [void](Write-ArtifactJson -Name ("{0}-navtriage-report.json" -f $script:RunTag) -Object $report)
    return $report
}

function Invoke-CollectEvidence
{
    param(
        [string]$Stage = "snapshot",
        [switch]$FlushSoak
    )

    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    Ensure-SessionArtifactDir | Out-Null

    [void](Write-ArtifactJson -Name ("{0}-{1}-ports.json" -f $stamp, $Stage) -Object (Get-PortStateSnapshot))
    [void](Save-ApiSnapshot -ApiPath "/api/health" -FileStem ("{0}-{1}-health.json" -f $stamp, $Stage) -TimeoutSec 6)
    [void](Save-ApiSnapshot -ApiPath "/api/launch/status" -FileStem ("{0}-{1}-launch-status.json" -f $stamp, $Stage) -TimeoutSec 8)
    [void](Save-ApiSnapshot -ApiPath "/api/bot/status" -FileStem ("{0}-{1}-bot-status.json" -f $stamp, $Stage) -TimeoutSec 6)
    [void](Save-ApiSnapshot -ApiPath "/api/session" -FileStem ("{0}-{1}-session.json" -f $stamp, $Stage) -TimeoutSec 6)
    [void](Save-ApiSnapshot -ApiPath "/api/session/stats" -FileStem ("{0}-{1}-session-stats.json" -f $stamp, $Stage) -TimeoutSec 6)
    [void](Save-ApiSnapshot -ApiPath "/api/test/status" -FileStem ("{0}-{1}-test-status.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
    [void](Save-ApiSnapshot -ApiPath "/api/test/frames" -FileStem ("{0}-{1}-test-frames.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
    [void](Save-ApiSnapshot -ApiPath "/api/test/snapshot" -FileStem ("{0}-{1}-test-snapshot.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
    [void](Save-ApiSnapshot -ApiPath "/api/diagnostics/navigation/runtime" -FileStem ("{0}-{1}-navigation-runtime.json" -f $stamp, $Stage) -TimeoutSec 8)
    [void](Save-ApiSnapshot -ApiPath "/api/diagnostics/navigation/reroute" -FileStem ("{0}-{1}-navigation-reroute.json" -f $stamp, $Stage) -TimeoutSec 8)
    [void](Save-ApiSnapshot -ApiPath "/api/diagnostics/bags?take=50" -FileStem ("{0}-{1}-bags.json" -f $stamp, $Stage) -TimeoutSec 10)
    [void](Save-ApiSnapshot -ApiPath "/api/features" -FileStem ("{0}-{1}-features.json" -f $stamp, $Stage) -TimeoutSec 8)
    [void](Save-ApiSnapshot -ApiPath "/api/diagnostics/soak/current" -FileStem ("{0}-{1}-soak-current.json" -f $stamp, $Stage) -TimeoutSec 8)
    [void](Save-ApiSnapshot -ApiPath "/api/logs/files" -FileStem ("{0}-{1}-logs-files.json" -f $stamp, $Stage) -TimeoutSec 8)

    if ($FlushSoak)
    {
        $flushResp = Invoke-AgentApiSafe -Method POST -Path "/api/diagnostics/soak/flush" -Body @{} -TimeoutSec 20
        [void](Write-ArtifactJson -Name ("{0}-{1}-soak-flush.json" -f $stamp, $Stage) -Object ([ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                Success = $flushResp.Success
                Error = $flushResp.Error
                Data = $flushResp.Result
            }))
        if ($flushResp.Success)
        {
            Write-Ok "Soak metrics flushed"
        }
        else
        {
            Write-WarnLine "Soak flush failed: $($flushResp.Error)"
        }
    }

    Save-LatestLogTail -Pattern "logs/soak-nav-*.json" -Label ("{0}-{1}-soak-artifact" -f $stamp, $Stage) -Lines 1200
    Save-LatestLogTail -Pattern "logs/agentctl-*-validation.json" -Label ("{0}-{1}-validation-artifact" -f $stamp, $Stage) -Lines 1200
    Save-LatestLogTail -Pattern "out*.log" -Label ("{0}-{1}-server-out" -f $stamp, $Stage) -Lines 400
    Save-LatestLogTail -Pattern "logs/*.log" -Label ("{0}-{1}-logs-tail" -f $stamp, $Stage) -Lines 400

    Write-Ok "Evidence snapshot captured for stage '$Stage' in $script:SessionArtifactDir"
}

function Get-ActiveRunHealthSample
{
    $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
    $launchResult = $(if ($launch.Success) { $launch.Result } else { $null })
    $navigationAssessment = Get-LaunchNavigationAssessment -Launch $launchResult

    $result = [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Ports = (Get-PortStateSnapshot)
        BotStatus = $null
        SessionStats = $null
        LaunchStatus = $launchResult
        NavigationAssessment = $navigationAssessment
        Snapshot = $null
        Health = $null
        Errors = @()
    }

    $health = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
    if ($health.Success) { $result.Health = $health.Result } else { $result.Errors += "health: $($health.Error)" }

    if (-not $launch.Success) { $result.Errors += "launch: $($launch.Error)" }

    $bot = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
    if ($bot.Success) { $result.BotStatus = $bot.Result } else { $result.Errors += "bot: $($bot.Error)" }

    $stats = Invoke-AgentApiSafe -Method GET -Path "/api/session/stats" -TimeoutSec 5
    if ($stats.Success) { $result.SessionStats = $stats.Result } else { $result.Errors += "session-stats: $($stats.Error)" }

    $snap = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 10
    if ($snap.Success -and $snap.Result.Success) { $result.Snapshot = $snap.Result.Data.snapshot } else { $result.Errors += "snapshot: $($snap.Error)" }

    return $result
}

function Assert-NoImmediateAbortCondition
{
    param([Parameter(Mandatory = $true)][object]$Sample)

    if ($null -eq $Sample)
    {
        throw "Active run sample is null"
    }

    if (-not $Sample.Ports.WebPort.Listening)
    {
        throw "Abort: Web port $WebPort is not listening"
    }

    $navigationAssessment = $Sample.NavigationAssessment
    if ($null -eq $navigationAssessment -and $null -ne $Sample.LaunchStatus)
    {
        $navigationAssessment = Get-LaunchNavigationAssessment -Launch $Sample.LaunchStatus
    }

    if ($null -ne $navigationAssessment)
    {
        if (-not [bool]$navigationAssessment.IsOk)
        {
            $reason = if (-not [string]::IsNullOrWhiteSpace("$($navigationAssessment.BlockingReason)"))
            {
                "$($navigationAssessment.BlockingReason)"
            }
            else
            {
                "$($navigationAssessment.Message)"
            }

            throw "Abort: Navigation readiness degraded during live run ($reason)"
        }
    }
    elseif (-not $Sample.Ports.NavigationPort.Listening)
    {
        throw "Abort: Navigation port $NavigationPort is not listening"
    }

    if ($null -ne $Sample.BotStatus -and -not [bool]$Sample.BotStatus.IsActive)
    {
        $reason = "$($Sample.BotStatus.LastDeactivateReason)"
        if ([string]::IsNullOrWhiteSpace($reason))
        {
            $reason = "unknown"
        }

        $when = "$($Sample.BotStatus.LastDeactivateUtc)"
        if ([string]::IsNullOrWhiteSpace($when))
        {
            $when = "unknown"
        }

        throw "Abort: Bot became inactive during live validation (Reason=$reason, LastDeactivateUtc=$when)"
    }

    if ($null -ne $Sample.Snapshot)
    {
        if ([bool]$Sample.Snapshot.Dead) { throw "Abort: Character is dead during live run" }
        if ([bool]$Sample.Snapshot.Swimming) { throw "Abort: Character is swimming during live run" }
        if (($Sample.Snapshot.MapX -le 0) -or ($Sample.Snapshot.MapX -gt 100) -or ($Sample.Snapshot.MapY -le 0) -or ($Sample.Snapshot.MapY -gt 100))
        {
            throw "Abort: Character map position out of bounds (MapX=$($Sample.Snapshot.MapX), MapY=$($Sample.Snapshot.MapY))"
        }
    }
}

function Invoke-ShortActiveValidation
{
    param([int]$DurationSeconds = 240)

    Write-Info "Running short active validation for ${DurationSeconds}s"
    $deadline = (Get-Date).AddSeconds($DurationSeconds)
    $sampleIndex = 0
    while ((Get-Date) -lt $deadline)
    {
        $sampleIndex++
        $sample = Get-ActiveRunHealthSample
        Assert-NoImmediateAbortCondition -Sample $sample

        [void](Write-ArtifactJson -Name ("{0}-short-validate-sample-{1:D3}.json" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $sampleIndex) -Object $sample)

        $goal = if ($null -ne $sample.BotStatus) { "$($sample.BotStatus.CurrentGoal)" } else { "n/a" }
        $pos = if ($null -ne $sample.Snapshot) { "Map=($([math]::Round($sample.Snapshot.MapX,2)),$([math]::Round($sample.Snapshot.MapY,2)))" } else { "Map=(n/a)" }
        Write-Info "Short validation sample ${sampleIndex}: Goal=$goal $pos"

        Start-Sleep -Seconds ([Math]::Max(5, [Math]::Min(15, $MonitorIntervalSeconds)))
    }

    Write-Ok "Short active validation completed"
}

function Get-LiveSessionAcceptanceSummary
{
    param(
        [AllowNull()][object]$WatchSummary,
        [AllowNull()][object]$SoakResult
    )

    $summary = New-ActionSummary -ActionName "LiveSession"
    $summary["InitialWatchRouteFollowSampleCount"] = $(if ($null -ne $WatchSummary) { [int]$WatchSummary.RouteFollowSampleCount } else { 0 })
    $summary["InitialWatchNavigationValid"] = $(if ($null -ne $WatchSummary) { [bool]$WatchSummary.NavigationValidationValid } else { $false })

    $samples = @()
    if ($null -ne $SoakResult -and $null -ne $SoakResult.Samples)
    {
        $samples = @($SoakResult.Samples)
    }

    $summary["SoakSampleCount"] = $samples.Count
    $summary["SoakArtifactPath"] = $(if ($null -ne $SoakResult -and -not [string]::IsNullOrWhiteSpace("$($SoakResult.SoakArtifactPath)")) { "$($SoakResult.SoakArtifactPath)" } else { $null })

    $windows = @()
    if ($null -ne $SoakResult -and $null -ne $SoakResult.SoakArtifact -and $null -ne $SoakResult.SoakArtifact.Windows)
    {
        $windows = @($SoakResult.SoakArtifact.Windows)
    }

    $summary["CompletedWindowCount"] = $windows.Count
    $summary["HealthErrorCount"] = @($samples | Where-Object {
            $errs = @($_.Errors)
            ($null -eq $_.Health) -or (@($errs | Where-Object { "$_" -like "health:*" }).Count -gt 0)
        }).Count
    $summary["PortFailureCount"] = @($samples | Where-Object {
            try
            {
                return ($null -eq $_.Ports) -or (-not [bool]$_.Ports.WebPort.Listening) -or (-not [bool]$_.Ports.NavigationPort.Listening)
            }
            catch
            {
                return $true
            }
        }).Count

    $summary["MaxWindowRepeatStuckRate"] = [math]::Round((Measure-WindowMaximum -Windows $windows -PropertyName "RepeatStuckRate"), 4)
    $summary["MaxWindowMaxRouteDeviation"] = [math]::Round((Measure-WindowMaximum -Windows $windows -PropertyName "MaxRouteDeviation"), 2)
    $summary["MaxWindowStuckEvents"] = [int](Measure-WindowMaximum -Windows $windows -PropertyName "StuckEvents")
    $summary["MaxWindowDetourOnlyCollapseCount"] = [int](Measure-WindowMaximum -Windows $windows -PropertyName "DetourOnlyCollapseCount")

    if ([int]$summary["InitialWatchRouteFollowSampleCount"] -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Initial watch did not observe any FollowRoute samples."
    }

    if ([int]$summary["HealthErrorCount"] -gt 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Health endpoint was unavailable in {0} soak samples." -f [int]$summary["HealthErrorCount"])
    }

    if ([int]$summary["PortFailureCount"] -gt 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Web or navigation listener was unavailable in {0} soak samples." -f [int]$summary["PortFailureCount"])
    }

    if ($windows.Count -eq 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "No completed soak windows were recorded."
    }

    if ([double]$summary["MaxWindowRepeatStuckRate"] -gt 0.30)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("CurrentWindowRepeatStuckRate exceeded threshold: {0}" -f $summary["MaxWindowRepeatStuckRate"])
    }

    if ([double]$summary["MaxWindowMaxRouteDeviation"] -gt 120.0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("CurrentWindowMaxRouteDeviation exceeded threshold: {0}" -f $summary["MaxWindowMaxRouteDeviation"])
    }

    if ([int]$summary["MaxWindowStuckEvents"] -gt 8)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("CurrentWindowStuckEvents exceeded threshold: {0}" -f $summary["MaxWindowStuckEvents"])
    }

    if ([int]$summary["MaxWindowDetourOnlyCollapseCount"] -gt 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("DetourOnlyCollapseCount exceeded threshold: {0}" -f $summary["MaxWindowDetourOnlyCollapseCount"])
    }

    $summary["Passed"] = (@($summary["FailureReasons"]).Count -eq 0)
    return $summary
}

function Get-RerouteAcceptanceSummary
{
    param([AllowNull()][object]$Report)

    $summary = New-ActionSummary -ActionName "ValidateReroute"
    $summary["PreflightPassed"] = $false
    $summary["StableGateFailureReason"] = $null
    $summary["HazardGateFailureReason"] = $null
    $summary["RearmAttempts"] = 0
    $summary["RejectedSampleCountsByReason"] = [ordered]@{}
    $summary["LiveStateContaminated"] = $false
    $summary["StableWatchRouteFollowSampleCount"] = 0
    $summary["HazardWatchRouteFollowSampleCount"] = 0
    $summary["RerouteEvidenceClosed"] = $false
    $summary["TriggerCount"] = 0
    $summary["ApplyCount"] = 0
    $summary["DropCount"] = 0
    $summary["DetourOnlyCollapseCount"] = 0
    $summary["ProbeDistanceXY"] = 0
    $summary["ProbeZDelta"] = 0
    $summary["HazardSnapshotValid"] = $false
    $summary["CentroidToSegmentDistance"] = $null
    $summary["NavigationCheckStatus"] = $null
    $summary["NavigationCheckMessage"] = $null
    $summary["NavigationSource"] = $null
    $summary["NavigationBlockingReason"] = $null

    if ($null -eq $Report)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Reroute validation report was not generated."
        $summary["Passed"] = $false
        return $summary
    }

    $summary["PreflightPassed"] = [bool]$Report.PreflightPassed
    $summary["StableGateFailureReason"] = $Report.StableGateFailureReason
    $summary["HazardGateFailureReason"] = $Report.HazardGateFailureReason
    $summary["RearmAttempts"] = [int]$Report.RearmAttempts
    $summary["RejectedSampleCountsByReason"] = $(if ($null -ne $Report.RejectedSampleCountsByReason) { $Report.RejectedSampleCountsByReason } else { [ordered]@{} })
    $summary["LiveStateContaminated"] = [bool]$Report.LiveStateContaminated
    $summary["StableWatchRouteFollowSampleCount"] = $(if ($null -ne $Report.StableWatch) { [int]$Report.StableWatch.RouteFollowSampleCount } else { 0 })
    $summary["HazardWatchRouteFollowSampleCount"] = $(if ($null -ne $Report.HazardWatch) { [int]$Report.HazardWatch.RouteFollowSampleCount } else { 0 })
    $summary["RerouteEvidenceClosed"] = [bool]$Report.RerouteEvidenceClosed
    $summary["TriggerCount"] = [int]$Report.TriggerCount
    $summary["ApplyCount"] = [int]$Report.ApplyCount
    $summary["DropCount"] = [int]$Report.DropCount
    $summary["DetourOnlyCollapseCount"] = [int]$Report.DetourOnlyCollapseCount
    $summary["ProbeDistanceXY"] = [double]$Report.ProbeDistanceXY
    $summary["ProbeZDelta"] = [double]$Report.ProbeZDelta
    $summary["HazardSnapshotValid"] = [bool]$Report.HazardSnapshotValid
    $summary["CentroidToSegmentDistance"] = $Report.CentroidToSegmentDistance
    $summary["NavigationCheckStatus"] = $(if ($null -ne $Report.HazardWatch -and -not [string]::IsNullOrWhiteSpace("$($Report.HazardWatch.NavigationCheckStatus)")) { "$($Report.HazardWatch.NavigationCheckStatus)" } elseif ($null -ne $Report.StableWatch) { "$($Report.StableWatch.NavigationCheckStatus)" } else { $null })
    $summary["NavigationCheckMessage"] = $(if ($null -ne $Report.HazardWatch -and -not [string]::IsNullOrWhiteSpace("$($Report.HazardWatch.NavigationCheckMessage)")) { "$($Report.HazardWatch.NavigationCheckMessage)" } elseif ($null -ne $Report.StableWatch) { "$($Report.StableWatch.NavigationCheckMessage)" } else { $null })
    $summary["NavigationSource"] = $(if ($null -ne $Report.HazardWatch -and -not [string]::IsNullOrWhiteSpace("$($Report.HazardWatch.NavigationSource)")) { "$($Report.HazardWatch.NavigationSource)" } elseif ($null -ne $Report.StableWatch) { "$($Report.StableWatch.NavigationSource)" } else { $null })
    $summary["NavigationBlockingReason"] = $(if ($null -ne $Report.HazardWatch -and -not [string]::IsNullOrWhiteSpace("$($Report.HazardWatch.NavigationBlockingReason)")) { "$($Report.HazardWatch.NavigationBlockingReason)" } elseif ($null -ne $Report.StableWatch) { "$($Report.StableWatch.NavigationBlockingReason)" } else { $null })

    if (-not [bool]$summary["PreflightPassed"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "Reroute preflight did not reach a clean FollowRoute state before validation."
    }
    if (-not [string]::IsNullOrWhiteSpace("$($summary["StableGateFailureReason"])"))
    {
        Add-ActionFailureReason -Summary $summary -Reason "$($summary["StableGateFailureReason"])"
    }
    if (-not [string]::IsNullOrWhiteSpace("$($summary["HazardGateFailureReason"])"))
    {
        Add-ActionFailureReason -Summary $summary -Reason "$($summary["HazardGateFailureReason"])"
    }
    if ([bool]$summary["LiveStateContaminated"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "Invalid live state for reroute proof was observed during validation."
    }
    if ([int]$summary["StableWatchRouteFollowSampleCount"] -lt 60)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Stable watch RouteFollowSampleCount must be >= 60 but was {0}." -f [int]$summary["StableWatchRouteFollowSampleCount"])
    }
    if (-not [bool]$summary["RerouteEvidenceClosed"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "Reroute validation did not close trigger/apply/drop within the allotted window."
    }
    if ([int]$summary["TriggerCount"] -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "RerouteTriggerCount did not exceed zero."
    }
    if ([int]$summary["ApplyCount"] -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "RerouteApplyCount did not exceed zero."
    }
    if ([int]$summary["DropCount"] -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "RerouteDropCount did not exceed zero."
    }
    if ([int]$summary["DetourOnlyCollapseCount"] -ne 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("DetourOnlyCollapseCount must be zero but was {0}." -f [int]$summary["DetourOnlyCollapseCount"])
    }
    if ([int]$summary["HazardWatchRouteFollowSampleCount"] -lt 60)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Hazard watch RouteFollowSampleCount must be >= 60 but was {0}." -f [int]$summary["HazardWatchRouteFollowSampleCount"])
    }
    if (-not [bool]$summary["HazardSnapshotValid"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "Synthetic hazard snapshot did not prove route intersection."
    }

    $summary["Passed"] = (@($summary["FailureReasons"]).Count -eq 0)
    return $summary
}

function Get-NoProgressAcceptanceSummary
{
    param([AllowNull()][object]$Report)

    $summary = New-ActionSummary -ActionName "ValidateNoProgress"
    $summary["TriggerObserved"] = $false
    $summary["RecoveryObserved"] = $false
    $summary["TriggerReason"] = $null
    $summary["SampleCount"] = 0

    if ($null -eq $Report)
    {
        Add-ActionFailureReason -Summary $summary -Reason "No-progress validation report was not generated."
        $summary["Passed"] = $false
        return $summary
    }

    $summary["TriggerObserved"] = [bool]$Report.TriggerObserved
    $summary["RecoveryObserved"] = [bool]$Report.RecoveryObserved
    $summary["TriggerReason"] = "$($Report.TriggerReason)"
    $summary["SampleCount"] = [int]$Report.SampleCount

    if (-not [bool]$summary["TriggerObserved"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "No explicit ShortNoProgress or TimeoutNoProgress trigger was observed."
    }
    if (-not [bool]$summary["RecoveryObserved"])
    {
        Add-ActionFailureReason -Summary $summary -Reason "Recovery was not observed after the no-progress trigger."
    }
    if ([bool]$summary["TriggerObserved"] -and ("$($summary["TriggerReason"])" -notin @("ShortNoProgress", "TimeoutNoProgress")))
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Unexpected trigger reason observed: {0}" -f "$($summary["TriggerReason"])")
    }

    $summary["Passed"] = (@($summary["FailureReasons"]).Count -eq 0)
    return $summary
}

function Get-CombatAcceptanceSummary
{
    param([AllowNull()][object]$Report)

    $focusContaminationThreshold = 3
    $summary = New-ActionSummary -ActionName "ValidateCombat"
    $summary["KillsDelta"] = 0
    $summary["TargetKills"] = 30
    $summary["SpellCoverageComplete"] = $false
    $summary["PullOpenerCounts"] = [ordered]@{
        CurseOfAgony = 0
        Immolate = 0
        ShadowBolt = 0
    }
    $summary["SpellCounts"] = [ordered]@{
        Immolate = 0
        Corruption = 0
        ShadowBolt = 0
        Shoot = 0
    }
    $summary["FearCastCount"] = 0
    $summary["DrainLifeCastCount"] = 0
    $summary["HealthstoneUseCount"] = 0
    $summary["HealthstoneCreateCount"] = 0
    $summary["SummonVoidwalkerCount"] = 0
    $summary["SummonImpCount"] = 0
    $summary["PetSummonAttemptCount"] = 0
    $summary["ApproachAssistCount"] = 0
    $summary["BodyPullFallbackCount"] = 0
    $summary["RangedStandoffMaintained"] = $false
    $summary["RangedFillerDominant"] = $false
    $summary["FacingRecoveryCount"] = 0
    $summary["LowHealthSampleCount"] = 0
    $summary["CriticalHealthSampleCount"] = 0
    $summary["FocusRestoreFailureCount"] = 0
    $summary["FocusContaminationThreshold"] = $focusContaminationThreshold
    $summary["FocusContaminated"] = $false
    $summary["SpellFailedMovingCount"] = 0
    $summary["BadAttackFacingCount"] = 0
    $summary["LostTargetBurstCountWindow"] = 0
    $summary["PullFailureSoftRetryCountWindow"] = 0
    $summary["LostTargetReacquireAttemptCountWindow"] = 0
    $summary["LostTargetReacquireSuccessCountWindow"] = 0
    $summary["LostTargetReacquireSuccessRatio"] = $null

    if ($null -eq $Report)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Controlled combat validation report was not generated."
        $summary["Passed"] = $false
        return $summary
    }

    $summary["KillsDelta"] = [int]$Report.KillsDelta
    $summary["TargetKills"] = [int]$Report.TargetKills
    $summary["PullOpenerCounts"] = [ordered]@{
        CurseOfAgony = [int]$Report.PullOpenerCounts.CurseOfAgony
        Immolate = [int]$Report.PullOpenerCounts.Immolate
        ShadowBolt = [int]$Report.PullOpenerCounts.ShadowBolt
    }
    $summary["SpellCounts"] = [ordered]@{
        Immolate = [int]$Report.SpellCounts.Immolate
        Corruption = [int]$Report.SpellCounts.Corruption
        ShadowBolt = [int]$Report.SpellCounts.ShadowBolt
        Shoot = [int]$Report.SpellCounts.Shoot
    }
    $summary["FearCastCount"] = [int]$Report.FearCastCount
    $summary["DrainLifeCastCount"] = [int]$Report.DrainLifeCastCount
    $summary["HealthstoneUseCount"] = [int]$Report.HealthstoneUseCount
    $summary["HealthstoneCreateCount"] = [int]$Report.HealthstoneCreateCount
    $summary["SummonVoidwalkerCount"] = [int]$Report.SummonVoidwalkerCount
    $summary["SummonImpCount"] = [int]$Report.SummonImpCount
    $summary["PetSummonAttemptCount"] = [int]$Report.SummonVoidwalkerCount + [int]$Report.SummonImpCount
    $summary["ApproachAssistCount"] = [int]$Report.ApproachAssistCount
    $summary["BodyPullFallbackCount"] = [int]$Report.BodyPullFallbackCount
    $summary["FacingRecoveryCount"] = [int]$Report.FacingRecoveryCount
    $summary["LowHealthSampleCount"] = [int]$Report.LowHealthSampleCount
    $summary["CriticalHealthSampleCount"] = [int]$Report.CriticalHealthSampleCount
    $summary["FocusRestoreFailureCount"] = [int]$Report.FocusRestoreFailureCount
    $summary["FocusContaminated"] = [int]$summary["FocusRestoreFailureCount"] -ge $focusContaminationThreshold
    $summary["SpellFailedMovingCount"] = [int]$Report.SpellFailedMovingCount
    $summary["BadAttackFacingCount"] = [int]$Report.BadAttackFacingCount

    $runtimeResult = Get-ApiSafeResultOrNull -Response $Report.Runtime
    $combatRuntime = $null
    $pullRuntime = $null
    if ($null -ne $runtimeResult)
    {
        $combatRuntime = $runtimeResult.Combat
        $pullRuntime = $runtimeResult.Pull
    }

    if ($null -eq $combatRuntime -or $null -eq $pullRuntime)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Combat runtime diagnostics were unavailable at the end of validation."
    }
    else
    {
        $summary["LostTargetBurstCountWindow"] = [int]$combatRuntime.LostTargetBurstCountWindow
        $summary["LostTargetReacquireAttemptCountWindow"] = [int]$combatRuntime.LostTargetReacquireAttemptCountWindow
        $summary["LostTargetReacquireSuccessCountWindow"] = [int]$combatRuntime.LostTargetReacquireSuccessCountWindow
        $summary["PullFailureSoftRetryCountWindow"] = [int]$pullRuntime.PullFailureSoftRetryCountWindow

        if ([int]$summary["LostTargetReacquireAttemptCountWindow"] -gt 0)
        {
            $summary["LostTargetReacquireSuccessRatio"] = [math]::Round(
                ([double]$summary["LostTargetReacquireSuccessCountWindow"] / [double]$summary["LostTargetReacquireAttemptCountWindow"]),
                4)
        }
    }

    $summary["SpellCoverageComplete"] =
        ([int]$summary["SpellCounts"].Immolate -gt 0) -and
        ([int]$summary["SpellCounts"].Corruption -gt 0) -and
        ([int]$summary["SpellCounts"].Shoot -gt 0)
    $summary["RangedStandoffMaintained"] =
        ([int]$summary["BodyPullFallbackCount"] -eq 0) -and
        ([int]$summary["PullFailureSoftRetryCountWindow"] -eq 0)
    $summary["RangedFillerDominant"] =
        ([int]$summary["SpellCounts"].Shoot -gt 0) -and
        ([int]$summary["SpellCounts"].Shoot -ge [int]$summary["SpellCounts"].ShadowBolt)

    if ([int]$summary["KillsDelta"] -lt [int]$summary["TargetKills"])
    {
        Add-ActionFailureReason -Summary $summary -Reason ("KillsDelta must be >= {0} but was {1}." -f [int]$summary["TargetKills"], [int]$summary["KillsDelta"])
    }

    if (-not [bool]$summary["SpellCoverageComplete"])
    {
        $missingSpells = New-Object System.Collections.Generic.List[string]
        foreach ($spellName in @("Immolate", "Corruption", "Shoot"))
        {
            if ([int]$summary["SpellCounts"][$spellName] -le 0)
            {
                [void]$missingSpells.Add($spellName)
            }
        }

        Add-ActionFailureReason -Summary $summary -Reason ("Required spell evidence missing: {0}" -f ($missingSpells -join ", "))
    }

    if ([int]$summary["PullOpenerCounts"].CurseOfAgony -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Curse of Agony pull opener evidence was not observed."
    }

    if (-not [bool]$summary["RangedStandoffMaintained"])
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Ranged standoff was not maintained: BodyPullFallbackCount={0}, PullFailureSoftRetryCountWindow={1}." -f [int]$summary["BodyPullFallbackCount"], [int]$summary["PullFailureSoftRetryCountWindow"])
    }

    if (-not [bool]$summary["RangedFillerDominant"])
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Ranged filler dominance failed: Shoot={0}, ShadowBolt={1}." -f [int]$summary["SpellCounts"].Shoot, [int]$summary["SpellCounts"].ShadowBolt)
    }

    if ([int]$summary["SpellFailedMovingCount"] -gt 2)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("SpellFailedMovingCount exceeded threshold: {0}" -f [int]$summary["SpellFailedMovingCount"])
    }

    if ([int]$summary["BadAttackFacingCount"] -ne 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("BadAttackFacingCount must be zero but was {0}." -f [int]$summary["BadAttackFacingCount"])
    }

    if ([bool]$summary["FocusContaminated"] -and
        (([int]$summary["LostTargetBurstCountWindow"] -ne 0) -or ([int]$summary["LostTargetReacquireAttemptCountWindow"] -gt 0)))
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Combat evidence was contaminated by {0} WoW focus restore failures (threshold {1})." -f [int]$summary["FocusRestoreFailureCount"], [int]$summary["FocusContaminationThreshold"])
    }

    if (-not [bool]$summary["FocusContaminated"] -and [int]$summary["LostTargetBurstCountWindow"] -ne 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("LostTargetBurstCountWindow must be zero but was {0}." -f [int]$summary["LostTargetBurstCountWindow"])
    }

    if ([int]$summary["PullFailureSoftRetryCountWindow"] -ne 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("PullFailureSoftRetryCountWindow must be zero but was {0}." -f [int]$summary["PullFailureSoftRetryCountWindow"])
    }

    if (-not [bool]$summary["FocusContaminated"] -and [int]$summary["LostTargetReacquireAttemptCountWindow"] -gt 0 -and [double]$summary["LostTargetReacquireSuccessRatio"] -lt 0.80)
    {
        Add-ActionFailureReason -Summary $summary -Reason ("Lost target reacquire success ratio must be >= 0.80 but was {0}." -f $summary["LostTargetReacquireSuccessRatio"])
    }

    $defensiveEvidenceCount =
        [int]$summary["FearCastCount"] +
        [int]$summary["DrainLifeCastCount"] +
        [int]$summary["HealthstoneUseCount"]
    if ([int]$summary["LowHealthSampleCount"] -gt 0 -and [int]$summary["DrainLifeCastCount"] -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Low-health combat samples were observed without Drain Life evidence."
    }
    if ([int]$summary["CriticalHealthSampleCount"] -gt 0 -and $defensiveEvidenceCount -le 0)
    {
        Add-ActionFailureReason -Summary $summary -Reason "Critical low-health combat samples were observed without Fear, Drain Life, or Healthstone evidence."
    }

    $summary["Passed"] = (@($summary["FailureReasons"]).Count -eq 0)
    return $summary
}

function Invoke-SoakRun
{
    Write-Info "Starting soak run (${SoakMinutes}m target, ${WindowMinutes}m windows, cadence ${EvidenceIntervalSeconds}s)"
    Assert-ServiceReadinessGate -Context "soak-start" -RequireApiHealth
    Assert-ProfileRouteReadyGate -Context "soak-start"
    Assert-CastingSnapshotReadyGate -Context "soak-start"
    if ($MaxPatchLoops -gt 0)
    {
        Write-Info "MaxPatchLoops parameter set to $MaxPatchLoops (manual triage/patch loop remains operator-driven)"
    }

    Invoke-CollectEvidence -Stage "soak-start"

    $deadline = (Get-Date).AddMinutes($SoakMinutes)
    $sampleIndex = 0
    $samples = New-Object System.Collections.Generic.List[object]
    while ((Get-Date) -lt $deadline)
    {
        $sampleIndex++
        $sample = Get-ActiveRunHealthSample
        [void]$samples.Add($sample)
        Assert-NoImmediateAbortCondition -Sample $sample
        [void](Write-ArtifactJson -Name ("{0}-soak-sample-{1:D3}.json" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $sampleIndex) -Object $sample)

        $repeatRate = $null
        $deviation = $null
        $soakResp = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/soak/current" -TimeoutSec 8
        if ($soakResp.Success -and $null -ne $soakResp.Result)
        {
            $repeatRate = $soakResp.Result.CurrentWindowRepeatStuckRate
            $deviation = $soakResp.Result.CurrentWindowMaxRouteDeviation
            [void](Write-ArtifactJson -Name ("{0}-soak-current-{1:D3}.json" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $sampleIndex) -Object $soakResp.Result)
        }

        Write-Info ("Soak sample {0}: Goal={1} RepeatRate={2} MaxDev={3}" -f
            $sampleIndex,
            $(if ($sample.BotStatus) { $sample.BotStatus.CurrentGoal } else { "n/a" }),
            $(if ($null -ne $repeatRate) { [string]([math]::Round([double]$repeatRate, 4)) } else { "n/a" }),
            $(if ($null -ne $deviation) { [string]([math]::Round([double]$deviation, 2)) } else { "n/a" }))

        $remaining = [int][Math]::Ceiling(($deadline - (Get-Date)).TotalSeconds)
        if ($remaining -le 0) { break }
        Start-Sleep -Seconds ([Math]::Min($EvidenceIntervalSeconds, [Math]::Max(1, $remaining)))
    }

    Invoke-CollectEvidence -Stage "soak-end" -FlushSoak
    $soakCurrent = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/soak/current" -TimeoutSec 8
    $soakArtifact = Get-ChildItem -LiteralPath $script:LogsDir -Filter "soak-nav-*.json" -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc -Descending |
        Select-Object -First 1
    $soakArtifactPath = $null
    $soakArtifactContent = $null
    if ($null -ne $soakArtifact)
    {
        $soakArtifactPath = $soakArtifact.FullName
        $soakArtifactContent = Read-JsonArtifact -Path $soakArtifactPath
    }

    Write-Ok "Soak run completed"
    return [pscustomobject]@{
        SampleCount = $samples.Count
        Samples = @($samples)
        FinalSoak = $soakCurrent
        SoakArtifactPath = $soakArtifactPath
        SoakArtifact = $soakArtifactContent
    }
}

function Get-RerouteProbeContext
{
    $resp = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/reroute" -TimeoutSec 8
    if (-not $resp.Success -or $null -eq $resp.Result -or $null -eq $resp.Result.Reroute)
    {
        throw "Reroute diagnostics unavailable: $($resp.Error)"
    }

    $reroute = $resp.Result.Reroute
    if ($null -eq $reroute.MapId -or $null -eq $reroute.CurrentPosition -or $null -eq $reroute.ProbeTarget)
    {
        throw "Reroute diagnostics did not expose deterministic probe data (MapId/CurrentPosition/ProbeTarget)."
    }

    return [pscustomobject]@{
        Snapshot = $resp.Result
        MapId = [int]$reroute.MapId
        CurrentPosition = $reroute.CurrentPosition
        ProbeTarget = $reroute.ProbeTarget
    }
}

function Get-RerouteInjectionPlan
{
    param([Parameter(Mandatory = $true)][object]$ProbeContext)

    $currentX = [double]$ProbeContext.CurrentPosition.X
    $currentY = [double]$ProbeContext.CurrentPosition.Y
    $currentZ = [double]$ProbeContext.CurrentPosition.Z
    $targetX = [double]$ProbeContext.ProbeTarget.X
    $targetY = [double]$ProbeContext.ProbeTarget.Y
    $targetZ = [double]$ProbeContext.ProbeTarget.Z

    $probeDistanceXY = [math]::Sqrt((($targetX - $currentX) * ($targetX - $currentX)) + (($targetY - $currentY) * ($targetY - $currentY)))
    $probeZDelta = [math]::Abs($targetZ - $currentZ)
    $zCorrected = $false
    if ($probeZDelta -gt 10.0 -or [math]::Abs($targetZ) -le 0.01)
    {
        $targetZ = $currentZ
        $probeZDelta = [math]::Abs($targetZ - $currentZ)
        $zCorrected = $true
    }

    if ($probeDistanceXY -lt 1.0)
    {
        throw ("Reroute probe geometry is invalid: probe XY distance {0:N2} is too short." -f $probeDistanceXY)
    }

    $directionX = ($targetX - $currentX) / $probeDistanceXY
    $directionY = ($targetY - $currentY) / $probeDistanceXY
    $effectiveDistance = [math]::Max(18.0, [math]::Min($probeDistanceXY, 40.0))
    $normalizedTarget = [ordered]@{
        X = [math]::Round(($currentX + ($directionX * $effectiveDistance)), 3)
        Y = [math]::Round(($currentY + ($directionY * $effectiveDistance)), 3)
        Z = [math]::Round($targetZ, 3)
    }

    $corridorFractions = @(0.35, 0.5, 0.65)
    $corridorPoints = @()
    foreach ($fraction in $corridorFractions)
    {
        $corridorPoints += [pscustomobject]@{
            X = [math]::Round(($currentX + (($normalizedTarget.X - $currentX) * $fraction)), 3)
            Y = [math]::Round(($currentY + (($normalizedTarget.Y - $currentY) * $fraction)), 3)
            Z = [math]::Round(($currentZ + (($normalizedTarget.Z - $currentZ) * $fraction)), 3)
        }
    }

    return [pscustomobject]@{
        CurrentPosition = [pscustomobject]@{
            X = [math]::Round($currentX, 3)
            Y = [math]::Round($currentY, 3)
            Z = [math]::Round($currentZ, 3)
        }
        OriginalProbeTarget = $ProbeContext.ProbeTarget
        NormalizedProbeTarget = [pscustomobject]$normalizedTarget
        ProbeDistanceXY = [math]::Round($probeDistanceXY, 3)
        ProbeZDelta = [math]::Round($probeZDelta, 3)
        ZCorrected = $zCorrected
        CorridorPoints = @($corridorPoints)
    }
}

function Get-PointToSegmentDistance3D
{
    param(
        [Parameter(Mandatory = $true)][object]$SegmentStart,
        [Parameter(Mandatory = $true)][object]$SegmentEnd,
        [Parameter(Mandatory = $true)][object]$Point
    )

    $start = [System.Numerics.Vector3]::new([float]$SegmentStart.X, [float]$SegmentStart.Y, [float]$SegmentStart.Z)
    $end = [System.Numerics.Vector3]::new([float]$SegmentEnd.X, [float]$SegmentEnd.Y, [float]$SegmentEnd.Z)
    $pointVector = [System.Numerics.Vector3]::new([float]$Point.X, [float]$Point.Y, [float]$Point.Z)
    $segment = $end - $start
    $lengthSquared = $segment.LengthSquared()

    if ($lengthSquared -le 0.0001)
    {
        return [math]::Round([double][System.Numerics.Vector3]::Distance($start, $pointVector), 3)
    }

    $projection = [System.Numerics.Vector3]::Dot(($pointVector - $start), $segment) / $lengthSquared
    $clampedProjection = [math]::Max(0.0, [math]::Min(1.0, $projection))
    $closest = $start + ($segment * [float]$clampedProjection)
    return [math]::Round([double][System.Numerics.Vector3]::Distance($closest, $pointVector), 3)
}

function Invoke-HazardClusterInjectionFromProbe
{
    param([Parameter(Mandatory = $true)][object]$ProbeContext)

    $mapId = [int]$ProbeContext.MapId
    $plan = Get-RerouteInjectionPlan -ProbeContext $ProbeContext
    $current = $plan.CurrentPosition
    $target = $plan.NormalizedProbeTarget

    $clear = Invoke-AgentApiSafe -Method POST -Path "/api/debug/hazards/$mapId/clear" -Body @{} -TimeoutSec 8
    if (-not $clear.Success)
    {
        throw "Failed to clear hazards for map ${mapId}: $($clear.Error)"
    }

    $injectResults = @()
    foreach ($corridorPoint in @($plan.CorridorPoints))
    {
        $injectBody = [ordered]@{
            x = $corridorPoint.X
            y = $corridorPoint.Y
            z = $corridorPoint.Z
            uiMapId = $mapId
            type = 99
            count = 4
            zone = "Agent-BotControl-Reroute"
            ageMinutes = 1
        }

        $inject = Invoke-AgentApiSafe -Method POST -Path "/api/debug/hazards/$mapId/inject" -Body $injectBody -TimeoutSec 8
        if (-not $inject.Success)
        {
            throw "Failed to inject hazards for map ${mapId}: $($inject.Error)"
        }

        $injectResults += @($inject.Result)
    }

    $cluster = Invoke-AgentApiSafe -Method POST -Path "/api/debug/hazards/$mapId/cluster" -Body @{} -TimeoutSec 8
    if (-not $cluster.Success)
    {
        throw "Failed to cluster hazards for map ${mapId}: $($cluster.Error)"
    }

    return [pscustomobject]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        MapId = $mapId
        CurrentPosition = $current
        ProbeTarget = $target
        OriginalProbeTarget = $plan.OriginalProbeTarget
        ProbeDistanceXY = $plan.ProbeDistanceXY
        ProbeZDelta = $plan.ProbeZDelta
        ZCorrected = $plan.ZCorrected
        CorridorPoints = @($plan.CorridorPoints)
        Inject = @($injectResults)
        Cluster = $cluster.Result
    }
}

function Get-RerouteHazardSnapshotValidation
{
    param([Parameter(Mandatory = $true)][object]$InjectionResult)

    $mapId = [int]$InjectionResult.MapId
    $snapshot = Invoke-AgentApiSafe -Method GET -Path "/api/debug/hazards/${mapId}?includeEvents=true&includeClusters=true&maxEvents=20&maxClusters=20&mostRecentFirst=true" -TimeoutSec 8
    if (-not $snapshot.Success -or $null -eq $snapshot.Result)
    {
        throw "Failed to capture hazard snapshot for map ${mapId}: $($snapshot.Error)"
    }

    $clusters = @($snapshot.Result.Clusters)
    $bestCluster = $null
    $bestDistance = [double]::PositiveInfinity
    foreach ($cluster in $clusters)
    {
        if ($null -eq $cluster.Centroid)
        {
            continue
        }

        $distance = Get-PointToSegmentDistance3D -SegmentStart $InjectionResult.CurrentPosition -SegmentEnd $InjectionResult.ProbeTarget -Point $cluster.Centroid
        if ($distance -lt $bestDistance)
        {
            $bestDistance = $distance
            $bestCluster = $cluster
        }
    }

    $passed = ($clusters.Count -gt 0) -and $bestDistance -le 30.0
    $failureReason = $null
    if ($clusters.Count -le 0)
    {
        $failureReason = "Synthetic hazard clustering produced zero clusters."
    }
    elseif ($bestDistance -gt 30.0)
    {
        $failureReason = ("Synthetic hazard centroid did not intersect the evaluated route segment (distance={0:N2})." -f $bestDistance)
    }

    return [pscustomobject]@{
        MapId = $mapId
        ClusterCount = $clusters.Count
        Passed = $passed
        BestCluster = $bestCluster
        CentroidToSegmentDistance = $(if ([double]::IsPositiveInfinity($bestDistance)) { $null } else { [math]::Round($bestDistance, 3) })
        Snapshot = $snapshot.Result
        FailureReason = $failureReason
    }
}

function Merge-ReasonCountMaps
{
    param([AllowNull()][object[]]$Maps)

    $merged = [ordered]@{}
    foreach ($map in @($Maps))
    {
        if ($null -eq $map)
        {
            continue
        }

        if ($map -is [System.Collections.IDictionary])
        {
            $entries = $map.GetEnumerator()
        }
        else
        {
            $entries = $map.PSObject.Properties
        }

        foreach ($entry in $entries)
        {
            $name = $(if ($entry -is [System.Collections.DictionaryEntry]) { "$($entry.Key)" } else { "$($entry.Name)" })
            if ([string]::IsNullOrWhiteSpace($name))
            {
                continue
            }

            $value = [int]$entry.Value
            if (-not $merged.Contains($name))
            {
                $merged[$name] = 0
            }

            $merged[$name] = [int]$merged[$name] + $value
        }
    }

    return $merged
}

function Get-DominantReasonCount
{
    param([AllowNull()][object]$Counts)

    $dominantName = $null
    $dominantCount = 0
    if ($null -eq $Counts)
    {
        return [pscustomobject]@{
            Name = $null
            Count = 0
        }
    }

    if ($Counts -is [System.Collections.IDictionary])
    {
        $entries = $Counts.GetEnumerator()
    }
    else
    {
        $entries = $Counts.PSObject.Properties
    }

    foreach ($entry in $entries)
    {
        $value = [int]$entry.Value
        if ($value -gt $dominantCount)
        {
            $dominantName = $(if ($entry -is [System.Collections.DictionaryEntry]) { "$($entry.Key)" } else { "$($entry.Name)" })
            $dominantCount = $value
        }
    }

    return [pscustomobject]@{
        Name = $dominantName
        Count = $dominantCount
    }
}

function Get-RerouteReadinessEvaluation
{
    param(
        [AllowNull()][object]$BotStatus,
        [AllowNull()][object]$Snapshot,
        [AllowNull()][object]$LaunchStatus,
        [AllowNull()][object]$SessionStats,
        [int]$BaselineDeaths = -1,
        [AllowNull()][string[]]$TransientGoalPrefixes = @("Pull", "Combat", "Flee", "Walk To Corpse", "Loot", "Conditional Wait")
    )

    $goal = ""
    if ($null -ne $BotStatus -and $null -ne $BotStatus.CurrentGoal)
    {
        $goal = "$($BotStatus.CurrentGoal)".Trim()
    }

    $launchReady = ($null -ne $LaunchStatus) -and [bool]$LaunchStatus.IsLaunchReady -and [bool]$LaunchStatus.CanStartBot
    $isActive = ($null -ne $BotStatus) -and [bool]$BotStatus.IsActive
    $snapshotAvailable = $null -ne $Snapshot
    $sessionStatsAvailable = $null -ne $SessionStats
    $currentDeaths = if ($sessionStatsAvailable -and $null -ne $SessionStats.Deaths) { [int]$SessionStats.Deaths } else { $BaselineDeaths }
    $deathsIncreased = ($BaselineDeaths -ge 0) -and ($currentDeaths -gt $BaselineDeaths)

    $goalIsFollow = -not [string]::IsNullOrWhiteSpace($goal) -and $goal.StartsWith("Follow", [System.StringComparison]::OrdinalIgnoreCase)
    $goalIsCorpse = -not [string]::IsNullOrWhiteSpace($goal) -and $goal.IndexOf("Corpse", [System.StringComparison]::OrdinalIgnoreCase) -ge 0
    $goalIsCombat = -not [string]::IsNullOrWhiteSpace($goal) -and (
        $goal.StartsWith("Combat", [System.StringComparison]::OrdinalIgnoreCase) -or
        $goal.StartsWith("Pull", [System.StringComparison]::OrdinalIgnoreCase) -or
        $goal.StartsWith("Flee", [System.StringComparison]::OrdinalIgnoreCase))

    $goalIsTransient = $false
    foreach ($prefix in @($TransientGoalPrefixes))
    {
        if ([string]::IsNullOrWhiteSpace($prefix))
        {
            continue
        }

        if (-not [string]::IsNullOrWhiteSpace($goal) -and $goal.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase))
        {
            $goalIsTransient = $true
            break
        }
    }

    $isDead = $snapshotAvailable -and [bool]$Snapshot.Dead
    $isInCombat = $snapshotAvailable -and [bool]$Snapshot.InCombat

    $rejectionReason = $null
    $liveStateContaminated = $false
    if (-not $isActive -or -not $launchReady -or -not $snapshotAvailable -or -not $sessionStatsAvailable)
    {
        $rejectionReason = "api/readiness failure"
    }
    elseif ($deathsIncreased -or $isDead -or $goalIsCorpse)
    {
        $rejectionReason = "death/corpse contamination"
        $liveStateContaminated = $true
    }
    elseif ($isInCombat -or $goalIsCombat)
    {
        $rejectionReason = "combat contamination"
    }
    elseif ($goalIsTransient -or -not $goalIsFollow)
    {
        $rejectionReason = "goal contamination"
    }

    return [pscustomobject]@{
        Goal = $goal
        IsActive = $isActive
        LaunchReady = $launchReady
        BaselineDeaths = $BaselineDeaths
        CurrentDeaths = $currentDeaths
        DeathsIncreased = $deathsIncreased
        RejectionReason = $rejectionReason
        LiveStateContaminated = $liveStateContaminated
        SamplePassed = [string]::IsNullOrWhiteSpace($rejectionReason)
    }
}

function Wait-ForSustainedFollowRoute
{
    param(
        [int]$TimeoutSec = 180,
        [int]$RequiredConsecutiveSamples = 8,
        [int]$CadenceMs = 1000,
        [int]$InitialDeaths = -1,
        [string]$GateName = "reroute-ready"
    )

    $deadline = (Get-Date).AddSeconds([Math]::Max(15, $TimeoutSec))
    $samples = New-Object System.Collections.Generic.List[object]
    $consecutiveFollowSamples = 0
    $baselineDeaths = $InitialDeaths
    $rejectedSampleCountsByReason = [ordered]@{}
    $liveStateContaminated = $false

    while ((Get-Date) -lt $deadline)
    {
        $stamp = (Get-Date).ToUniversalTime().ToString("o")
        $bot = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
        $snap = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 8
        $nav = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
        $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 5
        $session = Invoke-AgentApiSafe -Method GET -Path "/api/session/stats" -TimeoutSec 5

        $snapshot = $null
        if ($snap.Success -and $null -ne $snap.Result -and $snap.Result.Success)
        {
            $snapshot = $snap.Result.Data.snapshot
        }

        $launchStatus = Get-ApiSafeResultOrNull -Response $launch
        $sessionStats = Get-ApiSafeResultOrNull -Response $session
        if ($baselineDeaths -lt 0 -and $null -ne $sessionStats -and $null -ne $sessionStats.Deaths)
        {
            $baselineDeaths = [int]$sessionStats.Deaths
        }

        $evaluation = Get-RerouteReadinessEvaluation -BotStatus (Get-ApiSafeResultOrNull -Response $bot) -Snapshot $snapshot -LaunchStatus $launchStatus -SessionStats $sessionStats -BaselineDeaths $baselineDeaths
        $samplePassed = [bool]$evaluation.SamplePassed

        if ($samplePassed)
        {
            $consecutiveFollowSamples++
        }
        else
        {
            $consecutiveFollowSamples = 0
        }

        if (-not [string]::IsNullOrWhiteSpace($evaluation.RejectionReason))
        {
            if (-not $rejectedSampleCountsByReason.Contains($evaluation.RejectionReason))
            {
                $rejectedSampleCountsByReason[$evaluation.RejectionReason] = 0
            }

            $rejectedSampleCountsByReason[$evaluation.RejectionReason] = [int]$rejectedSampleCountsByReason[$evaluation.RejectionReason] + 1
        }

        if ([bool]$evaluation.LiveStateContaminated)
        {
            $liveStateContaminated = $true
        }

        $sample = [ordered]@{
            TimestampUtc = $stamp
            GateName = $GateName
            Goal = $evaluation.Goal
            BotStatus = $(if ($bot.Success) { $bot.Result } else { $null })
            Snapshot = $snapshot
            NavigationRuntime = $(if ($nav.Success) { $nav.Result } else { $null })
            LaunchStatus = $launchStatus
            SessionStats = $sessionStats
            SamplePassed = $samplePassed
            RejectionReason = $evaluation.RejectionReason
            LiveStateContaminated = [bool]$evaluation.LiveStateContaminated
            ConsecutiveFollowSamples = $consecutiveFollowSamples
            BaselineDeaths = $baselineDeaths
            CurrentDeaths = $evaluation.CurrentDeaths
        }

        [void]$samples.Add($sample)

        if ($samplePassed -and $consecutiveFollowSamples -ge [Math]::Max(2, $RequiredConsecutiveSamples))
        {
            [object[]]$sampleArray = $samples.ToArray()
            return [pscustomobject]@{
                Completed = $true
                RequiredConsecutiveSamples = [Math]::Max(2, $RequiredConsecutiveSamples)
                FinalConsecutiveFollowSamples = $consecutiveFollowSamples
                FailureReason = $null
                DominantRejectionReason = $null
                LiveStateContaminated = $liveStateContaminated
                InitialDeaths = $baselineDeaths
                FinalDeaths = $evaluation.CurrentDeaths
                RejectedSampleCountsByReason = $rejectedSampleCountsByReason
                Samples = $sampleArray
                Final = $sample
            }
        }

        Start-Sleep -Milliseconds ([Math]::Max(250, $CadenceMs))
    }

    [object[]]$sampleArray = $samples.ToArray()
    $dominantRejection = Get-DominantReasonCount -Counts $rejectedSampleCountsByReason
    $failureReason = if ($liveStateContaminated)
    {
        "Invalid live state for reroute proof: death/corpse contamination prevented a clean FollowRoute window."
    }
    elseif (-not [string]::IsNullOrWhiteSpace($dominantRejection.Name))
    {
        "No sustained FollowRoute window was observed; dominant rejection reason was '{0}' ({1} samples)." -f $dominantRejection.Name, $dominantRejection.Count
    }
    else
    {
        "No sustained FollowRoute window was observed before timeout."
    }

    return [pscustomobject]@{
        Completed = $false
        RequiredConsecutiveSamples = [Math]::Max(2, $RequiredConsecutiveSamples)
        FinalConsecutiveFollowSamples = $consecutiveFollowSamples
        FailureReason = $failureReason
        DominantRejectionReason = $dominantRejection.Name
        LiveStateContaminated = $liveStateContaminated
        InitialDeaths = $baselineDeaths
        FinalDeaths = $(if ($sampleArray.Length -gt 0) { $sampleArray[$sampleArray.Length - 1].CurrentDeaths } else { $baselineDeaths })
        RejectedSampleCountsByReason = $rejectedSampleCountsByReason
        Samples = $sampleArray
        Final = $(if ($sampleArray.Length -gt 0) { $sampleArray[$sampleArray.Length - 1] } else { $null })
    }
}

function Invoke-ReroutePreflight
{
    param(
        [int]$TimeoutSec = 120,
        [int]$CadenceMs = 1000
    )

    $gate = Wait-ForSustainedFollowRoute -TimeoutSec ([Math]::Max(30, $TimeoutSec)) -RequiredConsecutiveSamples 3 -CadenceMs $CadenceMs -GateName "reroute-preflight"
    return [pscustomobject]@{
        Completed = [bool]$gate.Completed
        FailureReason = $(if ($gate.Completed) { $null } else { $gate.FailureReason })
        Gate = $gate
        InitialDeaths = $gate.InitialDeaths
        FinalDeaths = $gate.FinalDeaths
        LiveStateContaminated = [bool]$gate.LiveStateContaminated
        RejectedSampleCountsByReason = $gate.RejectedSampleCountsByReason
    }
}

function Wait-ForRerouteEvidence
{
    param(
        [int]$TimeoutSec = 45,
        [int]$InitialTriggerCount = 0,
        [int]$InitialApplyCount = 0,
        [int]$InitialDropCount = 0,
        [switch]$RequireTrigger,
        [switch]$RequireApply,
        [switch]$RequireDrop
    )

    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    $snapshots = New-Object System.Collections.Generic.List[object]

    $requireTriggerCheck = $RequireTrigger.IsPresent
    $requireApplyCheck = $RequireApply.IsPresent
    $requireDropCheck = $RequireDrop.IsPresent
    if (-not $requireTriggerCheck -and -not $requireApplyCheck -and -not $requireDropCheck)
    {
        $requireTriggerCheck = $true
        $requireApplyCheck = $true
        $requireDropCheck = $true
    }

    while ((Get-Date) -lt $deadline)
    {
        $resp = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/reroute" -TimeoutSec 8
        if ($resp.Success -and $null -ne $resp.Result)
        {
            [void]$snapshots.Add($resp.Result)

            $reroute = $resp.Result.Reroute
            if ($null -ne $reroute)
            {
                $triggered = if ($requireTriggerCheck) { [int]$reroute.RerouteTriggerCount -gt $InitialTriggerCount } else { $true }
                $applied = if ($requireApplyCheck) { [int]$reroute.RerouteApplyCount -gt $InitialApplyCount } else { $true }
                $dropped = if ($requireDropCheck) { [int]$reroute.RerouteDropCount -gt $InitialDropCount } else { $true }
                if ($triggered -and $applied -and $dropped)
                {
                    [object[]]$snapshotArray = $snapshots.ToArray()
                    return [pscustomobject]@{
                        Completed = $true
                        Snapshots = $snapshotArray
                        Final = $resp.Result
                    }
                }
            }
        }

        Start-Sleep -Milliseconds 1000
    }

    [object[]]$snapshotArray = $snapshots.ToArray()
    return [pscustomobject]@{
        Completed = $false
        Snapshots = $snapshotArray
        Final = $(if ($snapshotArray.Length -gt 0) { $snapshotArray[$snapshotArray.Length - 1] } else { $null })
    }
}

function Invoke-RerouteValidation
{
    $report = [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        RequestedProfile = $NavProfile
        AppliedProfiles = @("stable-live", "triage-hazard")
        Preflight = $null
        PreflightPassed = $false
        StableFollowGate = $null
        StableGateFailureReason = $null
        StableWatch = $null
        HazardFollowGate = $null
        HazardFollowGateAttempts = @()
        HazardGateFailureReason = $null
        RearmAttempts = 0
        HazardWatch = $null
        Probe = $null
        Injection = $null
        HazardSnapshotValidation = $null
        ProbeDistanceXY = 0
        ProbeZDelta = 0
        HazardSnapshotValid = $false
        InjectedCentroid = $null
        CentroidToSegmentDistance = $null
        TriggerApplyObserved = $false
        DropObserved = $false
        RerouteEvidenceClosed = $false
        PreReloadReroute = $null
        PostReloadReroute = $null
        FinalReroute = $null
        TriggerCount = 0
        ApplyCount = 0
        DropCount = 0
        DetourOnlyCollapseCount = 0
        RejectedSampleCountsByReason = [ordered]@{}
        LiveStateContaminated = $false
        Snapshots = @()
    }
    $summary = $null
    try
    {
        Ensure-SessionArtifactDir | Out-Null
        $baseline = New-SessionManifestBaseline
        Write-SessionManifest -Baseline $baseline
        Assert-ServiceReadinessGate -Context "reroute-validation-start" -RequireApiHealth
        Assert-ProfileRouteReadyGate -Context "reroute-validation-start"
        Assert-CastingSnapshotReadyGate -Context "reroute-validation-start"

        if (-not $script:FeatureFlagSnapshotActive)
        {
            Save-FeatureFlagSnapshot | Out-Null
        }

        $rerouteWatchDurationSeconds = 180

        Apply-FeatureFlagProfile -ProfileName "stable-live" -VerifyViaApi
        $preflight = Invoke-ReroutePreflight -TimeoutSec ([Math]::Max(60, $ValidationTimeoutSeconds)) -CadenceMs $WatchCadenceMs
        $report["Preflight"] = $preflight
        $report["PreflightPassed"] = [bool]$preflight.Completed
        [void](Write-ArtifactJson -Name ("{0}-reroute-preflight.json" -f $script:RunTag) -Object $preflight)
        if (-not $preflight.Completed)
        {
            $report["StableGateFailureReason"] = $preflight.FailureReason
            $report["LiveStateContaminated"] = [bool]$preflight.LiveStateContaminated
            $report["RejectedSampleCountsByReason"] = Merge-ReasonCountMaps -Maps @($preflight.RejectedSampleCountsByReason)
            throw "$($preflight.FailureReason)"
        }

        $stableFollowGate = Wait-ForSustainedFollowRoute -TimeoutSec ([Math]::Max(90, $ValidationTimeoutSeconds)) -RequiredConsecutiveSamples 8 -CadenceMs $WatchCadenceMs -InitialDeaths $preflight.FinalDeaths -GateName "stable-followroute"
        $report["StableFollowGate"] = $stableFollowGate
        $report["StableGateFailureReason"] = $stableFollowGate.FailureReason
        $report["LiveStateContaminated"] = [bool]$report["LiveStateContaminated"] -or [bool]$stableFollowGate.LiveStateContaminated
        $report["RejectedSampleCountsByReason"] = Merge-ReasonCountMaps -Maps @($report["RejectedSampleCountsByReason"], $stableFollowGate.RejectedSampleCountsByReason)
        [void](Write-ArtifactJson -Name ("{0}-stable-followroute-gate.json" -f $script:RunTag) -Object $stableFollowGate)
        if (-not $stableFollowGate.Completed)
        {
            throw "$($stableFollowGate.FailureReason)"
        }

        $stableWatch = Invoke-WatchNav -DurationSeconds $rerouteWatchDurationSeconds -CadenceMs $WatchCadenceMs -RequiredAcceptedFollowSamples 60
        $report["StableWatch"] = $stableWatch
        if (-not [bool]$stableWatch.NavigationValidationValid)
        {
            $report["StableGateFailureReason"] = $stableWatch.InvalidReason
            throw "$($stableWatch.InvalidReason)"
        }

        Apply-FeatureFlagProfile -ProfileName "triage-hazard" -VerifyViaApi
        Write-SessionManifest -Baseline $baseline -IncludeRuntimeState
        $hazardFollowGate = $null
        $hazardGateAttempts = New-Object System.Collections.Generic.List[object]
        $hazardInitialDeaths = $stableFollowGate.FinalDeaths
        $maxHazardRearmAttempts = 3
        for ($attempt = 1; $attempt -le $maxHazardRearmAttempts; $attempt++)
        {
            $gateAttempt = Wait-ForSustainedFollowRoute -TimeoutSec ([Math]::Max(90, $ValidationTimeoutSeconds)) -RequiredConsecutiveSamples 8 -CadenceMs $WatchCadenceMs -InitialDeaths $hazardInitialDeaths -GateName ("hazard-followroute-attempt-{0}" -f $attempt)
            [void]$hazardGateAttempts.Add($gateAttempt)
            [void](Write-ArtifactJson -Name ("{0}-hazard-followroute-gate-attempt-{1}.json" -f $script:RunTag, $attempt) -Object $gateAttempt)

            if ($gateAttempt.Completed)
            {
                $hazardFollowGate = $gateAttempt
                break
            }

            $hazardFollowGate = $gateAttempt
            if ([bool]$gateAttempt.LiveStateContaminated -or $gateAttempt.DominantRejectionReason -eq "api/readiness failure")
            {
                break
            }

            if ($attempt -lt $maxHazardRearmAttempts)
            {
                Start-Sleep -Seconds 10
                Assert-ServiceReadinessGate -Context ("reroute-validation-rearm-{0}" -f $attempt) -RequireApiHealth
                Assert-ProfileRouteReadyGate -Context ("reroute-validation-rearm-{0}" -f $attempt)
            }
        }

        $hazardGateAttemptsArray = $hazardGateAttempts.ToArray()
        $hazardRejectedCounts = @()
        foreach ($attemptResult in $hazardGateAttemptsArray)
        {
            $hazardRejectedCounts += @($attemptResult.RejectedSampleCountsByReason)
        }

        $hazardFollowGateAggregate = [ordered]@{
            Completed = [bool]($null -ne $hazardFollowGate -and $hazardFollowGate.Completed)
            FailureReason = $(if ($null -ne $hazardFollowGate) { $hazardFollowGate.FailureReason } else { "Hazard reroute validation did not execute." })
            RearmAttempts = [Math]::Max(0, $hazardGateAttemptsArray.Length - 1)
            Attempts = @($hazardGateAttemptsArray)
            Final = $(if ($null -ne $hazardFollowGate) { $hazardFollowGate.Final } else { $null })
            Samples = $(if ($null -ne $hazardFollowGate) { $hazardFollowGate.Samples } else { @() })
            RejectedSampleCountsByReason = (Merge-ReasonCountMaps -Maps $hazardRejectedCounts)
            LiveStateContaminated = [bool](($hazardGateAttemptsArray | Where-Object { $_.LiveStateContaminated }).Count -gt 0)
            InitialDeaths = $hazardInitialDeaths
            FinalDeaths = $(if ($null -ne $hazardFollowGate) { $hazardFollowGate.FinalDeaths } else { $hazardInitialDeaths })
            DominantRejectionReason = $(if ($null -ne $hazardFollowGate) { $hazardFollowGate.DominantRejectionReason } else { $null })
        }
        $report["HazardFollowGate"] = $hazardFollowGateAggregate
        $report["HazardFollowGateAttempts"] = @($hazardGateAttemptsArray)
        $report["HazardGateFailureReason"] = $hazardFollowGateAggregate.FailureReason
        $report["RearmAttempts"] = [int]$hazardFollowGateAggregate.RearmAttempts
        $report["LiveStateContaminated"] = [bool]$report["LiveStateContaminated"] -or [bool]$hazardFollowGateAggregate.LiveStateContaminated
        $report["RejectedSampleCountsByReason"] = Merge-ReasonCountMaps -Maps @($report["RejectedSampleCountsByReason"], $hazardFollowGateAggregate.RejectedSampleCountsByReason)
        [void](Write-ArtifactJson -Name ("{0}-hazard-followroute-gate.json" -f $script:RunTag) -Object $hazardFollowGateAggregate)
        if (-not [bool]$hazardFollowGateAggregate.Completed)
        {
            throw "$($hazardFollowGateAggregate.FailureReason)"
        }

        $probe = Get-RerouteProbeContext
        $report["Probe"] = $probe
        $injection = Invoke-HazardClusterInjectionFromProbe -ProbeContext $probe
        $report["Injection"] = $injection
        [void](Write-ArtifactJson -Name ("{0}-reroute-injection.json" -f $script:RunTag) -Object $injection)
        $hazardSnapshotValidation = Get-RerouteHazardSnapshotValidation -InjectionResult $injection
        $report["HazardSnapshotValidation"] = $hazardSnapshotValidation
        $report["ProbeDistanceXY"] = [double]$injection.ProbeDistanceXY
        $report["ProbeZDelta"] = [double]$injection.ProbeZDelta
        $report["HazardSnapshotValid"] = [bool]$hazardSnapshotValidation.Passed
        $report["InjectedCentroid"] = $(if ($null -ne $hazardSnapshotValidation.BestCluster -and $null -ne $hazardSnapshotValidation.BestCluster.Centroid) { $hazardSnapshotValidation.BestCluster.Centroid } else { $null })
        $report["CentroidToSegmentDistance"] = $hazardSnapshotValidation.CentroidToSegmentDistance
        [void](Write-ArtifactJson -Name ("{0}-reroute-hazard-snapshot-validation.json" -f $script:RunTag) -Object $hazardSnapshotValidation)
        if (-not $hazardSnapshotValidation.Passed)
        {
            throw "$($hazardSnapshotValidation.FailureReason)"
        }

        $hazardWatch = Invoke-WatchNav -DurationSeconds $rerouteWatchDurationSeconds -CadenceMs $WatchCadenceMs -RequiredAcceptedFollowSamples 60
        $report["HazardWatch"] = $hazardWatch
        if (-not [bool]$hazardWatch.NavigationValidationValid)
        {
            $report["HazardGateFailureReason"] = $hazardWatch.InvalidReason
            throw "$($hazardWatch.InvalidReason)"
        }
        $triggerApplyWait = Wait-ForRerouteEvidence -TimeoutSec ([Math]::Max(45, $ValidationTimeoutSeconds)) -RequireTrigger -RequireApply
        [void](Write-ArtifactJson -Name ("{0}-reroute-trigger-apply-wait.json" -f $script:RunTag) -Object $triggerApplyWait)

        $preReloadReroute = $(if ($null -ne $triggerApplyWait.Final) { $triggerApplyWait.Final.Reroute } else { $null })
        $dropWait = [pscustomobject]@{
            Completed = $false
            Snapshots = @()
            Final = $null
        }
        if ($triggerApplyWait.Completed)
        {
            $dropBaseline = if ($null -ne $preReloadReroute) { [int]$preReloadReroute.RerouteDropCount } else { 0 }
            Start-Sleep -Seconds 3
            Load-BotProfile
            $dropWait = Wait-ForRerouteEvidence -TimeoutSec ([Math]::Max(45, $ValidationTimeoutSeconds)) -InitialDropCount $dropBaseline -RequireDrop
            [void](Write-ArtifactJson -Name ("{0}-reroute-drop-wait.json" -f $script:RunTag) -Object $dropWait)
        }

        Invoke-CollectEvidence -Stage "reroute-validation-end"

        $postReloadReroute = $(if ($null -ne $dropWait.Final) { $dropWait.Final.Reroute } else { $null })
        $finalReroute = $(if ($null -ne $postReloadReroute) { $postReloadReroute } else { $preReloadReroute })
        $triggerCount = [Math]::Max($(if ($null -ne $preReloadReroute) { [int]$preReloadReroute.RerouteTriggerCount } else { 0 }), $(if ($null -ne $postReloadReroute) { [int]$postReloadReroute.RerouteTriggerCount } else { 0 }))
        $applyCount = [Math]::Max($(if ($null -ne $preReloadReroute) { [int]$preReloadReroute.RerouteApplyCount } else { 0 }), $(if ($null -ne $postReloadReroute) { [int]$postReloadReroute.RerouteApplyCount } else { 0 }))
        $dropCount = [Math]::Max($(if ($null -ne $preReloadReroute) { [int]$preReloadReroute.RerouteDropCount } else { 0 }), $(if ($null -ne $postReloadReroute) { [int]$postReloadReroute.RerouteDropCount } else { 0 }))
        $detourOnlyCollapseCount = [Math]::Max($(if ($null -ne $preReloadReroute) { [int]$preReloadReroute.DetourOnlyCollapseCount } else { 0 }), $(if ($null -ne $postReloadReroute) { [int]$postReloadReroute.DetourOnlyCollapseCount } else { 0 }))
        $combinedSnapshots = @()
        if ($null -ne $triggerApplyWait -and $null -ne $triggerApplyWait.Snapshots)
        {
            $combinedSnapshots += @($triggerApplyWait.Snapshots)
        }
        if ($null -ne $dropWait -and $null -ne $dropWait.Snapshots)
        {
            $combinedSnapshots += @($dropWait.Snapshots)
        }

        $report["TriggerApplyObserved"] = [bool]$triggerApplyWait.Completed
        $report["DropObserved"] = [bool]$dropWait.Completed
        $report["RerouteEvidenceClosed"] = [bool]($triggerApplyWait.Completed -and $dropWait.Completed)
        $report["PreReloadReroute"] = $preReloadReroute
        $report["PostReloadReroute"] = $postReloadReroute
        $report["FinalReroute"] = $finalReroute
        $report["TriggerCount"] = $triggerCount
        $report["ApplyCount"] = $applyCount
        $report["DropCount"] = $dropCount
        $report["DetourOnlyCollapseCount"] = $detourOnlyCollapseCount
        $report["Snapshots"] = @($combinedSnapshots)

        [void](Write-ArtifactJson -Name ("{0}-reroute-validation.json" -f $script:RunTag) -Object $report)
        $summary = Get-RerouteAcceptanceSummary -Report $report
        Write-ActionSummaryArtifact -ActionName "ValidateReroute" -Summary $summary
        if (-not [bool]$summary["Passed"])
        {
            throw ("Reroute validation failed acceptance: {0}" -f ([string]::Join(" | ", @($summary["FailureReasons"]))))
        }

        return $report
    }
    catch
    {
        if ($null -ne $report)
        {
            [void](Write-ArtifactJson -Name ("{0}-reroute-validation.json" -f $script:RunTag) -Object $report)
        }

        if ($null -eq $summary)
        {
            $summary = Get-RerouteAcceptanceSummary -Report $report
        }

        Add-ActionFailureReason -Summary $summary -Reason $_.Exception.Message
        $summary["Passed"] = $false
        Write-ActionSummaryArtifact -ActionName "ValidateReroute" -Summary $summary
        throw
    }
    finally
    {
        if ([bool]$RestoreFlagsOnExit -and $script:FeatureFlagSnapshotActive)
        {
            try
            {
                Restore-FeatureFlagSnapshot
                [void](Wait-ForFeatureFlagsApplied -ExpectedPatch @{} -TimeoutSec 2 -SkipIfApiUnavailable)
            }
            catch
            {
                Write-WarnLine "Failed to restore feature flags on exit: $($_.Exception.Message)"
            }
        }
    }
}

function Invoke-NoProgressValidation
{
    $report = $null
    $summary = $null
    try
    {
        Ensure-SessionArtifactDir | Out-Null
        $baseline = New-SessionManifestBaseline
        Write-SessionManifest -Baseline $baseline
        Assert-ServiceReadinessGate -Context "no-progress-validation-start" -RequireApiHealth
        Assert-ProfileRouteReadyGate -Context "no-progress-validation-start"
        Write-SessionManifest -Baseline $baseline -IncludeRuntimeState

        $deadline = (Get-Date).AddSeconds([Math]::Max(120, $WatchSeconds))
        $samples = New-Object System.Collections.Generic.List[object]
        $triggerSample = $null
        $recoveryObserved = $false

        while ((Get-Date) -lt $deadline)
        {
            $runtime = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
            if ($runtime.Success -and $null -ne $runtime.Result)
            {
                [void]$samples.Add($runtime.Result)
                $stuck = $runtime.Result.StuckDetector
                if ($null -ne $stuck)
                {
                    $reason = "$($stuck.LastTriggerReason)"
                    if ($reason -eq "ShortNoProgress" -or $reason -eq "TimeoutNoProgress")
                    {
                        $triggerSample = $runtime.Result
                    }

                    if ($null -ne $triggerSample -and -not [bool]$stuck.IsCurrentlyStuck)
                    {
                        $recoveryObserved = $true
                        break
                    }
                }
            }

            Start-Sleep -Milliseconds 1000
        }

        Invoke-CollectEvidence -Stage "no-progress-validation-end"
        $report = [ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            TriggerObserved = $null -ne $triggerSample
            RecoveryObserved = $recoveryObserved
            TriggerReason = $(if ($null -ne $triggerSample -and $null -ne $triggerSample.StuckDetector) { "$($triggerSample.StuckDetector.LastTriggerReason)" } else { $null })
            SampleCount = $samples.Count
            Samples = @($samples)
        }

        [void](Write-ArtifactJson -Name ("{0}-no-progress-validation.json" -f $script:RunTag) -Object $report)
        $summary = Get-NoProgressAcceptanceSummary -Report $report
        Write-ActionSummaryArtifact -ActionName "ValidateNoProgress" -Summary $summary
        if (-not [bool]$summary["Passed"])
        {
            throw ("No-progress validation failed acceptance: {0}" -f ([string]::Join(" | ", @($summary["FailureReasons"]))))
        }

        return $report
    }
    catch
    {
        if ($null -eq $summary)
        {
            $summary = Get-NoProgressAcceptanceSummary -Report $report
        }

        Add-ActionFailureReason -Summary $summary -Reason $_.Exception.Message
        $summary["Passed"] = $false
        Write-ActionSummaryArtifact -ActionName "ValidateNoProgress" -Summary $summary
        throw
    }
}

function Get-SessionLogContent
{
    $manifestPath = Join-Path $script:SessionArtifactDir "session-manifest.json"
    $manifest = $(if (Test-Path -LiteralPath $manifestPath) { Get-Content -LiteralPath $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json } else { $null })
    $runtimeLogSnapshot = $(if ($null -ne $manifest) { $manifest.RuntimeLogSnapshot } else { $null })

    $logCandidates = @(
        (Join-Path $script:LogsDir "$script:RunTag-blazor-stdout.log"),
        (Join-Path $script:LogsDir "$script:RunTag-blazor-stderr.log")
    )

    if (Test-Path -LiteralPath $script:SessionArtifactDir)
    {
        $artifactCandidates = Get-ChildItem -LiteralPath $script:SessionArtifactDir -File -ErrorAction SilentlyContinue |
            Where-Object { $_.Extension -in @(".log", ".txt") } |
            Sort-Object LastWriteTimeUtc
        foreach ($candidate in @($artifactCandidates))
        {
            $logCandidates += $candidate.FullName
        }
    }

    $content = New-Object System.Text.StringBuilder
    if ($null -ne $runtimeLogSnapshot)
    {
        $runtimeLogPath = "$($runtimeLogSnapshot.FullPath)"
        if (-not [string]::IsNullOrWhiteSpace($runtimeLogPath))
        {
            $runtimeContent = Get-FileContentFromOffset -Path $runtimeLogPath -StartOffset ([long]$runtimeLogSnapshot.SizeBytes)
            if (-not [string]::IsNullOrWhiteSpace($runtimeContent))
            {
                [void]$content.AppendLine($runtimeContent)
            }
        }
    }

    foreach ($candidate in @($logCandidates | Select-Object -Unique))
    {
        if (Test-Path -LiteralPath $candidate)
        {
            [void]$content.AppendLine((Get-Content -LiteralPath $candidate -Raw -Encoding UTF8))
        }
    }

    return $content.ToString()
}

function Get-RegexMatchCount
{
    param(
        [Parameter(Mandatory = $true)][string]$Content,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    return ([regex]::Matches($Content, $Pattern, [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)).Count
}

function Get-CombatHealthWindowCount
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Samples,
        [double]$Threshold = 35
    )

    $count = 0
    foreach ($sample in @($Samples))
    {
        try
        {
            if ($null -eq $sample)
            {
                continue
            }

            $healthPercent = 0.0
            if (($sample.PSObject.Properties.Name -contains 'HealthPercent') -and
                [double]::TryParse([string]$sample.HealthPercent, [ref]$healthPercent))
            {
                if ($healthPercent -lt $Threshold)
                {
                    $count++
                }

                continue
            }

            if ($null -eq $sample.Snapshot)
            {
                continue
            }

            $snapshotEnvelope = $sample.Snapshot
            if (($snapshotEnvelope.PSObject.Properties.Name -contains 'Success') -and -not [bool]$snapshotEnvelope.Success)
            {
                continue
            }

            if (($snapshotEnvelope.PSObject.Properties.Name -contains 'Result') -and $null -ne $snapshotEnvelope.Result)
            {
                $snapshotPayload = $snapshotEnvelope.Result
            }
            elseif (($snapshotEnvelope.PSObject.Properties.Name -contains 'Data') -and $null -ne $snapshotEnvelope.Data)
            {
                $snapshotPayload = $snapshotEnvelope.Data
            }
            else
            {
                continue
            }

            if (($snapshotPayload.PSObject.Properties.Name -contains 'data') -and $null -ne $snapshotPayload.data)
            {
                $snapshotData = $snapshotPayload.data
            }
            elseif (($snapshotPayload.PSObject.Properties.Name -contains 'Data') -and $null -ne $snapshotPayload.Data)
            {
                $snapshotData = $snapshotPayload.Data
            }
            else
            {
                continue
            }

            if (($snapshotData.PSObject.Properties.Name -notcontains 'snapshot') -or $null -eq $snapshotData.snapshot)
            {
                continue
            }

            if (-not [double]::TryParse([string]$snapshotData.snapshot.healthPercent, [ref]$healthPercent))
            {
                continue
            }

            if ($healthPercent -lt $Threshold)
            {
                $count++
            }
        }
        catch
        {
        }
    }

    return $count
}

function Convert-CombatSamplesToTelemetry
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Samples
    )

    $telemetry = New-Object System.Collections.Generic.List[object]
    foreach ($sample in @($Samples))
    {
        $statsKills = $null
        $statsDeaths = $null
        $runtimeGoal = $null
        $snapshotGoal = $null
        $healthPercent = $null
        $inCombat = $null

        try
        {
            if ($null -ne $sample.SessionStats)
            {
                $statsEnvelope = $sample.SessionStats
                if (($statsEnvelope.PSObject.Properties.Name -contains 'Result') -and $null -ne $statsEnvelope.Result)
                {
                    $statsPayload = $statsEnvelope.Result
                }
                elseif (($statsEnvelope.PSObject.Properties.Name -contains 'Data') -and $null -ne $statsEnvelope.Data)
                {
                    $statsPayload = $statsEnvelope.Data
                }

                if ($null -ne $statsPayload)
                {
                    $statsKills = $statsPayload.Kills
                    $statsDeaths = $statsPayload.Deaths
                    $snapshotGoal = $statsPayload.CurrentGoal
                }
            }
        }
        catch
        {
        }

        try
        {
            if ($null -ne $sample.Runtime)
            {
                $runtimeEnvelope = $sample.Runtime
                if (($runtimeEnvelope.PSObject.Properties.Name -contains 'Result') -and $null -ne $runtimeEnvelope.Result)
                {
                    $runtimePayload = $runtimeEnvelope.Result
                }
                elseif (($runtimeEnvelope.PSObject.Properties.Name -contains 'Data') -and $null -ne $runtimeEnvelope.Data)
                {
                    $runtimePayload = $runtimeEnvelope.Data
                }

                if ($null -ne $runtimePayload)
                {
                    $runtimeGoal = $runtimePayload.CurrentGoal
                }
            }
        }
        catch
        {
        }

        try
        {
            if ($null -ne $sample.Snapshot)
            {
                $snapshotEnvelope = $sample.Snapshot
                if (($snapshotEnvelope.PSObject.Properties.Name -contains 'Result') -and $null -ne $snapshotEnvelope.Result)
                {
                    $snapshotPayload = $snapshotEnvelope.Result
                }
                elseif (($snapshotEnvelope.PSObject.Properties.Name -contains 'Data') -and $null -ne $snapshotEnvelope.Data)
                {
                    $snapshotPayload = $snapshotEnvelope.Data
                }

                if (($snapshotPayload.PSObject.Properties.Name -contains 'data') -and $null -ne $snapshotPayload.data)
                {
                    $snapshotData = $snapshotPayload.data
                }
                elseif (($snapshotPayload.PSObject.Properties.Name -contains 'Data') -and $null -ne $snapshotPayload.Data)
                {
                    $snapshotData = $snapshotPayload.Data
                }

                if ($null -ne $snapshotData -and $null -ne $snapshotData.snapshot)
                {
                    $healthPercent = $snapshotData.snapshot.healthPercent
                    $inCombat = $snapshotData.snapshot.inCombat
                }
            }
        }
        catch
        {
        }

        [void]$telemetry.Add([pscustomobject][ordered]@{
                TimestampUtc = $sample.TimestampUtc
                Kills = $statsKills
                Deaths = $statsDeaths
                SessionGoal = $snapshotGoal
                RuntimeGoal = $runtimeGoal
                HealthPercent = $healthPercent
                InCombat = $inCombat
            })
    }

    return $telemetry.ToArray()
}

function Invoke-ControlledCombatValidation
{
    $report = $null
    $summary = $null
    try
    {
        Ensure-SessionArtifactDir | Out-Null
        $baseline = New-SessionManifestBaseline
        Write-SessionManifest -Baseline $baseline
        Assert-ServiceReadinessGate -Context "combat-validation-start" -RequireApiHealth
        Assert-ProfileRouteReadyGate -Context "combat-validation-start"
        Write-SessionManifest -Baseline $baseline -IncludeRuntimeState

        $targetKills = 30
        $startStats = Invoke-AgentApi -Method GET -Path "/api/session/stats" -TimeoutSec 5
        $startKills = [int]$startStats.Kills
        $deadline = (Get-Date).AddSeconds([Math]::Max(900, ($ShortValidationSeconds * 2)))
        $samples = New-Object System.Collections.Generic.List[object]

        while ((Get-Date) -lt $deadline)
        {
            $stats = Invoke-AgentApiSafe -Method GET -Path "/api/session/stats" -TimeoutSec 5
            $runtime = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
            $snapshot = Invoke-AgentApiSafe -Method GET -Path "/api/test/snapshot" -TimeoutSec 8
            $sample = [ordered]@{
                TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
                SessionStats = $stats
                Runtime = $runtime
                Snapshot = $snapshot
            }
            [void]$samples.Add($sample)

            if ($stats.Success -and ([int]$stats.Result.Kills - $startKills) -ge $targetKills)
            {
                break
            }

            Start-Sleep -Seconds ([Math]::Max(5, [Math]::Min(15, $MonitorIntervalSeconds)))
        }

        Invoke-CollectEvidence -Stage "combat-validation-end"
        $endStats = Invoke-AgentApi -Method GET -Path "/api/session/stats" -TimeoutSec 5
        $killsDelta = [int]$endStats.Kills - $startKills
        $logContent = Get-SessionLogContent
        $runtimeFinal = Invoke-AgentApiSafe -Method GET -Path "/api/diagnostics/navigation/runtime" -TimeoutSec 8
        $sampleTelemetry = Convert-CombatSamplesToTelemetry -Samples $samples.ToArray()
        $lowHealthSampleCount = Get-CombatHealthWindowCount -Samples $sampleTelemetry -Threshold 35
        $criticalHealthSampleCount = Get-CombatHealthWindowCount -Samples $sampleTelemetry -Threshold 20

        $report = [ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            StartKills = $startKills
            EndKills = [int]$endStats.Kills
            KillsDelta = $killsDelta
            TargetKills = $targetKills
            PullOpenerCounts = [ordered]@{
                CurseOfAgony = (Get-RegexMatchCount -Content $logContent -Pattern "Curse of Agony")
                Immolate = (Get-RegexMatchCount -Content $logContent -Pattern "\bImmolate\b")
                ShadowBolt = (Get-RegexMatchCount -Content $logContent -Pattern "Shadow Bolt")
            }
            SpellCounts = [ordered]@{
                Immolate = (Get-RegexMatchCount -Content $logContent -Pattern "\bImmolate\b")
                Corruption = (Get-RegexMatchCount -Content $logContent -Pattern "\bCorruption\b")
                ShadowBolt = (Get-RegexMatchCount -Content $logContent -Pattern "Shadow Bolt")
                Shoot = (Get-RegexMatchCount -Content $logContent -Pattern "\bShoot\b")
            }
            FearCastCount = (Get-RegexMatchCount -Content $logContent -Pattern 'DataToColor:PS\("Fear"|CastingHandler\s+\]\s+\[Fear')
            DrainLifeCastCount = (Get-RegexMatchCount -Content $logContent -Pattern 'DataToColor:PS\("Drain Life"|CastingHandler\s+\]\s+\[Drain Life')
            HealthstoneUseCount = (Get-RegexMatchCount -Content $logContent -Pattern 'Healthstone use requested|Use Healthstone requested|/use item:(34062|30703|22044|22105|22104|22103|19013|19012|9421|19011|19010|5510|19009|19008|5509|19007|19006|5511|19005|19004|5512)')
            HealthstoneCreateCount = (Get-RegexMatchCount -Content $logContent -Pattern 'Create Healthstone requested via command cast|/cast Create Healthstone|CastingHandler\s+\]\s+\[Create Healthstone')
            SummonVoidwalkerCount = (Get-RegexMatchCount -Content $logContent -Pattern 'DataToColor:PS\("Summon Voidwalker"|CastingHandler\s+\]\s+\[Summon Voidwalker')
            SummonImpCount = (Get-RegexMatchCount -Content $logContent -Pattern 'DataToColor:PS\("Summon Imp"|CastingHandler\s+\]\s+\[Summon Imp')
            ApproachAssistCount = (Get-RegexMatchCount -Content $logContent -Pattern "New Plan=\s+Approach Target")
            BodyPullFallbackCount = (Get-RegexMatchCount -Content $logContent -Pattern "forcing short approach retry before clear")
            LostTargetCount = (Get-RegexMatchCount -Content $logContent -Pattern "Lost target")
            SpellFailedMovingCount = (Get-RegexMatchCount -Content $logContent -Pattern "SPELL_FAILED_MOVING")
            BadAttackFacingCount = (Get-RegexMatchCount -Content $logContent -Pattern "ERR_BADATTACKFACING")
            FacingRecoveryCount = (Get-RegexMatchCount -Content $logContent -Pattern "React to ERR_BADATTACKFACING")
            LowHealthSampleCount = $lowHealthSampleCount
            CriticalHealthSampleCount = $criticalHealthSampleCount
            FocusRestoreFailureCount = (Get-RegexMatchCount -Content $logContent -Pattern "Failed to restore WoW focus")
            Runtime = $runtimeFinal
            SampleCount = @($sampleTelemetry).Count
            Samples = @($sampleTelemetry)
        }

        [void](Write-ArtifactJson -Name ("{0}-combat-validation.json" -f $script:RunTag) -Object $report)
        $summary = Get-CombatAcceptanceSummary -Report $report
        Write-ActionSummaryArtifact -ActionName "ValidateCombat" -Summary $summary
        if (-not [bool]$summary["Passed"])
        {
            throw ("Controlled combat validation failed acceptance: {0}" -f ([string]::Join(" | ", @($summary["FailureReasons"]))))
        }

        return $report
    }
    catch
    {
        if ($null -eq $summary)
        {
            $summary = Get-CombatAcceptanceSummary -Report $report
        }

        Add-ActionFailureReason -Summary $summary -Reason $_.Exception.Message
        $summary["Passed"] = $false
        Write-ActionSummaryArtifact -ActionName "ValidateCombat" -Summary $summary
        throw
    }
}

function Invoke-LiveSession
{
    Ensure-SessionArtifactDir | Out-Null
    Write-Info "Session artifact directory: $script:SessionArtifactDir"

    $baseline = New-SessionManifestBaseline
    Write-SessionManifest -Baseline $baseline
    [void](Write-ArtifactJson -Name "prestart-ports.json" -Object (Get-PortStateSnapshot))
    $watchSummary = $null
    $soakResult = $null
    $summary = $null

    try
    {
        if (-not $NoAutoNavProfile)
        {
            Save-FeatureFlagSnapshot | Out-Null
            Apply-FeatureFlagProfile -ProfileName $NavProfile
        }

        Invoke-StartFlow -WithValidation
        Write-SessionManifest -Baseline $baseline -IncludeRuntimeState
        Assert-ServiceReadinessGate -Context "live-session-post-start" -RequireApiHealth
        Invoke-CollectEvidence -Stage "post-start"

        $watchDuration = [Math]::Min([Math]::Max(60, $ShortValidationSeconds), 300)
        $watchSummary = Invoke-WatchNav -DurationSeconds $watchDuration -CadenceMs $WatchCadenceMs
        [void](Write-ArtifactJson -Name ("{0}-live-watchnav-initial.json" -f $script:RunTag) -Object $watchSummary)

        if ([int]$watchSummary.RouteFollowSampleCount -le 0)
        {
            Invoke-CollectEvidence -Stage "watchnav-invalid-no-routefollow"
            throw "Initial navigation validation was invalid (no FollowRoute samples observed). Refusing to treat this run as a navigation pass."
        }

        if (Test-NavChurnDetected -WatchSummary $watchSummary)
        {
            Write-WarnLine "Navigation churn detected during initial watch; running NavTriage"
            $triage = Invoke-NavTriage
            [void](Write-ArtifactJson -Name ("{0}-live-navtriage.json" -f $script:RunTag) -Object $triage)
            if ($null -ne $triage -and -not [string]::IsNullOrWhiteSpace("$($triage.RecommendedProfile)") -and "$($triage.RecommendedProfile)" -ne "$NavProfile")
            {
                Write-Info "Applying triage-recommended profile '$($triage.RecommendedProfile)' for soak run"
                Apply-FeatureFlagProfile -ProfileName "$($triage.RecommendedProfile)" -VerifyViaApi
                Write-SessionManifest -Baseline $baseline -IncludeRuntimeState
            }

            $botStatusAfterTriage = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
            if (-not $botStatusAfterTriage.Success -or -not [bool]$botStatusAfterTriage.Result.IsActive)
            {
                Start-Bot
            }
        }

        Invoke-CollectEvidence -Stage "pre-soak"
        Assert-ServiceReadinessGate -Context "live-session-pre-soak" -RequireApiHealth
        $soakResult = Invoke-SoakRun
        Invoke-CollectEvidence -Stage "final" -FlushSoak

        $summary = Get-LiveSessionAcceptanceSummary -WatchSummary $watchSummary -SoakResult $soakResult
        Write-ActionSummaryArtifact -ActionName "LiveSession" -Summary $summary
        if (-not [bool]$summary["Passed"])
        {
            throw ("Live session failed acceptance: {0}" -f ([string]::Join(" | ", @($summary["FailureReasons"]))))
        }
    }
    catch
    {
        if ($null -eq $summary)
        {
            $summary = Get-LiveSessionAcceptanceSummary -WatchSummary $watchSummary -SoakResult $soakResult
        }

        Add-ActionFailureReason -Summary $summary -Reason $_.Exception.Message
        $summary["Passed"] = $false
        Write-ActionSummaryArtifact -ActionName "LiveSession" -Summary $summary
        throw
    }
    finally
    {
        if ([bool]$RestoreFlagsOnExit -and $script:FeatureFlagSnapshotActive)
        {
            try
            {
                Restore-FeatureFlagSnapshot
                [void](Wait-ForFeatureFlagsApplied -ExpectedPatch @{} -TimeoutSec 2 -SkipIfApiUnavailable)
            }
            catch
            {
                Write-WarnLine "Failed to restore feature flags on exit: $($_.Exception.Message)"
            }
        }
    }
}

function Invoke-StartFlow([switch]$WithValidation)
{
    Stop-StaleServerProcesses
    Invoke-ReleaseBuild
    Start-NavigationServer
    Start-BlazorServer
    Assert-ServiceReadinessGate -Context "post-service-start" -RequireApiHealth
    Set-LaunchOverrides
    Load-BotProfile
    Assert-CharacterAlignment

    if ($AutoRepairReadiness)
    {
        $doctor = Invoke-Doctor -SkipEvidence -ReturnDispositionOnly
        if ($doctor.Disposition -eq "Blocked")
        {
            throw "Doctor readiness repair failed: $($doctor.Reason)"
        }
    }
    else
    {
        Invoke-ReadinessFixes | Out-Null
        $null = Wait-ForReadiness
    }

    Assert-ServiceReadinessGate -Context "pre-bot-start" -RequireApiHealth
    Start-Bot
    Assert-ProfileRouteReadyGate -Context "post-bot-start"
    Assert-CastingSnapshotReadyGate -Context "post-bot-start"

    if ($WithValidation)
    {
        $report = Invoke-SystemValidation
        if (-not $report.OverallPass)
        {
            throw "Validation reported failures. See report JSON in logs."
        }
    }

    if ($StartMonitor)
    {
        $monitorScript = Join-Path $BotRoot "Monitor-Bot.ps1"
        if (Test-Path -LiteralPath $monitorScript)
        {
            & $monitorScript -Interval $MonitorIntervalSeconds
        }
        else
        {
            Write-WarnLine "Monitor script not found: $monitorScript"
        }
    }
}

try
{
    switch ($Action)
    {
        "Start"
        {
            Invoke-StartFlow
        }
        "StartAndValidate"
        {
            Invoke-StartFlow -WithValidation
        }
        "Validate"
        {
            $report = Invoke-SystemValidation
            if (-not $report.OverallPass)
            {
                exit 1
            }
        }
        "Status"
        {
            Show-Status
        }
        "Stop"
        {
            Stop-BotAndServices
        }
        "Restart"
        {
            Stop-BotAndServices
            Invoke-StartFlow
        }
        "Monitor"
        {
            $monitorScript = Join-Path $BotRoot "Monitor-Bot.ps1"
            if (-not (Test-Path -LiteralPath $monitorScript))
            {
                throw "Missing monitor script: $monitorScript"
            }
            & $monitorScript -Interval $MonitorIntervalSeconds
        }
        "Api"
        {
            if ([string]::IsNullOrWhiteSpace($ApiPath))
            {
                throw "-ApiPath is required when -Action Api is used."
            }

            $bodyObj = $null
            if (-not [string]::IsNullOrWhiteSpace($ApiBody))
            {
                try
                {
                    $bodyObj = $ApiBody | ConvertFrom-Json
                }
                catch
                {
                    $bodyObj = $ApiBody
                }
            }

            $result = Invoke-AgentApi -Method $ApiMethod -Path $ApiPath -Body $bodyObj -TimeoutSec $ApiTimeoutSeconds
            $result | ConvertTo-Json -Depth 16
        }
        "CollectEvidence"
        {
            Invoke-CollectEvidence -Stage "manual" -FlushSoak
        }
        "Doctor"
        {
            Invoke-Doctor
        }
        "GameCmd"
        {
            Invoke-GameCommandAction
        }
        "FlagsProfile"
        {
            Invoke-FlagsProfileAction
        }
        "WatchNav"
        {
            $summary = Invoke-WatchNav -DurationSeconds $WatchSeconds -CadenceMs $WatchCadenceMs
            $summary | ConvertTo-Json -Depth 16
        }
        "NavTriage"
        {
            $report = Invoke-NavTriage
            $report | ConvertTo-Json -Depth 24
        }
        "ValidateReroute"
        {
            $report = Invoke-RerouteValidation
            $report | ConvertTo-Json -Depth 24
        }
        "ValidateNoProgress"
        {
            $report = Invoke-NoProgressValidation
            $report | ConvertTo-Json -Depth 24
        }
        "ValidateCombat"
        {
            $report = Invoke-ControlledCombatValidation
            $report | ConvertTo-Json -Depth 24
        }
        "Soak"
        {
            Invoke-SoakRun
        }
        "LiveSession"
        {
            Invoke-LiveSession
        }
    }

    Write-Ok "Action '$Action' completed"
}
catch
{
    Write-ActionFailureArtifact -ActionName $Action -ErrorRecord $_
    $line = $null
    try { $line = $_.InvocationInfo.ScriptLineNumber } catch { }
    if ($line)
    {
        Write-ErrLine ("{0} (line {1})" -f $_.Exception.Message, $line)
    }
    else
    {
        Write-ErrLine $_.Exception.Message
    }
    if (-not [string]::IsNullOrWhiteSpace($_.ScriptStackTrace))
    {
        Write-ErrLine $_.ScriptStackTrace
    }
    exit 1
}
