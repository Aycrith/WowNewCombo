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
    [ValidateSet("Start", "StartAndValidate", "Validate", "Status", "Stop", "Restart", "Monitor", "Api", "CollectEvidence", "Soak", "LiveSession")]
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

    [switch]$AllowStartWithWarnings,
    [switch]$BypassActionBar,
    [switch]$SkipCharacterGate,
    [switch]$StartMonitor,
    [switch]$StopServices,

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

    $uri = "$BaseUrl$Path"

    try
    {
        if ($Method -eq "GET")
        {
            return Invoke-RestMethod -Uri $uri -Method Get -TimeoutSec $TimeoutSec
        }

        if ($null -eq $Body)
        {
            return Invoke-RestMethod -Uri $uri -Method $Method -TimeoutSec $TimeoutSec -ContentType "application/json" -Body "{}"
        }

        if ($Body -is [string])
        {
            $jsonBody = $Body
        }
        else
        {
            $jsonBody = $Body | ConvertTo-Json -Depth 12
        }

        return Invoke-RestMethod -Uri $uri -Method $Method -TimeoutSec $TimeoutSec -ContentType "application/json" -Body $jsonBody
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

        throw "API $Method $Path failed: $err"
    }
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

function Set-LaunchOverrides
{
    Write-Info "Applying launch overrides"

    $body = @{
        AllowStartWithWarnings = [bool]$AllowStartWithWarnings
        EmergencyBypassAll = $false
        Bypass = @{
            Route = $false
            ActionBar = [bool]$BypassActionBar
            KeyBindings = $false
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

    $body = @{
        AllowStartWithWarnings = [bool]$AllowStartWithWarnings
        EmergencyBypassAll = $false
        Bypass = @{
            Route = $false
            ActionBar = $Enabled
            KeyBindings = $false
        }
        Reason = $Reason
        Source = "Agent-BotControl"
    }

    $null = Invoke-AgentApi -Method POST -Path "/api/launch/overrides" -Body $body
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
        throw "Chat input is currently open in WoW. Close chat (Escape) before startup fixes to prevent in-game text spam."
    }

    Write-Info "Applying startup fixes (initstate + bindings/actions sync)"
    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/initstate" -Body @{} -TimeoutSec 30
    }
    catch
    {
        Write-WarnLine "fix/initstate did not complete: $($_.Exception.Message)"
    }

    Start-Sleep -Seconds 1

    try
    {
        $null = Invoke-AgentApi -Method POST -Path "/api/diagnostics/fix/all" -Body @{} -TimeoutSec 60
    }
    catch
    {
        Write-WarnLine "fix/all did not complete: $($_.Exception.Message)"
    }

    Write-Ok "Startup fixes triggered"
}

function Wait-ForReadiness
{
    Write-Info "Waiting for launch readiness checks"

    $deadline = (Get-Date).AddSeconds($ReadinessTimeoutSeconds)
    $attempt = 0
    $lastBlocking = @()
    $actionBarBypassApplied = $false
    $dtcHandshakeReloadAttempted = $false

    while ((Get-Date) -lt $deadline)
    {
        $attempt++
        $launch = Invoke-AgentApi -Method GET -Path "/api/launch/status" -TimeoutSec 8
        if ($launch.CanStartBot)
        {
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
                throw "Chat input became visible while waiting for readiness. Close chat (Escape) and rerun StartAndValidate."
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
    if ($snap.Dead)
    {
        [void]$issues.Add("Character is dead (resurrect at spirit healer before bot start).")
    }
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
        throw "Abort: Bot became inactive during live validation"
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

    Invoke-StartFlow -WithValidation
    Invoke-CollectEvidence -Stage "post-start"
    Invoke-ShortActiveValidation -DurationSeconds $ShortValidationSeconds
    Invoke-CollectEvidence -Stage "pre-soak"
    Invoke-SoakRun
    Invoke-CollectEvidence -Stage "final" -FlushSoak
}

function Invoke-StartFlow([switch]$WithValidation)
{
    Stop-StaleServerProcesses
    Start-NavigationServer
    Start-BlazorServer
    Set-LaunchOverrides
    Load-BotProfile
    Assert-CharacterAlignment
    Invoke-ReadinessFixes | Out-Null
    $null = Wait-ForReadiness
    Start-Bot

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
    Write-ErrLine $_.Exception.Message
    exit 1
}
