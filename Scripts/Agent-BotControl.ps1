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
param(
    [ValidateSet("Start", "StartAndValidate", "Validate", "Status", "Stop", "Restart", "Monitor", "Api", "CollectEvidence", "Soak", "LiveSession", "Doctor", "GameCmd", "FlagsProfile", "WatchNav", "NavTriage")]
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
                    if ($err -notlike "*$($_.ErrorDetails.Message)*")
                    {
                        $err = "$err`n$($_.ErrorDetails.Message)"
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
            return [ordered]@{
                "Features.HazardAvoidance.Enabled" = $false
                "Features.StuckSensitivity.Enabled" = $true
                "Features.StuckSensitivity.MinDistance" = 0.08
                "Features.StuckSensitivity.UnstuckAfterMs" = 3200
                "Features.StuckSensitivity.EnablePredictiveDetection" = $false
                "Features.StuckSensitivity.PredictiveRiskThreshold" = 80
                "Features.StuckSensitivity.ApproachTimeoutMultiplier" = 1.5
            }
        }
        "triage-baseline"
        {
            return (Get-FeatureFlagProfilePatch -ProfileName "stable-live")
        }
        "triage-hazard"
        {
            $patch = [ordered]@{}
            foreach ($k in (Get-FeatureFlagProfilePatch -ProfileName "stable-live").Keys)
            {
                $patch[$k] = (Get-FeatureFlagProfilePatch -ProfileName "stable-live")[$k]
            }
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

    if ($ProfileName -eq "current")
    {
        Write-Info "Nav profile 'current' selected; no runtime flag changes applied"
        $script:FeatureFlagProfileApplied = "current"
        return
    }

    if (-not $script:FeatureFlagSnapshotActive)
    {
        Save-FeatureFlagSnapshot | Out-Null
    }

    $flagsPath = Get-FeatureFlagsFilePath
    $json = Get-Content -LiteralPath $flagsPath -Raw -Encoding UTF8 | ConvertFrom-Json -Depth 64
    $patch = Get-FeatureFlagProfilePatch -ProfileName $ProfileName

    foreach ($entry in $patch.GetEnumerator())
    {
        Set-ObjectPathValue -Root $json -Path $entry.Key -Value $entry.Value
    }

    $json.LastModified = (Get-Date).ToUniversalTime().ToString("o")
    ($json | ConvertTo-Json -Depth 64) | Set-Content -LiteralPath $flagsPath -Encoding UTF8
    $script:FeatureFlagProfileApplied = $ProfileName

    [void](Write-ArtifactJson -Name ("{0}-flags-profile-{1}.json" -f $script:RunTag, $ProfileName) -Object ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Profile = $ProfileName
            Patch = $patch
        }))

    Write-Ok "Applied nav feature-flag profile '$ProfileName'"

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
        [switch]$RequireApiHealth
    )

    if (-not (Test-PortListening -Port $WebPort))
    {
        throw "Readiness gate failed ($Context): web port $WebPort is not listening"
    }

    if (-not (Test-PortListening -Port $NavigationPort))
    {
        throw "Readiness gate failed ($Context): navigation port $NavigationPort is not listening"
    }

    if ($RequireApiHealth)
    {
        $health = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
        if (-not $health.Success -or $null -eq $health.Result)
        {
            throw "Readiness gate failed ($Context): /api/health unavailable ($($health.Error))"
        }
    }
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

    while ((Get-Date) -lt $deadline)
    {
        $attempt++
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

function Assert-CharacterAlignment
{
    if ($SkipCharacterGate)
    {
        Write-WarnLine "Character alignment gate skipped by flag"
        return
    }

    Write-Info "Checking active character alignment"
    $snap = Get-CurrentSnapshot
    if ($null -eq $snap)
    {
        throw "Unable to read player snapshot for alignment checks."
    }

    $issues = New-Object System.Collections.Generic.List[string]
    if ($snap.ChatInputVisible)
    {
        [void]$issues.Add("Chat input is open (press Escape first to avoid typing automation keys into chat).")
    }
    $isDead = [bool]$snap.Dead
    if ($snap.Swimming)
    {
        [void]$issues.Add("Character is swimming (likely ocean/off-route position).")
    }
    if (($snap.MapX -le 0) -or ($snap.MapX -gt 100) -or ($snap.MapY -le 0) -or ($snap.MapY -gt 100))
    {
        [void]$issues.Add("Character map position appears out-of-bounds (MapX=$($snap.MapX), MapY=$($snap.MapY)).")
    }

    if ($issues.Count -gt 0)
    {
        throw ($issues -join " ")
    }

    if ($isDead)
    {
        Write-WarnLine "Character is dead; allowing startup so corpse-recovery GOAP can run autonomously."
    }

    Write-Ok "Character alignment checks passed (MapX=$([math]::Round($snap.MapX, 2)), MapY=$([math]::Round($snap.MapY, 2)), UIMapId=$($snap.UIMapId))"
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
        [switch]$Quiet
    )

    Assert-ServiceReadinessGate -Context "watchnav-start" -RequireApiHealth
    Assert-ProfileRouteReadyGate -Context "watchnav-start"
    Assert-CastingSnapshotReadyGate -Context "watchnav-start"
    Ensure-SessionArtifactDir | Out-Null
    $sampleFile = Join-Path $script:SessionArtifactDir ("{0}-watchnav-samples.jsonl" -f $script:RunTag)
    $deadline = (Get-Date).AddSeconds([Math]::Max(1, $DurationSeconds))
    $samples = New-Object System.Collections.Generic.List[object]

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

        [void]$samples.Add($sample)
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

            Write-Info ("WatchNav: Goal={0} MaxDev={1} RepeatRate={2} LastTrigger={3} FrontBypass={4}" -f $goal, $maxDev, $repeatRate, $trigger, $bypass)
        }

        Start-Sleep -Milliseconds ([Math]::Max(250, $CadenceMs))
    }

    $routeFollowSamples = @($samples | Where-Object {
            $null -ne $_.BotStatus -and "$($_.BotStatus.CurrentGoal)".Trim() -like "Follow*"
        })

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
    if ($routeFollowSamples.Count -eq 0 -and $samples.Count -gt 0)
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
            $invalidReason = "No FollowRoute samples observed"
        }
    }

    $summary = [pscustomobject]@{
        SampleFile = $sampleFile
        DurationSeconds = $DurationSeconds
        SampleCount = $samples.Count
        RouteFollowSampleCount = $routeFollowSamples.Count
        RouteFollowSamplesObserved = ($routeFollowSamples.Count -gt 0)
        NavigationValidationValid = ($routeFollowSamples.Count -gt 0)
        MixedGoalWindow = ($routeFollowSamples.Count -gt 0 -and $routeFollowSamples.Count -lt $samples.Count)
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
    [void](Save-ApiSnapshot -ApiPath "/api/test/status" -FileStem ("{0}-{1}-test-status.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
    [void](Save-ApiSnapshot -ApiPath "/api/test/frames" -FileStem ("{0}-{1}-test-frames.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
    [void](Save-ApiSnapshot -ApiPath "/api/test/snapshot" -FileStem ("{0}-{1}-test-snapshot.json" -f $stamp, $Stage) -TimeoutSec $ValidationTimeoutSeconds)
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
    $result = [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Ports = (Get-PortStateSnapshot)
        BotStatus = $null
        LaunchStatus = $null
        Snapshot = $null
        Health = $null
        Errors = @()
    }

    $health = Invoke-AgentApiSafe -Method GET -Path "/api/health" -TimeoutSec 5
    if ($health.Success) { $result.Health = $health.Result } else { $result.Errors += "health: $($health.Error)" }

    $launch = Invoke-AgentApiSafe -Method GET -Path "/api/launch/status" -TimeoutSec 8
    if ($launch.Success) { $result.LaunchStatus = $launch.Result } else { $result.Errors += "launch: $($launch.Error)" }

    $bot = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
    if ($bot.Success) { $result.BotStatus = $bot.Result } else { $result.Errors += "bot: $($bot.Error)" }

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
    if (-not $Sample.Ports.NavigationPort.Listening)
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
    while ((Get-Date) -lt $deadline)
    {
        $sampleIndex++
        $sample = Get-ActiveRunHealthSample
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
    Write-Ok "Soak run completed"
}

function Invoke-LiveSession
{
    Ensure-SessionArtifactDir | Out-Null
    Write-Info "Session artifact directory: $script:SessionArtifactDir"

    $baseline = [ordered]@{
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
    }
    [void](Write-ArtifactJson -Name "session-manifest.json" -Object $baseline)
    [void](Write-ArtifactJson -Name "prestart-ports.json" -Object (Get-PortStateSnapshot))

    try
    {
        if (-not $NoAutoNavProfile)
        {
            Save-FeatureFlagSnapshot | Out-Null
            Apply-FeatureFlagProfile -ProfileName $NavProfile
        }

        Invoke-StartFlow -WithValidation
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
            }

            $botStatusAfterTriage = Invoke-AgentApiSafe -Method GET -Path "/api/bot/status" -TimeoutSec 5
            if (-not $botStatusAfterTriage.Success -or -not [bool]$botStatusAfterTriage.Result.IsActive)
            {
                Start-Bot
            }
        }

        Invoke-CollectEvidence -Stage "pre-soak"
        Assert-ServiceReadinessGate -Context "live-session-pre-soak" -RequireApiHealth
        Invoke-SoakRun
        Invoke-CollectEvidence -Stage "final" -FlushSoak
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
