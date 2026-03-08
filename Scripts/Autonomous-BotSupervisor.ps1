#Requires -Version 5.1
<#
.SYNOPSIS
  Autonomous validation and improvement supervisor for WowClassicGrindBot.

.DESCRIPTION
  Orchestrates synthetic validation, live readiness checks, targeted live gates,
  evidence collection, issue prioritization, and proposal-first improvement loops
  using the existing project control surfaces.

.EXAMPLE
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Autonomous-BotSupervisor.ps1 -Action RunLoop -MaxCycles 1 -DryRun

.EXAMPLE
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Autonomous-BotSupervisor.ps1 -Action Status

.EXAMPLE
  pwsh -NoProfile -ExecutionPolicy Bypass -File .\Scripts\Autonomous-BotSupervisor.ps1 -Action NextSteps
#>
[CmdletBinding()]
param(
    [ValidateSet("RunLoop", "Status", "NextSteps", "Pause", "Resume", "Stop")]
    [string]$Action = "RunLoop",

    [string]$BotRoot = "",
    [string]$Profile = "BloodElf_Warlock_1-70_TBC.json",
    [ValidateSet("current", "stable-live", "triage-baseline", "triage-hazard", "triage-predictive")]
    [string]$NavProfile = "stable-live",
    [string]$BaseUrl = "http://localhost:5000",
    [ValidateSet("Hybrid", "SyntheticOnly", "LiveFirst")]
    [string]$PrimarySurface = "Hybrid",
    [string]$OutputRoot = "",
    [string]$SupervisorId = "default",
    [int]$HeartbeatSeconds = 30,
    [int]$ObservationSeconds = 15,
    [int]$SyntheticIntervalMinutes = 15,
    [int]$MaxCycles = 0,
    [int]$MaxGateActionsPerCycle = 1,
    [int]$SoakMinutes = 60,
    [int]$WindowMinutes = 10,
    [int]$EvidenceIntervalSeconds = 150,
    [int]$ShortValidationSeconds = 240,
    [int]$MaxAgentControlRuntimeSeconds = 5400,
    [int]$StableGoalObservationSeconds = 120,
    [int]$LegacyHarnessEveryNCycles = 4,
    [string]$PriorityGateOrder = "ValidateCombat,ValidateReroute,ValidateNoProgress,LiveSession",
    [string]$LegacyHarnessStages = "PreFlight",
    [switch]$EnableMutations,
    [switch]$DryRun,
    [switch]$StopServicesOnExit
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

if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot = Join-Path $BotRoot "logs\autonomous-supervisor"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputRoot))
{
    $OutputRoot = Join-Path $BotRoot $OutputRoot
}

$script:BotRoot = $BotRoot
$script:AgentControlPath = Join-Path $BotRoot "Scripts\Agent-BotControl.ps1"
$script:LegacyHarnessPath = Join-Path $BotRoot "Scripts\Test-Harness\Test-Harness.ps1"
$script:LevelingOrchestratorPath = Join-Path $BotRoot "Scripts\Orchestrate-WarlockLeveling.ps1"
$script:OutputRoot = $OutputRoot
$script:SupervisorId = $SupervisorId
$script:SupervisorRoot = Join-Path $OutputRoot $SupervisorId
$script:CyclesRoot = Join-Path $script:SupervisorRoot "cycles"
$script:ControlRoot = Join-Path $script:SupervisorRoot "control"
$script:StatePath = Join-Path $script:SupervisorRoot "state.json"
$script:StatusLatestPath = Join-Path $script:SupervisorRoot "status-latest.json"
$script:NextStepsLatestPath = Join-Path $script:SupervisorRoot "next-steps-latest.json"
$script:NextStepsLatestMarkdownPath = Join-Path $script:SupervisorRoot "next-steps-latest.md"
$script:OpenIssuesPath = Join-Path $script:SupervisorRoot "open-issues.json"
$script:IncidentsLatestPath = Join-Path $script:SupervisorRoot "incidents-latest.json"
$script:RunsLatestPath = Join-Path $script:SupervisorRoot "runs-latest.json"
$script:MetricsHistoryPath = Join-Path $script:SupervisorRoot "metrics-history.ndjson"
$script:IncidentHistoryPath = Join-Path $script:SupervisorRoot "incident-history.ndjson"
$script:PauseFlagPath = Join-Path $script:ControlRoot "pause.flag"
$script:StopFlagPath = Join-Path $script:ControlRoot "stop.flag"
$script:KillSwitchPath = Join-Path $script:ControlRoot "kill-switch.json"
$script:RunLockPath = Join-Path $script:ControlRoot "run.lock.json"
$script:MutationLockPath = Join-Path $script:ControlRoot "mutation.lock.json"

function Ensure-Directory
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path))
    {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }

    return $Path
}

[void](Ensure-Directory -Path $script:OutputRoot)
[void](Ensure-Directory -Path $script:SupervisorRoot)
[void](Ensure-Directory -Path $script:CyclesRoot)
[void](Ensure-Directory -Path $script:ControlRoot)

function Write-Info
{
    param([string]$Message)
    Write-Host "[INFO ] $Message" -ForegroundColor Cyan
}

function Write-Ok
{
    param([string]$Message)
    Write-Host "[ OK  ] $Message" -ForegroundColor Green
}

function Write-WarnLine
{
    param([string]$Message)
    Write-Host "[WARN ] $Message" -ForegroundColor Yellow
}

function Write-ErrLine
{
    param([string]$Message)
    Write-Host "[ERR  ] $Message" -ForegroundColor Red
}

function Get-SafeName
{
    param([AllowNull()][string]$Value)

    if ([string]::IsNullOrWhiteSpace($Value))
    {
        return "unknown"
    }

    return (($Value -replace "[^A-Za-z0-9\.-]+", "-").Trim("-")).ToLowerInvariant()
}

function Get-JsonText
{
    param(
        [AllowNull()][object]$Object,
        [int]$Depth = 20
    )

    if ($null -eq $Object)
    {
        return "null"
    }

    return ($Object | ConvertTo-Json -Depth $Depth)
}

function Write-JsonFile
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowNull()][object]$Object,
        [int]$Depth = 20
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory))
    {
        [void](Ensure-Directory -Path $directory)
    }

    Set-Content -LiteralPath $Path -Value (Get-JsonText -Object $Object -Depth $Depth) -Encoding UTF8
    return $Path
}

function Write-TextFile
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [AllowNull()][string]$Value
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory))
    {
        [void](Ensure-Directory -Path $directory)
    }

    Set-Content -LiteralPath $Path -Value $Value -Encoding UTF8
    return $Path
}

function Read-JsonFile
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path))
    {
        return $null
    }

    try
    {
        return (Get-Content -LiteralPath $Path -Raw -Encoding UTF8 | ConvertFrom-Json)
    }
    catch
    {
        Write-WarnLine ("Failed to parse JSON file {0}: {1}" -f $Path, $_.Exception.Message)
        return $null
    }
}

function Add-NdJsonRecord
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Record,
        [int]$Depth = 20
    )

    $directory = Split-Path -Parent $Path
    if (-not [string]::IsNullOrWhiteSpace($directory))
    {
        [void](Ensure-Directory -Path $directory)
    }

    Add-Content -LiteralPath $Path -Value ($Record | ConvertTo-Json -Depth $Depth -Compress) -Encoding UTF8
}

function Get-PowerShellExecutable
{
    $pwsh = Get-Command pwsh -ErrorAction SilentlyContinue
    if ($null -ne $pwsh)
    {
        return $pwsh.Source
    }

    $windowsPowerShell = Get-Command powershell -ErrorAction SilentlyContinue
    if ($null -ne $windowsPowerShell)
    {
        return $windowsPowerShell.Source
    }

    throw "Unable to locate pwsh or powershell in PATH."
}

function ConvertTo-QuotedArgument
{
    param([AllowNull()][string]$Value)

    if ($null -eq $Value)
    {
        return '""'
    }

    if ($Value -match '[\s"]')
    {
        return '"' + ($Value.Replace('"', '\"')) + '"'
    }

    return $Value
}

function Join-ProcessArguments
{
    param([string[]]$Arguments)

    if ($null -eq $Arguments -or $Arguments.Count -eq 0)
    {
        return ""
    }

    return (($Arguments | ForEach-Object { ConvertTo-QuotedArgument -Value $_ }) -join " ")
}

function Test-TcpPort
{
    param(
        [string]$HostName = "127.0.0.1",
        [Parameter(Mandatory = $true)][int]$Port,
        [int]$TimeoutMs = 1500
    )

    $client = New-Object System.Net.Sockets.TcpClient
    try
    {
        $asyncResult = $client.BeginConnect($HostName, $Port, $null, $null)
        if (-not $asyncResult.AsyncWaitHandle.WaitOne($TimeoutMs, $false))
        {
            return $false
        }

        $client.EndConnect($asyncResult) | Out-Null
        return $true
    }
    catch
    {
        return $false
    }
    finally
    {
        $client.Close()
    }
}

function Get-OptionalPropertyValue
{
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory = $true)][string]$Name,
        [AllowNull()][object]$Default = $null
    )

    if ($null -eq $Object)
    {
        return $Default
    }

    if ($Object -is [System.Collections.IDictionary] -and $Object.Contains($Name))
    {
        return $Object[$Name]
    }

    $property = $Object.PSObject.Properties[$Name]
    if ($null -ne $property)
    {
        return $property.Value
    }

    return $Default
}

function Get-NestedPropertyValue
{
    param(
        [AllowNull()][object]$Object,
        [Parameter(Mandatory = $true)][string[]]$Path,
        [AllowNull()][object]$Default = $null
    )

    $current = $Object
    foreach ($segment in $Path)
    {
        $current = Get-OptionalPropertyValue -Object $current -Name $segment -Default $null
        if ($null -eq $current)
        {
            return $Default
        }
    }

    return $current
}

function Get-WowProcessSnapshot
{
    $processes = @(Get-Process -Name "WowClassic" -ErrorAction SilentlyContinue)
    return [ordered]@{
        Running = ($processes.Count -gt 0)
        Count = $processes.Count
        Processes = @($processes | ForEach-Object {
                [ordered]@{
                    Id = $_.Id
                    Name = $_.ProcessName
                    StartTimeUtc = $(try { $_.StartTime.ToUniversalTime().ToString("o") } catch { $null })
                    Responding = $(try { $_.Responding } catch { $null })
                }
            })
    }
}

function Get-ServiceProcessSnapshot
{
    $processes = @(Get-Process -Name "dotnet" -ErrorAction SilentlyContinue)
    return [ordered]@{
        DotnetProcessCount = $processes.Count
        Processes = @($processes | Select-Object -First 25 | ForEach-Object {
                [ordered]@{
                    Id = $_.Id
                    Name = $_.ProcessName
                    StartTimeUtc = $(try { $_.StartTime.ToUniversalTime().ToString("o") } catch { $null })
                }
            })
    }
}

function Invoke-SafeApiGet
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [int]$TimeoutSeconds = 10
    )

    $uri = "{0}{1}" -f $BaseUrl.TrimEnd('/'), $Path
    try
    {
        $result = Invoke-RestMethod -Method Get -Uri $uri -TimeoutSec $TimeoutSeconds
        return [ordered]@{
            Success = $true
            Path = $Path
            Uri = $uri
            Error = $null
            Result = $result
        }
    }
    catch
    {
        return [ordered]@{
            Success = $false
            Path = $Path
            Uri = $uri
            Error = $_.Exception.Message
            Result = $null
        }
    }
}

function New-DefaultGateState
{
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [int]$TargetPassCount = 1
    )

    return [ordered]@{
        Name = $Name
        Status = "Pending"
        PassCount = 0
        FailCount = 0
        BlockCount = 0
        TargetPassCount = $TargetPassCount
        LastArtifactTag = $null
        LastSummaryPath = $null
        LastUpdatedUtc = $null
    }
}

function New-DefaultSupervisorState
{
    return [ordered]@{
        SupervisorId = $script:SupervisorId
        CurrentPhase = "Idle"
        LastCycleId = $null
        CycleCount = 0
        LastUpdatedUtc = (Get-Date).ToUniversalTime().ToString("o")
        SyntheticBaseline = [ordered]@{
            Status = "Pending"
            LastRunUtc = $null
            Passed = $false
            ArtifactPath = $null
        }
        Budget = [ordered]@{
            MaxCycleRuntimeMinutes = 120
            MaxRetriesPerIncident = 2
            SameReasonFailureLimit = 2
            MutationCooldownMinutes = 60
            LiveDemotionMinutes = 60
        }
        IncidentQueue = @()
        RetryLedger = [ordered]@{}
        PromotionState = [ordered]@{
            RequestedSurface = $PrimarySurface
            EffectiveSurface = $PrimarySurface
            LiveMode = "Guarded"
            LastDecisionReason = "Initial"
            LastUpdatedUtc = (Get-Date).ToUniversalTime().ToString("o")
            LiveDemotedUntilUtc = $null
        }
        KillSwitchState = [ordered]@{
            Enabled = $false
            Reason = ""
            Source = ""
            UpdatedUtc = $null
        }
        RecentRuns = @()
        Gates = [ordered]@{
            ValidateReroute = (New-DefaultGateState -Name "ValidateReroute")
            ValidateNoProgress = (New-DefaultGateState -Name "ValidateNoProgress")
            ValidateCombat = (New-DefaultGateState -Name "ValidateCombat")
            LiveSession = (New-DefaultGateState -Name "LiveSession" -TargetPassCount 3)
        }
    }
}

function Merge-HashtableDefaults
{
    param(
        [AllowNull()][object]$Existing,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Defaults
    )

    $merged = [ordered]@{}
    foreach ($key in $Defaults.Keys)
    {
        $defaultValue = $Defaults[$key]
        $existingValue = Get-OptionalPropertyValue -Object $Existing -Name $key -Default $null

        if ($defaultValue -is [System.Collections.IDictionary])
        {
            $merged[$key] = Merge-HashtableDefaults -Existing $existingValue -Defaults $defaultValue
        }
        elseif ($null -ne $existingValue)
        {
            $merged[$key] = $existingValue
        }
        else
        {
            $merged[$key] = $defaultValue
        }
    }

    return $merged
}

function Get-SupervisorState
{
    $defaults = New-DefaultSupervisorState
    $existing = Read-JsonFile -Path $script:StatePath
    $state = Merge-HashtableDefaults -Existing $existing -Defaults $defaults
    Sync-KillSwitchState -State $state
    return $state
}

function Save-SupervisorState
{
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)

    $State["LastUpdatedUtc"] = (Get-Date).ToUniversalTime().ToString("o")
    [void](Write-JsonFile -Path $script:StatePath -Object $State)
}

function Get-KillSwitchState
{
    $persisted = Read-JsonFile -Path $script:KillSwitchPath
    if ($null -eq $persisted)
    {
        return [ordered]@{
            Enabled = $false
            Reason = ""
            Source = ""
            UpdatedUtc = $null
        }
    }

    return [ordered]@{
        Enabled = [bool](Get-OptionalPropertyValue -Object $persisted -Name "Enabled" -Default $false)
        Reason = [string](Get-OptionalPropertyValue -Object $persisted -Name "Reason" -Default "")
        Source = [string](Get-OptionalPropertyValue -Object $persisted -Name "Source" -Default "")
        UpdatedUtc = Get-OptionalPropertyValue -Object $persisted -Name "UpdatedUtc" -Default $null
    }
}

function Sync-KillSwitchState
{
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)

    $State["KillSwitchState"] = Get-KillSwitchState
}

function Get-IncidentFingerprint
{
    param([Parameter(Mandatory = $true)][object]$Finding)

    return ("{0}|{1}|{2}" -f `
            [string](Get-OptionalPropertyValue -Object $Finding -Name "Category" -Default ""), `
            [string](Get-OptionalPropertyValue -Object $Finding -Name "Subsystem" -Default ""), `
            [string](Get-OptionalPropertyValue -Object $Finding -Name "BlockerType" -Default "")).ToLowerInvariant()
}

function New-ArtifactRef
{
    param(
        [Parameter(Mandatory = $true)][string]$Kind,
        [Parameter(Mandatory = $true)][string]$Path,
        [string]$Source = "supervisor"
    )

    return [ordered]@{
        Kind = $Kind
        Path = $Path
        Source = $Source
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Metadata = [ordered]@{}
    }
}

function Get-ArtifactRefs
{
    param([string[]]$Paths)

    $artifacts = @()
    foreach ($path in @($Paths))
    {
        if ([string]::IsNullOrWhiteSpace($path))
        {
            continue
        }

        $kind = "Evidence"
        if ($path -like "*.jpg" -or $path -like "*.png")
        {
            $kind = "Screenshot"
        }
        elseif ($path -like "*.json")
        {
            $kind = "Json"
        }
        elseif ($path -like "*.txt" -or $path -like "*.log")
        {
            $kind = "Log"
        }

        $artifacts += New-ArtifactRef -Kind $kind -Path $path
    }

    return @($artifacts)
}

function Get-FailoverDecision
{
    param([Parameter(Mandatory = $true)][object]$Finding)

    $blockerType = [string](Get-OptionalPropertyValue -Object $Finding -Name "BlockerType" -Default "")
    $reason = switch ($blockerType)
    {
        "health unavailable" { "Primary health checks failed, so service recovery should start before any live validation is retried." }
        "launch readiness incomplete" { "Launch readiness is blocking bot start, so doctoring the stack is safer than continuing live retries." }
        "navigation launch check blocking" { "Navigation readiness is blocking launch; escalating through doctor and restart is lower risk than repeated gate retries." }
        "profile drift" { "The wrong profile is loaded, so re-bootstrap the requested profile before continuing guarded live work." }
        "bot inactive after bootstrap" { "Bootstrap completed without an active agent, so restart and re-bootstrap are the bounded recovery path." }
        "bootstrap failed" { "Bootstrap itself failed; escalate from re-bootstrap to doctor to restart within the retry budget." }
        "ValidateCombat" { "Combat validation failed; fall back to a clean bootstrap before attempting another combat gate." }
        "ValidateReroute" { "Reroute validation failed; restart and re-bootstrap before another reroute attempt." }
        "ValidateNoProgress" { "No-progress validation failed; restart and re-bootstrap before another recovery attempt." }
        default { "Use bounded operational remediation before considering code mutation or manual intervention." }
    }

    $primaryAction = switch ($blockerType)
    {
        "health unavailable" { "Restart" }
        "launch readiness incomplete" { "Doctor" }
        "navigation launch check blocking" { "Doctor" }
        "profile drift" { "StartAndValidate" }
        "bot inactive after bootstrap" { "StartAndValidate" }
        "bootstrap failed" { "StartAndValidate" }
        "ValidateCombat" { "StartAndValidate" }
        "ValidateReroute" { "StartAndValidate" }
        "ValidateNoProgress" { "StartAndValidate" }
        default { "Status" }
    }

    $secondaryAction = switch ($blockerType)
    {
        "health unavailable" { "Doctor" }
        "launch readiness incomplete" { "Restart" }
        "navigation launch check blocking" { "Restart" }
        "profile drift" { "Doctor" }
        "bot inactive after bootstrap" { "Doctor" }
        "bootstrap failed" { "Doctor" }
        "ValidateCombat" { "Doctor" }
        "ValidateReroute" { "Doctor" }
        "ValidateNoProgress" { "Doctor" }
        default { "Restart" }
    }

    $tertiaryAction = switch ($blockerType)
    {
        "health unavailable" { "StartAndValidate" }
        "launch readiness incomplete" { "StartAndValidate" }
        "navigation launch check blocking" { "StartAndValidate" }
        "profile drift" { "Restart" }
        "bot inactive after bootstrap" { "Restart" }
        "bootstrap failed" { "Restart" }
        "ValidateCombat" { "Restart" }
        "ValidateReroute" { "Restart" }
        "ValidateNoProgress" { "Restart" }
        default { "Stop" }
    }

    return [ordered]@{
        PrimaryAction = $primaryAction
        SecondaryAction = $secondaryAction
        TertiaryAction = $tertiaryAction
        DecisionReason = $reason
        DemoteLiveMode = ($blockerType -in @("health unavailable", "launch readiness incomplete", "navigation launch check blocking", "bootstrap failed", "profile drift"))
        TargetSurface = $(if ($blockerType -eq "ValidateCombat") { "Hybrid" } else { "SyntheticOnly" })
    }
}

function Convert-FindingToIncident
{
    param(
        [Parameter(Mandatory = $true)][object]$Finding,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][string]$CycleId,
        [Parameter(Mandatory = $true)][string]$CycleDir
    )

    $fingerprint = Get-IncidentFingerprint -Finding $Finding
    $existing = ($State["IncidentQueue"] | Where-Object { $_.Fingerprint -eq $fingerprint } | Select-Object -First 1)
    $nowUtc = (Get-Date).ToUniversalTime().ToString("o")
    $retryEntry = Get-OptionalPropertyValue -Object $State["RetryLedger"] -Name $fingerprint -Default $null
    $evidencePaths = @((Get-OptionalPropertyValue -Object $Finding -Name "EvidencePaths" -Default @()))
    $artifacts = Get-ArtifactRefs -Paths $evidencePaths
    $screenshotArtifact = ($artifacts | Where-Object { $_.Kind -eq "Screenshot" } | Select-Object -First 1)
    $failover = Get-FailoverDecision -Finding $Finding
    $incidentId = $(if ($null -ne $existing) { $existing.Id } else { "incident-{0}" -f [guid]::NewGuid().ToString("N").Substring(0, 8) })
    $correlationId = $(if ($null -ne $existing) { $existing.CorrelationId } else { [guid]::NewGuid().ToString("N") })

    return [ordered]@{
        Id = $incidentId
        Fingerprint = $fingerprint
        CorrelationId = $correlationId
        Category = [string](Get-OptionalPropertyValue -Object $Finding -Name "Category" -Default "")
        Subsystem = [string](Get-OptionalPropertyValue -Object $Finding -Name "Subsystem" -Default "")
        Severity = $(switch ([string](Get-OptionalPropertyValue -Object $Finding -Name "Priority" -Default "P3"))
            {
                "P1" { "Critical" }
                "P2" { "Error" }
                default { "Warning" }
            })
        Gate = [string](Get-OptionalPropertyValue -Object $Finding -Name "BlockerType" -Default "")
        Reason = [string](Get-OptionalPropertyValue -Object $Finding -Name "BlockerType" -Default "")
        Summary = [string](Get-OptionalPropertyValue -Object $Finding -Name "Summary" -Default "")
        Status = "Open"
        Outcome = "Open"
        OccurrenceCount = $(if ($null -ne $existing) { [int]$existing.OccurrenceCount + 1 } else { 1 })
        RetryCount = [int](Get-OptionalPropertyValue -Object $retryEntry -Name "Attempts" -Default 0)
        FirstSeenUtc = $(if ($null -ne $existing) { $existing.FirstSeenUtc } else { $nowUtc })
        LastSeenUtc = $nowUtc
        Artifacts = @($artifacts)
        Screenshot = $(if ($null -ne $screenshotArtifact)
            {
                [ordered]@{
                    RequestId = ""
                    CorrelationId = $correlationId
                    IncidentId = $incidentId
                    Reason = "supervisor-artifact"
                    Path = $screenshotArtifact.Path
                    RequestedUtc = $nowUtc
                    CompletedUtc = $nowUtc
                    CaptureLatencyMs = $null
                    Success = $true
                    Error = $null
                }
            }
            else
            {
                $null
            })
        Failover = $failover
        RemediationTask = $null
        Metadata = [ordered]@{
            CycleId = $CycleId
            CycleDir = $CycleDir
            Priority = [string](Get-OptionalPropertyValue -Object $Finding -Name "Priority" -Default "")
        }
    }
}

function Get-IncidentsForCycle
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][object[]]$Findings,
        [Parameter(Mandatory = $true)][string]$CycleId,
        [Parameter(Mandatory = $true)][string]$CycleDir
    )

    $incidents = @()
    foreach ($finding in @($Findings))
    {
        if ([string](Get-OptionalPropertyValue -Object $finding -Name "Category" -Default "") -eq "optimization")
        {
            continue
        }

        $incidents += Convert-FindingToIncident -Finding $finding -State $State -CycleId $CycleId -CycleDir $CycleDir
    }

    if ($incidents.Count -eq 0)
    {
        return @()
    }

    return @($incidents | Sort-Object @{ Expression = { [int]$_.OccurrenceCount } ; Descending = $true }, @{ Expression = { $_.Severity } ; Descending = $true })
}

function Update-RetryLedgerForIncidents
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Incidents
    )

    $ledger = $State["RetryLedger"]
    foreach ($incident in @($Incidents))
    {
        if ([string](Get-OptionalPropertyValue -Object $incident -Name "Category" -Default "") -eq "optimization")
        {
            continue
        }

        $fingerprint = [string](Get-OptionalPropertyValue -Object $incident -Name "Fingerprint" -Default "")
        if ([string]::IsNullOrWhiteSpace($fingerprint))
        {
            continue
        }

        $entry = Get-OptionalPropertyValue -Object $ledger -Name $fingerprint -Default $null
        if ($null -eq $entry)
        {
            $entry = [ordered]@{
                Fingerprint = $fingerprint
                Attempts = 0
                SameReasonFailures = 0
                LastAction = ""
                LastOutcome = "Observed"
                LastAttemptUtc = $null
            }
        }

        $entry["SameReasonFailures"] = [int](Get-OptionalPropertyValue -Object $entry -Name "SameReasonFailures" -Default 0) + 1
        $entry["LastOutcome"] = "Observed"
        $ledger[$fingerprint] = $entry
    }
}

function Update-PromotionStateFromIncidents
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Incidents
    )

    $promotionState = $State["PromotionState"]
    $budget = $State["Budget"]
    $sameReasonLimit = [int](Get-OptionalPropertyValue -Object $budget -Name "SameReasonFailureLimit" -Default 2)
    $liveDemotionMinutes = [int](Get-OptionalPropertyValue -Object $budget -Name "LiveDemotionMinutes" -Default 60)

    $demote = $false
    $decisionReason = "No demotion required."

    foreach ($incident in @($Incidents))
    {
        $fingerprint = [string](Get-OptionalPropertyValue -Object $incident -Name "Fingerprint" -Default "")
        $ledgerEntry = Get-OptionalPropertyValue -Object $State["RetryLedger"] -Name $fingerprint -Default $null
        $sameReasonFailures = [int](Get-OptionalPropertyValue -Object $ledgerEntry -Name "SameReasonFailures" -Default 0)
        if ($sameReasonFailures -ge $sameReasonLimit)
        {
            $demote = $true
            $decisionReason = "Repeated same-reason failures reached the guarded-live cap."
            break
        }
    }

    if ([bool](Get-OptionalPropertyValue -Object $State["KillSwitchState"] -Name "Enabled" -Default $false))
    {
        $demote = $true
        $decisionReason = "Kill switch enabled."
    }

    if ($demote)
    {
        $promotionState["EffectiveSurface"] = "SyntheticOnly"
        $promotionState["LastDecisionReason"] = $decisionReason
        $promotionState["LiveDemotedUntilUtc"] = (Get-Date).AddMinutes($liveDemotionMinutes).ToUniversalTime().ToString("o")
        $promotionState["LastUpdatedUtc"] = (Get-Date).ToUniversalTime().ToString("o")
        return
    }

    $demotedUntilUtcText = [string](Get-OptionalPropertyValue -Object $promotionState -Name "LiveDemotedUntilUtc" -Default "")
    if (-not [string]::IsNullOrWhiteSpace($demotedUntilUtcText))
    {
        try
        {
            $demotedUntilUtc = [DateTime]::Parse($demotedUntilUtcText, $null, [System.Globalization.DateTimeStyles]::RoundtripKind)
            if ($demotedUntilUtc -le (Get-Date).ToUniversalTime())
            {
                $promotionState["EffectiveSurface"] = $promotionState["RequestedSurface"]
                $promotionState["LastDecisionReason"] = "Guarded live demotion expired."
                $promotionState["LiveDemotedUntilUtc"] = $null
                $promotionState["LastUpdatedUtc"] = (Get-Date).ToUniversalTime().ToString("o")
            }
        }
        catch
        {
        }
    }
}

function Test-LiveSurfaceAllowed
{
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)

    if ([bool](Get-OptionalPropertyValue -Object $State["KillSwitchState"] -Name "Enabled" -Default $false))
    {
        return $false
    }

    $effectiveSurface = [string](Get-OptionalPropertyValue -Object $State["PromotionState"] -Name "EffectiveSurface" -Default $PrimarySurface)
    if ($effectiveSurface -eq "SyntheticOnly")
    {
        return $false
    }

    return $true
}

function Get-GitWorkspaceAssessment
{
    $assessment = [ordered]@{
        Branch = $null
        Head = $null
        IsDirty = $null
        DirtyFiles = @()
    }

    try
    {
        $assessment["Branch"] = (git -C $script:BotRoot rev-parse --abbrev-ref HEAD 2>$null).Trim()
        $assessment["Head"] = (git -C $script:BotRoot rev-parse --short HEAD 2>$null).Trim()
        $dirty = @(git -C $script:BotRoot status --porcelain 2>$null)
        $assessment["IsDirty"] = ($dirty.Count -gt 0)
        $assessment["DirtyFiles"] = @($dirty)
    }
    catch
    {
        $assessment["IsDirty"] = $null
    }

    return $assessment
}

function Update-RecentRuns
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$CycleSummary,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Incidents
    )

    $run = [ordered]@{
        CycleId = $CycleSummary["CycleId"]
        CurrentPhase = $CycleSummary["CurrentPhase"]
        StartedUtc = Get-OptionalPropertyValue -Object $CycleSummary -Name "StartedUtc" -Default $null
        CompletedUtc = Get-OptionalPropertyValue -Object $CycleSummary -Name "CompletedUtc" -Default $null
        SyntheticPassed = [bool](Get-OptionalPropertyValue -Object $CycleSummary["SyntheticBaseline"] -Name "Passed" -Default $false)
        LiveAttempted = ($null -ne $CycleSummary["Bootstrap"])
        LiveValid = [bool](Get-OptionalPropertyValue -Object $CycleSummary["LiveAssessment"] -Name "Valid" -Default $false)
        InvalidReason = [string](Get-OptionalPropertyValue -Object $CycleSummary["LiveAssessment"] -Name "InvalidReason" -Default "")
        IncidentIds = @(@($Incidents | ForEach-Object { $_.Id }))
        AppliedActions = @(@($CycleSummary["AppliedChanges"] | ForEach-Object { $_.ActionName }))
    }

    $recentRuns = @($State["RecentRuns"])
    $recentRuns = ,$run + $recentRuns
    if ($recentRuns.Count -gt 10)
    {
        $recentRuns = @($recentRuns | Select-Object -First 10)
    }

    $State["RecentRuns"] = $recentRuns
}

function Test-ProcessIdRunning
{
    param([int]$ProcessId)

    if ($ProcessId -le 0)
    {
        return $false
    }

    try
    {
        $null = Get-Process -Id $ProcessId -ErrorAction Stop
        return $true
    }
    catch
    {
        return $false
    }
}

function Acquire-JsonLock
{
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $existing = Read-JsonFile -Path $Path
    if ($null -ne $existing)
    {
        $existingProcessId = [int](Get-OptionalPropertyValue -Object $existing -Name "ProcessId" -Default 0)
        if (Test-ProcessIdRunning -ProcessId $existingProcessId)
        {
            throw ("{0} is already running under PID {1}." -f $Name, $existingProcessId)
        }
    }

    $lock = [ordered]@{
        Name = $Name
        ProcessId = $PID
        AcquiredUtc = (Get-Date).ToUniversalTime().ToString("o")
        MachineName = $env:COMPUTERNAME
    }
    [void](Write-JsonFile -Path $Path -Object $lock)
    return $lock
}

function Release-JsonLock
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path)
    {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Set-ControlFlag
{
    param([Parameter(Mandatory = $true)][string]$Path)

    [void](Write-TextFile -Path $Path -Value ((Get-Date).ToUniversalTime().ToString("o")))
}

function Clear-ControlFlag
{
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-Path -LiteralPath $Path)
    {
        Remove-Item -LiteralPath $Path -Force
    }
}

function Test-PauseRequested
{
    return (Test-Path -LiteralPath $script:PauseFlagPath)
}

function Test-StopRequested
{
    return (Test-Path -LiteralPath $script:StopFlagPath)
}

function Wait-ForResume
{
    while (Test-PauseRequested)
    {
        if (Test-StopRequested)
        {
            break
        }

        Start-Sleep -Seconds 2
    }
}

function Get-CycleId
{
    return (Get-Date -Format "yyyyMMdd-HHmmss")
}

function Get-CycleDir
{
    param([Parameter(Mandatory = $true)][string]$CycleId)

    $path = Join-Path $script:CyclesRoot ("cycle-{0}" -f $CycleId)
    [void](Ensure-Directory -Path $path)
    return $path
}

function Add-CycleObservation
{
    param(
        [Parameter(Mandatory = $true)][string]$CycleDir,
        [Parameter(Mandatory = $true)][string]$Phase,
        [Parameter(Mandatory = $true)][string]$Subsystem,
        [Parameter(Mandatory = $true)][string]$ArtifactSource,
        [AllowNull()][object]$Context
    )

    $record = [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Phase = $Phase
        Subsystem = $Subsystem
        ArtifactSource = $ArtifactSource
        Context = $Context
    }

    Add-NdJsonRecord -Path (Join-Path $CycleDir "observations.ndjson") -Record $record
}

function Get-EnvironmentObservation
{
    return [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Ports = [ordered]@{
            Web5000 = (Test-TcpPort -Port 5000)
            Navigation47110 = (Test-TcpPort -Port 47110)
        }
        Wow = Get-WowProcessSnapshot
        Services = Get-ServiceProcessSnapshot
    }
}

function Get-SnapshotPayload
{
    param([AllowNull()][object]$ApiResult)

    if ($null -eq $ApiResult -or -not [bool](Get-OptionalPropertyValue -Object $ApiResult -Name "Success" -Default $false))
    {
        return $null
    }

    $root = Get-OptionalPropertyValue -Object $ApiResult -Name "Result" -Default $null
    if ($null -eq $root)
    {
        return $null
    }

    $snapshot = Get-NestedPropertyValue -Object $root -Path @("data", "snapshot") -Default $null
    if ($null -eq $snapshot)
    {
        $snapshot = Get-OptionalPropertyValue -Object $root -Name "snapshot" -Default $null
    }

    return $snapshot
}

function Get-LiveApiObservation
{
    $health = Invoke-SafeApiGet -Path "/api/health"
    $launchStatus = Invoke-SafeApiGet -Path "/api/launch/status"
    $botStatus = Invoke-SafeApiGet -Path "/api/bot/status"
    $snapshot = Invoke-SafeApiGet -Path "/api/test/snapshot"
    $sessionStats = Invoke-SafeApiGet -Path "/api/session/stats"

    return [ordered]@{
        Health = $health
        LaunchStatus = $launchStatus
        BotStatus = $botStatus
        Snapshot = $snapshot
        SessionStats = $sessionStats
    }
}

function Get-LiveDetailedObservation
{
    $base = Get-LiveApiObservation
    $base["Session"] = Invoke-SafeApiGet -Path "/api/session"
    $base["NavigationRuntime"] = Invoke-SafeApiGet -Path "/api/diagnostics/navigation/runtime"
    $base["NavigationReroute"] = Invoke-SafeApiGet -Path "/api/diagnostics/navigation/reroute"
    $base["SoakCurrent"] = Invoke-SafeApiGet -Path "/api/diagnostics/soak/current"
    return $base
}

function Convert-LaunchStatusName
{
    param([AllowNull()][object]$StatusValue)

    if ($null -eq $StatusValue)
    {
        return "Unknown"
    }

    $code = 0
    if ($StatusValue -is [int] -or [int]::TryParse([string]$StatusValue, [ref]$code))
    {
        switch ($code)
        {
            0 { return "Pending" }
            1 { return "Warning" }
            2 { return "Ok" }
            3 { return "Error" }
            default { return "Unknown" }
        }
    }

    $text = [string]$StatusValue
    if ([string]::IsNullOrWhiteSpace($text))
    {
        return "Unknown"
    }

    switch -Regex ($text)
    {
        "^(?i)ok$" { return "Ok" }
        "^(?i)warning$" { return "Warning" }
        "^(?i)pending$" { return "Pending" }
        "^(?i)error$" { return "Error" }
        default { return "Unknown" }
    }
}

function Get-LaunchNavigationAssessment
{
    param([AllowNull()][object]$LaunchPayload)

    $assessment = [ordered]@{
        NavigationCheckStatus = "Unknown"
        NavigationCheckMessage = $null
        NavigationSource = "launch-check"
        NavigationBlockingReason = $null
        NavigationCheckIsBlocking = $false
        NavigationCheckPresent = $false
        NavigationCheckIsOk = $false
    }

    if ($null -eq $LaunchPayload)
    {
        $assessment["NavigationSource"] = "port-check"
        $assessment["NavigationBlockingReason"] = "launch payload unavailable"
        return $assessment
    }

    $checks = Get-OptionalPropertyValue -Object $LaunchPayload -Name "checks" -Default $null
    if ($null -eq $checks)
    {
        $assessment["NavigationBlockingReason"] = "navigation launch check missing"
        return $assessment
    }

    $navigationCheck = $null
    foreach ($check in @($checks))
    {
        $title = [string](Get-OptionalPropertyValue -Object $check -Name "title" -Default "")
        $subsystem = [string](Get-OptionalPropertyValue -Object $check -Name "subsystem" -Default "")
        if ($title -ieq "Navigation" -or $subsystem -eq "0")
        {
            $navigationCheck = $check
            break
        }
    }

    if ($null -eq $navigationCheck)
    {
        $assessment["NavigationBlockingReason"] = "navigation launch check missing"
        return $assessment
    }

    $assessment["NavigationCheckPresent"] = $true
    $statusRaw = Get-OptionalPropertyValue -Object $navigationCheck -Name "status" -Default $null
    $statusName = Convert-LaunchStatusName -StatusValue $statusRaw
    $message = [string](Get-OptionalPropertyValue -Object $navigationCheck -Name "message" -Default "")
    $isBlocking = [bool](Get-OptionalPropertyValue -Object $navigationCheck -Name "isBlocking" -Default $false)

    $isOk = ($statusName -eq "Ok")
    if (-not $isOk -and $message -match "(?i)remotev3\s+connected\s*\(hybrid\)")
    {
        # Treat this historical message variant as equivalent to Navigation=Ok in hybrid mode.
        $isOk = $true
        $statusName = "Ok"
    }

    $assessment["NavigationCheckStatus"] = $statusName
    $assessment["NavigationCheckMessage"] = $message
    $assessment["NavigationCheckIsBlocking"] = $isBlocking
    $assessment["NavigationCheckIsOk"] = $isOk
    if (-not $isOk -and $isBlocking)
    {
        $assessment["NavigationBlockingReason"] = $(if ([string]::IsNullOrWhiteSpace($message))
            {
                "navigation launch check is blocking"
            }
            else
            {
                "navigation launch check blocking: $message"
            })
    }

    return $assessment
}

function Invoke-ManagedProcess
{
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [string[]]$ArgumentList = @(),
        [string]$WorkingDirectory = $script:BotRoot,
        [Parameter(Mandatory = $true)][string]$CycleDir,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$TimeoutSeconds = 0,
        [switch]$CollectLiveObservations
    )

    [void](Ensure-Directory -Path $CycleDir)
    $stdoutPath = Join-Path $CycleDir ("{0}-stdout.log" -f (Get-SafeName -Value $Label))
    $stderrPath = Join-Path $CycleDir ("{0}-stderr.log" -f (Get-SafeName -Value $Label))
    $argumentLine = Join-ProcessArguments -Arguments $ArgumentList

    if (Test-PauseRequested)
    {
        Wait-ForResume
    }

    Write-Info ("Starting {0}: {1} {2}" -f $Label, $FilePath, $argumentLine)
    $startedUtc = (Get-Date).ToUniversalTime()
    $process = Start-Process -FilePath $FilePath `
        -ArgumentList $argumentLine `
        -WorkingDirectory $WorkingDirectory `
        -RedirectStandardOutput $stdoutPath `
        -RedirectStandardError $stderrPath `
        -PassThru `
        -WindowStyle Hidden
    $null = $process.Handle

    $lastHeartbeat = Get-Date
    $lastObservation = (Get-Date).AddSeconds(-1 * [Math]::Max(1, $ObservationSeconds))
    $timedOut = $false
    $stoppedBySupervisor = $false

    while (-not $process.HasExited)
    {
        if (Test-StopRequested)
        {
            Write-WarnLine ("Stop requested while {0} was running." -f $Label)
            try
            {
                $process.Kill()
            }
            catch { }
            $stoppedBySupervisor = $true
            break
        }

        if (Test-PauseRequested)
        {
            Add-CycleObservation -CycleDir $CycleDir -Phase $Label -Subsystem "control" -ArtifactSource "pause" -Context ([ordered]@{ Message = "Supervisor paused while child process was active." })
            Wait-ForResume
            Add-CycleObservation -CycleDir $CycleDir -Phase $Label -Subsystem "control" -ArtifactSource "resume" -Context ([ordered]@{ Message = "Supervisor resumed child-process monitoring." })
        }

        $now = Get-Date
        if (($now - $lastHeartbeat).TotalSeconds -ge $HeartbeatSeconds)
        {
            Add-CycleObservation -CycleDir $CycleDir -Phase $Label -Subsystem "heartbeat" -ArtifactSource "environment" -Context (Get-EnvironmentObservation)
            $lastHeartbeat = $now
        }

        if ($CollectLiveObservations -and ($now - $lastObservation).TotalSeconds -ge $ObservationSeconds)
        {
            Add-CycleObservation -CycleDir $CycleDir -Phase $Label -Subsystem "live" -ArtifactSource "api" -Context (Get-LiveDetailedObservation)
            $lastObservation = $now
        }

        if ($TimeoutSeconds -gt 0 -and ($now - $startedUtc).TotalSeconds -ge $TimeoutSeconds)
        {
            Write-WarnLine ("Timeout reached for {0} after {1}s." -f $Label, $TimeoutSeconds)
            try
            {
                $process.Kill()
            }
            catch { }
            $timedOut = $true
            break
        }

        Start-Sleep -Seconds 2
        $process.Refresh()
    }

    try
    {
        $process.WaitForExit()
    }
    catch { }

    $completedUtc = (Get-Date).ToUniversalTime()
    $exitCode = $(if ($process.HasExited) { $process.ExitCode } else { -1 })
    $result = [ordered]@{
        Label = $Label
        FilePath = $FilePath
        Arguments = $ArgumentList
        WorkingDirectory = $WorkingDirectory
        ProcessId = $process.Id
        StartedUtc = $startedUtc.ToString("o")
        CompletedUtc = $completedUtc.ToString("o")
        DurationSeconds = [math]::Round(($completedUtc - $startedUtc).TotalSeconds, 2)
        TimedOut = $timedOut
        StoppedBySupervisor = $stoppedBySupervisor
        ExitCode = $exitCode
        Success = (-not $timedOut -and -not $stoppedBySupervisor -and $exitCode -eq 0)
        StdoutPath = $stdoutPath
        StderrPath = $stderrPath
    }

    [void](Write-JsonFile -Path (Join-Path $CycleDir ("{0}-process-result.json" -f (Get-SafeName -Value $Label))) -Object $result)
    return $result
}

function Invoke-ManagedPowerShellScript
{
    param(
        [Parameter(Mandatory = $true)][string]$ScriptPath,
        [string[]]$ScriptArguments = @(),
        [Parameter(Mandatory = $true)][string]$CycleDir,
        [Parameter(Mandatory = $true)][string]$Label,
        [int]$TimeoutSeconds = 0,
        [switch]$CollectLiveObservations
    )

    $psExe = Get-PowerShellExecutable
    $arguments = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath) + $ScriptArguments
    return (Invoke-ManagedProcess -FilePath $psExe -ArgumentList $arguments -WorkingDirectory $script:BotRoot -CycleDir $CycleDir -Label $Label -TimeoutSeconds $TimeoutSeconds -CollectLiveObservations:$CollectLiveObservations)
}

function Get-GateOrder
{
    $order = @()
    foreach ($item in ($PriorityGateOrder -split ","))
    {
        $trimmed = $item.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed))
        {
            $order += $trimmed
        }
    }

    if ($order.Count -eq 0)
    {
        return @("ValidateCombat", "ValidateReroute", "ValidateNoProgress", "LiveSession")
    }

    return $order
}

function Get-ArtifactByPattern
{
    param(
        [Parameter(Mandatory = $true)][string]$DirectoryPath,
        [Parameter(Mandatory = $true)][string]$Pattern
    )

    if (-not (Test-Path -LiteralPath $DirectoryPath))
    {
        return $null
    }

    $matches = @(Get-ChildItem -LiteralPath $DirectoryPath -Filter $Pattern -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTimeUtc)

    if ($matches.Count -eq 0)
    {
        return $null
    }

    return $matches[-1].FullName
}

function Get-GateSummaryPattern
{
    param([Parameter(Mandatory = $true)][string]$GateName)

    switch ($GateName)
    {
        "ValidateReroute" { return "*-validatereroute-summary.json" }
        "ValidateNoProgress" { return "*-validatenoprogress-summary.json" }
        "ValidateCombat" { return "*-validatecombat-summary.json" }
        "LiveSession" { return "*-livesession-summary.json" }
        default { return "*-summary.json" }
    }
}

function Get-GateSummaryPath
{
    param([Parameter(Mandatory = $true)][string]$ArtifactTag)

    $artifactDir = Join-Path $script:BotRoot ("logs\{0}" -f $ArtifactTag)
    $gateName = $ArtifactTag.Split('-')[-1]
    $pattern = Get-GateSummaryPattern -GateName $gateName
    return (Get-ArtifactByPattern -DirectoryPath $artifactDir -Pattern $pattern)
}

function Get-LiveStateAssessment
{
    $observation = Get-LiveApiObservation
    $healthResult = $observation.Health
    $launchResult = $observation.LaunchStatus
    $botStatusResult = $observation.BotStatus
    $snapshotResult = $observation.Snapshot
    $sessionStatsResult = $observation.SessionStats

    $launchPayload = Get-OptionalPropertyValue -Object $launchResult -Name "Result" -Default $null
    $botStatus = Get-OptionalPropertyValue -Object $botStatusResult -Name "Result" -Default $null
    $snapshot = Get-SnapshotPayload -ApiResult $snapshotResult
    $sessionStats = Get-OptionalPropertyValue -Object $sessionStatsResult -Name "Result" -Default $null
    $webPortListening = Test-TcpPort -Port 5000
    $navigationPortListening = Test-TcpPort -Port 47110

    $isLaunchReady = $false
    if ($null -ne $launchPayload)
    {
        $isLaunchReady = [bool](Get-OptionalPropertyValue -Object $launchPayload -Name "isLaunchReady" -Default $false)
        if (-not $isLaunchReady)
        {
            $isLaunchReady = [bool](Get-OptionalPropertyValue -Object $launchPayload -Name "canStartBot" -Default $false)
        }
    }
    $navigationAssessment = Get-LaunchNavigationAssessment -LaunchPayload $launchPayload
    $navigationCheckStatus = [string](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationCheckStatus" -Default "Unknown")
    $navigationCheckMessage = [string](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationCheckMessage" -Default "")
    $navigationSource = [string](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationSource" -Default "launch-check")
    $navigationBlockingReason = [string](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationBlockingReason" -Default "")
    $navigationCheckIsBlocking = [bool](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationCheckIsBlocking" -Default $false)
    $navigationCheckPresent = [bool](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationCheckPresent" -Default $false)
    $navigationCheckIsOk = [bool](Get-OptionalPropertyValue -Object $navigationAssessment -Name "NavigationCheckIsOk" -Default $false)

    $botActive = [bool](Get-OptionalPropertyValue -Object $botStatus -Name "isActive" -Default $false)
    $agentAvailable = [bool](Get-OptionalPropertyValue -Object $botStatus -Name "agentAvailable" -Default $false)
    $runtimeMode = [string](Get-OptionalPropertyValue -Object $botStatus -Name "runtimeMode" -Default "")
    $currentGoal = [string](Get-OptionalPropertyValue -Object $botStatus -Name "currentGoal" -Default "")
    $profileName = [string](Get-OptionalPropertyValue -Object $botStatus -Name "profileName" -Default "")
    $snapshotDead = [bool](Get-OptionalPropertyValue -Object $snapshot -Name "dead" -Default $false)
    $snapshotSwimming = [bool](Get-OptionalPropertyValue -Object $snapshot -Name "swimming" -Default $false)
    $chatInputVisible = [bool](Get-OptionalPropertyValue -Object $snapshot -Name "chatInputVisible" -Default $false)
    $inCombat = [bool](Get-OptionalPropertyValue -Object $snapshot -Name "inCombat" -Default $false)

    $invalidReason = $null
    if (-not [bool](Get-OptionalPropertyValue -Object $healthResult -Name "Success" -Default $false))
    {
        $invalidReason = "health unavailable"
    }
    elseif (-not $isLaunchReady)
    {
        $invalidReason = "launch readiness incomplete"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($navigationBlockingReason))
    {
        $invalidReason = "navigation launch check blocking"
    }
    elseif ($snapshotDead -or $currentGoal -like "*Corpse*")
    {
        $invalidReason = "death/corpse recovery"
    }
    elseif ($chatInputVisible)
    {
        $invalidReason = "chat input visible"
    }
    elseif ($snapshotSwimming)
    {
        $invalidReason = "swimming state"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($profileName) -and $profileName -ne $Profile)
    {
        $invalidReason = "profile drift"
    }
    elseif (-not [string]::IsNullOrWhiteSpace($currentGoal) -and $currentGoal -like "Adhoc*")
    {
        $invalidReason = "route-follow not restored"
    }

    return [ordered]@{
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Valid = [string]::IsNullOrWhiteSpace($invalidReason)
        InvalidReason = $invalidReason
        WebPortListening = $webPortListening
        NavigationPortListening = $navigationPortListening
        NavigationCheckStatus = $navigationCheckStatus
        NavigationCheckMessage = $navigationCheckMessage
        NavigationSource = $navigationSource
        NavigationBlockingReason = $navigationBlockingReason
        NavigationCheckIsBlocking = $navigationCheckIsBlocking
        NavigationCheckPresent = $navigationCheckPresent
        NavigationCheckIsOk = $navigationCheckIsOk
        LaunchReady = $isLaunchReady
        RuntimeMode = $runtimeMode
        ProfileName = $profileName
        RequestedProfile = $Profile
        BotActive = $botActive
        AgentAvailable = $agentAvailable
        CurrentGoal = $currentGoal
        InCombat = $inCombat
        Snapshot = $snapshot
        SessionStats = $sessionStats
        Observation = $observation
    }
}

function Invoke-SyntheticBaseline
{
    param([Parameter(Mandatory = $true)][string]$CycleDir)

    $result = [ordered]@{
        Stage = "SyntheticBaseline"
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        Build = $null
        CoreTests = $null
        FrontendTests = $null
        LegacyHarness = $null
        Passed = $false
    }

    $buildDir = Join-Path $CycleDir "synthetic-build"
    [void](Ensure-Directory -Path $buildDir)
    $result["Build"] = Invoke-ManagedProcess -FilePath "dotnet" `
        -ArgumentList @("build", ".\MasterOfPuppets.sln", "-c", "Release", "--nologo", "-v", "quiet") `
        -WorkingDirectory $script:BotRoot `
        -CycleDir $buildDir `
        -Label "dotnet-build"

    if (-not [bool]$result["Build"].Success)
    {
        return $result
    }

    $coreDir = Join-Path $CycleDir "synthetic-coretests"
    [void](Ensure-Directory -Path $coreDir)
    $result["CoreTests"] = Invoke-ManagedProcess -FilePath "dotnet" `
        -ArgumentList @("test", ".\CoreUnitTests\CoreUnitTests.csproj", "-c", "Release", "--nologo", "-v", "quiet") `
        -WorkingDirectory $script:BotRoot `
        -CycleDir $coreDir `
        -Label "dotnet-test-core"

    if (-not [bool]$result["CoreTests"].Success)
    {
        return $result
    }

    $frontendDir = Join-Path $CycleDir "synthetic-frontendtests"
    [void](Ensure-Directory -Path $frontendDir)
    $result["FrontendTests"] = Invoke-ManagedProcess -FilePath "dotnet" `
        -ArgumentList @("test", ".\FrontendUnitTests\FrontendUnitTests.csproj", "-c", "Release", "--nologo", "-v", "quiet") `
        -WorkingDirectory $script:BotRoot `
        -CycleDir $frontendDir `
        -Label "dotnet-test-frontend"

    if (([int]$LegacyHarnessEveryNCycles) -gt 0 -and (Test-Path -LiteralPath $script:LegacyHarnessPath))
    {
        $state = Get-SupervisorState
        $nextCycleIndex = [int]$state["CycleCount"] + 1
        if (($nextCycleIndex % [int]$LegacyHarnessEveryNCycles) -eq 0)
        {
            $legacyDir = Join-Path $CycleDir "legacy-harness"
            [void](Ensure-Directory -Path $legacyDir)
            $result["LegacyHarness"] = Invoke-ManagedPowerShellScript -ScriptPath $script:LegacyHarnessPath `
                -ScriptArguments @("-Stages", $LegacyHarnessStages, "-OutputPath", $legacyDir) `
                -CycleDir $legacyDir `
                -Label "legacy-harness" `
                -TimeoutSeconds 3600
        }
    }

    $result["Passed"] = ([bool]$result["Build"].Success -and [bool]$result["CoreTests"].Success -and [bool]$result["FrontendTests"].Success)
    return $result
}

function Invoke-AgentControlAction
{
    param(
        [Parameter(Mandatory = $true)][string]$CycleDir,
        [Parameter(Mandatory = $true)][string]$ActionName,
        [Parameter(Mandatory = $true)][string]$ArtifactTag
    )

    $arguments = @(
        "-Action", $ActionName,
        "-Profile", $Profile,
        "-NavProfile", $NavProfile,
        "-ArtifactTag", $ArtifactTag,
        "-BaseUrl", $BaseUrl,
        "-SoakMinutes", [string]$SoakMinutes,
        "-WindowMinutes", [string]$WindowMinutes,
        "-EvidenceIntervalSeconds", [string]$EvidenceIntervalSeconds,
        "-ShortValidationSeconds", [string]$ShortValidationSeconds
    )

    $timeoutSeconds = $MaxAgentControlRuntimeSeconds
    if ($ActionName -eq "LiveSession")
    {
        $timeoutSeconds = [Math]::Max($MaxAgentControlRuntimeSeconds, ($SoakMinutes * 60) + 1800)
    }

    $actionDir = Join-Path $CycleDir ("agentctl-{0}" -f (Get-SafeName -Value $ActionName))
    [void](Ensure-Directory -Path $actionDir)
    $processResult = Invoke-ManagedPowerShellScript -ScriptPath $script:AgentControlPath `
        -ScriptArguments $arguments `
        -CycleDir $actionDir `
        -Label ("agentctl-{0}" -f $ActionName) `
        -TimeoutSeconds $timeoutSeconds `
        -CollectLiveObservations

    $artifactDir = Join-Path $script:BotRoot ("logs\{0}" -f $ArtifactTag)
    $summaryPath = Get-ArtifactByPattern -DirectoryPath $artifactDir -Pattern (Get-GateSummaryPattern -GateName $ActionName)
    $summary = $(if ($null -ne $summaryPath) { Read-JsonFile -Path $summaryPath } else { $null })

    return [ordered]@{
        ActionName = $ActionName
        ArtifactTag = $ArtifactTag
        ArtifactDir = $artifactDir
        ProcessResult = $processResult
        SummaryPath = $summaryPath
        Summary = $summary
    }
}

function Get-NextGateAction
{
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)

    $gateOrder = Get-GateOrder
    foreach ($gateName in $gateOrder)
    {
        $gateState = $State["Gates"][$gateName]
        if ($gateName -eq "LiveSession")
        {
            $prereqsPassed =
                $State["Gates"]["ValidateReroute"]["Status"] -eq "Passed" -and
                $State["Gates"]["ValidateNoProgress"]["Status"] -eq "Passed" -and
                $State["Gates"]["ValidateCombat"]["Status"] -eq "Passed"

            if ($prereqsPassed -and [int]$gateState["PassCount"] -lt [int]$gateState["TargetPassCount"])
            {
                return $gateName
            }
        }
        elseif ($gateState["Status"] -ne "Passed")
        {
            return $gateName
        }
    }

    return $null
}

function Update-GateStateFromSummary
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][string]$GateName,
        [Parameter(Mandatory = $true)][string]$ArtifactTag,
        [AllowNull()][string]$SummaryPath,
        [AllowNull()][object]$Summary
    )

    $gateState = $State["Gates"][$GateName]
    $gateState["LastArtifactTag"] = $ArtifactTag
    $gateState["LastSummaryPath"] = $SummaryPath
    $gateState["LastUpdatedUtc"] = (Get-Date).ToUniversalTime().ToString("o")

    $passed = [bool](Get-OptionalPropertyValue -Object $Summary -Name "Passed" -Default $false)
    $liveStateContaminated = [bool](Get-OptionalPropertyValue -Object $Summary -Name "LiveStateContaminated" -Default $false)

    if ($GateName -eq "LiveSession")
    {
        if ($passed)
        {
            $gateState["PassCount"] = [int]$gateState["PassCount"] + 1
            $gateState["Status"] = $(if ([int]$gateState["PassCount"] -ge [int]$gateState["TargetPassCount"]) { "Passed" } else { "Pending" })
        }
        elseif ($liveStateContaminated)
        {
            $gateState["BlockCount"] = [int]$gateState["BlockCount"] + 1
            $gateState["Status"] = "Blocked"
        }
        else
        {
            $gateState["FailCount"] = [int]$gateState["FailCount"] + 1
            $gateState["Status"] = "Failed"
        }

        return
    }

    if ($passed)
    {
        $gateState["PassCount"] = [int]$gateState["PassCount"] + 1
        $gateState["Status"] = "Passed"
    }
    elseif ($liveStateContaminated)
    {
        $gateState["BlockCount"] = [int]$gateState["BlockCount"] + 1
        $gateState["Status"] = "Blocked"
    }
    else
    {
        $gateState["FailCount"] = [int]$gateState["FailCount"] + 1
        $gateState["Status"] = "Failed"
    }
}

function New-Finding
{
    param(
        [Parameter(Mandatory = $true)][string]$Title,
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Subsystem,
        [Parameter(Mandatory = $true)][string]$BlockerType,
        [Parameter(Mandatory = $true)][string]$Summary,
        [int]$Severity = 3,
        [int]$Reproducibility = 3,
        [int]$TestBlockingImpact = 3,
        [int]$UserImpact = 2,
        [int]$EstimatedFixCost = 2,
        [string[]]$EvidencePaths = @()
    )

    $score = ($Severity * 4) + ($Reproducibility * 3) + ($TestBlockingImpact * 4) + ($UserImpact * 2) - $EstimatedFixCost
    $priority = $(if ($score -ge 28) { "P1" } elseif ($score -ge 20) { "P2" } else { "P3" })

    return [ordered]@{
        Id = "{0}-{1}" -f (Get-SafeName -Value $Category), [guid]::NewGuid().ToString("N").Substring(0, 8)
        Priority = $priority
        Score = $score
        Category = $Category
        Subsystem = $Subsystem
        BlockerType = $BlockerType
        Title = $Title
        Summary = $Summary
        Severity = $Severity
        Reproducibility = $Reproducibility
        TestBlockingImpact = $TestBlockingImpact
        UserImpact = $UserImpact
        EstimatedFixCost = $EstimatedFixCost
        EvidencePaths = @($EvidencePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
    }
}

function Get-FindingsForCycle
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$CycleSummary,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State
    )

    $findings = @()
    $synthetic = $CycleSummary["SyntheticBaseline"]
    $liveAssessment = $CycleSummary["LiveAssessment"]
    $gateResults = $CycleSummary["GateResults"]

    if ($null -eq $synthetic -or -not [bool](Get-OptionalPropertyValue -Object $synthetic -Name "Passed" -Default $false))
    {
        $evidence = @(
            $(Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $synthetic -Name "Build" -Default $null) -Name "StdoutPath" -Default $null),
            $(Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $synthetic -Name "CoreTests" -Default $null) -Name "StdoutPath" -Default $null),
            $(Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $synthetic -Name "FrontendTests" -Default $null) -Name "StdoutPath" -Default $null)
        )
        $findings += New-Finding `
            -Title "Synthetic baseline failed" `
            -Category "infra/startup" `
            -Subsystem "synthetic" `
            -BlockerType "synthetic baseline failure" `
            -Summary "Build or automated suites failed, so live promotion is not allowed." `
            -Severity 5 `
            -Reproducibility 4 `
            -TestBlockingImpact 5 `
            -UserImpact 4 `
            -EstimatedFixCost 3 `
            -EvidencePaths $evidence
    }

    $invalidReason = [string](Get-OptionalPropertyValue -Object $liveAssessment -Name "InvalidReason" -Default "")
    if (-not [string]::IsNullOrWhiteSpace($invalidReason))
    {
        $category = switch ($invalidReason)
        {
            "route-follow not restored" { "navigation/reroute" }
            "navigation launch check blocking" { "infra/startup" }
            "profile drift" { "launch/profile" }
            default { "invalid live state" }
        }
        $subsystem = switch ($category)
        {
            "navigation/reroute" { "navigation" }
            "infra/startup" { "live-readiness" }
            "launch/profile" { "profile" }
            default { "live-state" }
        }
        $findings += New-Finding `
            -Title "Live validation is blocked by current client state" `
            -Category $category `
            -Subsystem $subsystem `
            -BlockerType $invalidReason `
            -Summary ("Live validation cannot proceed while the stack reports: {0}." -f $invalidReason) `
            -Severity 4 `
            -Reproducibility 4 `
            -TestBlockingImpact 5 `
            -UserImpact 3 `
            -EstimatedFixCost 2 `
            -EvidencePaths @($CycleSummary["LiveAssessmentPath"])
    }

    foreach ($gateName in $State["Gates"].Keys)
    {
        $gateResult = $gateResults[$gateName]
        if ($null -eq $gateResult)
        {
            continue
        }

        $summary = $gateResult["Summary"]
        if ($null -eq $summary)
        {
            $findings += New-Finding `
                -Title ("{0} did not produce a summary artifact" -f $gateName) `
                -Category "telemetry/evidence gaps" `
                -Subsystem "telemetry" `
                -BlockerType "missing summary artifact" `
                -Summary ("The {0} gate executed but did not leave a final summary JSON." -f $gateName) `
                -Severity 3 `
                -Reproducibility 4 `
                -TestBlockingImpact 4 `
                -UserImpact 2 `
                -EstimatedFixCost 2 `
                -EvidencePaths @($gateResult["ArtifactDir"], $gateResult["ProcessResult"]["StdoutPath"], $gateResult["ProcessResult"]["StderrPath"])
            continue
        }

        if ([bool](Get-OptionalPropertyValue -Object $summary -Name "Passed" -Default $false))
        {
            continue
        }

        $focusContaminated = [bool](Get-OptionalPropertyValue -Object $summary -Name "FocusContaminated" -Default $false)
        $category = switch ($gateName)
        {
            "ValidateCombat" { "combat/rotation" }
            "ValidateNoProgress" { "navigation/reroute" }
            "ValidateReroute" { "navigation/reroute" }
            "LiveSession" { "navigation/reroute" }
            default { "telemetry/evidence gaps" }
        }
        if ([bool](Get-OptionalPropertyValue -Object $summary -Name "LiveStateContaminated" -Default $false))
        {
            $category = "invalid live state"
        }
        elseif ($focusContaminated)
        {
            $category = "input/focus"
        }

        $findings += New-Finding `
            -Title ("{0} is not passing acceptance" -f $gateName) `
            -Category $category `
            -Subsystem $(if ($category -eq "combat/rotation") { "combat" } elseif ($category -eq "invalid live state") { "live-state" } elseif ($category -eq "input/focus") { "focus" } else { "navigation" }) `
            -BlockerType $gateName `
            -Summary ("{0} reported one or more acceptance failures." -f $gateName) `
            -Severity 4 `
            -Reproducibility 4 `
            -TestBlockingImpact 5 `
            -UserImpact 3 `
            -EstimatedFixCost 3 `
            -EvidencePaths @($gateResult["SummaryPath"], $gateResult["ArtifactDir"])
    }

    if ($findings.Count -eq 0)
    {
        $nextGate = Get-NextGateAction -State $State
        $findings += New-Finding `
            -Title "Current candidate is healthy enough to promote" `
            -Category "optimization" `
            -Subsystem "campaign" `
            -BlockerType $(if ([string]::IsNullOrWhiteSpace($nextGate)) { "CampaignCloseout" } else { $nextGate }) `
            -Summary ("No active blocking findings were generated. Promote to the next gate: {0}." -f $(if ([string]::IsNullOrWhiteSpace($nextGate)) { "complete campaign closeout" } else { $nextGate })) `
            -Severity 1 `
            -Reproducibility 3 `
            -TestBlockingImpact 1 `
            -UserImpact 2 `
            -EstimatedFixCost 1 `
            -EvidencePaths @($CycleSummary["CycleDir"])
    }

    return @($findings | Sort-Object @{ Expression = { [int]$_.Score } ; Descending = $true })
}

function Get-HypothesesForFindings
{
    param([object[]]$Findings)

    $hypotheses = @()
    foreach ($finding in $Findings)
    {
        if ($finding.Category -eq "optimization")
        {
            $statement = switch ($finding.BlockerType)
            {
                "ValidateReroute" { "The current candidate is healthy enough to promote into reroute validation, which should confirm navigation hazard handling on the live stack." }
                "ValidateNoProgress" { "The current candidate is ready for explicit no-progress recovery validation to confirm escalation and recovery behavior." }
                "ValidateCombat" { "The current candidate is ready for controlled combat validation to confirm rotation, pull, and reacquire behavior." }
                "LiveSession" { "The current candidate is ready for soak promotion because targeted gates are clear." }
                "CampaignCloseout" { "The current candidate has no open gate blockers and is ready for final evidence review and closeout." }
                default { "The current candidate is ready for the next validation gate." }
            }
        }
        else
        {
            $statement = switch ($finding.BlockerType)
            {
                "bootstrap failed" { "StartAndValidate exited non-zero, so the live stack is not safe to promote into targeted gates until bootstrap is recovered." }
                "navigation listener unavailable" { "The navigation listener is down after bootstrap, so targeted live navigation gates cannot run yet." }
                "navigation launch check blocking" { "Launch readiness reports Navigation as non-OK and blocking, so promotion must wait for a healthy launch-navigation state." }
                "bot inactive after bootstrap" { "The live stack came up, but the bot never became active after bootstrap. Startup recovery needs to be revalidated before gate promotion." }
                "health unavailable" { "A service or listener crash is preventing health checks from succeeding. Restarting the live stack should restore the contract." }
                "launch readiness incomplete" { "One or more launch prerequisites are unresolved. Running Doctor or collecting readiness evidence should identify the missing subsystem." }
                "profile drift" { "The loaded live profile does not match the requested profile, so bootstrap evidence is not trustworthy until the profile contract is restored." }
                "route-follow not restored" { "The live bot is not settling into FollowRoute, likely because a transient goal or recovery path is still active." }
                "ValidateReroute" { "Reroute acceptance is failing because synthetic hazards are not intersecting the route strongly enough or reroute counters are not advancing." }
                "ValidateCombat" { "Combat acceptance is failing due to pull/reacquire regressions, incomplete combat evidence, or focus/input contamination." }
                "ValidateNoProgress" { "The no-progress scenario is not generating an explicit trigger-and-recover sequence in the current route segment." }
                "LiveSession" { "Soak acceptance is exceeding health, route-deviation, or repeat-stuck thresholds over time." }
                default { "A targeted validation rerun with tighter evidence focus should isolate the smallest reproducible cause." }
            }
        }

        $hypotheses += [ordered]@{
            FindingId = $finding.Id
            Priority = $finding.Priority
            Subsystem = $finding.Subsystem
            Hypothesis = $statement
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
        }
    }

    return $hypotheses
}

function Get-ProposalsForFindings
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Findings,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State
    )

    $proposals = @()
    $gitWorkspace = Get-GitWorkspaceAssessment
    $retryBudget = [int](Get-OptionalPropertyValue -Object $State["Budget"] -Name "MaxRetriesPerIncident" -Default 2)
    foreach ($finding in $Findings)
    {
        $fingerprint = Get-IncidentFingerprint -Finding $finding
        $retryEntry = Get-OptionalPropertyValue -Object $State["RetryLedger"] -Name $fingerprint -Default $null
        $attempts = [int](Get-OptionalPropertyValue -Object $retryEntry -Name "Attempts" -Default 0)
        $failover = Get-FailoverDecision -Finding $finding
        $proposal = [ordered]@{
            FindingId = $finding.Id
            Fingerprint = $fingerprint
            Priority = $finding.Priority
            Subsystem = $finding.Subsystem
            Title = ""
            RecommendedChangeType = "Investigation"
            AutoApplyEligible = $false
            Executor = $null
            FailoverDecision = $failover
            FailoverActions = @($failover.PrimaryAction, $failover.SecondaryAction, $failover.TertiaryAction)
            RetryAttempts = $attempts
            RetryBudget = $retryBudget
            MutationAssessment = $gitWorkspace
            RemediationTask = $null
        }

        switch ($finding.BlockerType)
        {
            "health unavailable"
            {
                $proposal["Title"] = "Restart the live stack and re-check health"
                $proposal["RecommendedChangeType"] = "Operational"
                $proposal["AutoApplyEligible"] = $true
                $proposal["Executor"] = [ordered]@{ Kind = "AgentControl"; Action = "Restart" }
            }
            "launch readiness incomplete"
            {
                $proposal["Title"] = "Run doctor workflow to restore launch readiness"
                $proposal["RecommendedChangeType"] = "Operational"
                $proposal["Executor"] = [ordered]@{ Kind = "AgentControl"; Action = "Doctor" }
            }
            "profile drift"
            {
                $proposal["Title"] = "Re-bootstrap the requested profile and confirm the live profile contract"
                $proposal["RecommendedChangeType"] = "Operational"
                $proposal["Executor"] = [ordered]@{ Kind = "AgentControl"; Action = "StartAndValidate" }
            }
            default
            {
                $proposal["Title"] = "Collect targeted evidence and prepare a narrow remediation pass"
                $proposal["RecommendedChangeType"] = $(if ($finding.Category -eq "invalid live state") { "Operational" } elseif ($finding.Category -eq "combat/rotation") { "Code" } else { "Investigation" })
            }
        }

        if ($proposal["RecommendedChangeType"] -eq "Code")
        {
            $taskId = "remediate-{0}" -f [guid]::NewGuid().ToString("N").Substring(0, 8)
            $proposal["RemediationTask"] = [ordered]@{
                Id = $taskId
                IncidentId = $finding.Id
                Kind = "Code"
                Status = $(if ([bool](Get-OptionalPropertyValue -Object $gitWorkspace -Name "IsDirty" -Default $false)) { "BlockedProtectedWorktree" } else { "Pending" })
                Summary = $proposal["Title"]
                BranchName = ("autonomy/{0}" -f $taskId)
                WorktreePath = (Join-Path $script:SupervisorRoot ("worktrees\{0}" -f $taskId))
                ProtectedWorktreeRequired = $true
                CreatedUtc = (Get-Date).ToUniversalTime().ToString("o")
                UpdatedUtc = $null
            }
        }

        if ($proposal["RecommendedChangeType"] -eq "Operational" -and $attempts -lt $retryBudget)
        {
            $proposal["AutoApplyEligible"] = $true
        }
        elseif ($proposal["RecommendedChangeType"] -eq "Operational")
        {
            $proposal["AutoApplyEligible"] = $false
            $proposal["Title"] = "{0} (retry budget exhausted)" -f $proposal["Title"]
        }

        $proposals += $proposal
    }

    return $proposals
}

function Invoke-OperationalProposal
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$Proposal,
        [Parameter(Mandatory = $true)][string]$CycleDir
    )

    $lock = Acquire-JsonLock -Path $script:MutationLockPath -Name "SupervisorMutation"
    try
    {
        $executor = $Proposal["Executor"]
        if ($null -eq $executor -or (Get-OptionalPropertyValue -Object $executor -Name "Kind" -Default "") -ne "AgentControl")
        {
            return [ordered]@{
                Applied = $false
                Rejected = $true
                Reason = "No supported executor was configured."
                Restoration = "No repo mutation support in v1."
            }
        }

        $actions = @($Proposal["FailoverActions"] | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique)
        if ($actions.Count -eq 0)
        {
            $actions = @([string](Get-OptionalPropertyValue -Object $executor -Name "Action" -Default ""))
        }

        $retryEntry = Get-OptionalPropertyValue -Object $State["RetryLedger"] -Name $Proposal["Fingerprint"] -Default $null
        if ($null -eq $retryEntry)
        {
            $retryEntry = [ordered]@{
                Fingerprint = $Proposal["Fingerprint"]
                Attempts = 0
                SameReasonFailures = 0
                LastAction = ""
                LastOutcome = ""
                LastAttemptUtc = $null
            }
        }

        $attemptResults = @()
        foreach ($actionName in $actions)
        {
            if ([string]::IsNullOrWhiteSpace($actionName))
            {
                continue
            }

            $artifactTag = "{0}-{1}-proposal-{2}" -f (Get-SafeName -Value $script:SupervisorId), (Get-Date -Format "yyyyMMdd-HHmmss"), (Get-SafeName -Value $actionName)
            $result = Invoke-AgentControlAction -CycleDir $CycleDir -ActionName $actionName -ArtifactTag $artifactTag
            $attemptResults += [ordered]@{
                ActionName = $actionName
                ArtifactTag = $artifactTag
                Result = $result
            }

            $retryEntry["Attempts"] = [int](Get-OptionalPropertyValue -Object $retryEntry -Name "Attempts" -Default 0) + 1
            $retryEntry["LastAction"] = $actionName
            $retryEntry["LastOutcome"] = $(if ([bool]$result["ProcessResult"]["Success"]) { "Success" } else { "Failure" })
            $retryEntry["LastAttemptUtc"] = (Get-Date).ToUniversalTime().ToString("o")
            $State["RetryLedger"][$Proposal["Fingerprint"]] = $retryEntry

            if ([bool]$result["ProcessResult"]["Success"])
            {
                return [ordered]@{
                    Applied = $true
                    Rejected = $false
                    Reason = "Operational failover action executed successfully."
                    ActionName = $actionName
                    ArtifactTag = $artifactTag
                    AttemptResults = @($attemptResults)
                    Result = $result
                    Restoration = "Repo mutation still requires a dedicated autonomous worktree."
                }
            }
        }

        return [ordered]@{
            Applied = $false
            Rejected = $true
            Reason = "All operational failover actions failed within the retry budget."
            ActionName = ""
            ArtifactTag = $null
            AttemptResults = @($attemptResults)
            Result = $null
            Restoration = "Repo mutation still requires a dedicated autonomous worktree."
        }
    }
    finally
    {
        Release-JsonLock -Path $script:MutationLockPath
    }
}

function Get-NextStepsQueue
{
    param(
        [Parameter(Mandatory = $true)][object[]]$Findings,
        [Parameter(Mandatory = $true)][object[]]$Hypotheses,
        [Parameter(Mandatory = $true)][object[]]$Proposals
    )

    $queue = @()
    foreach ($finding in ($Findings | Sort-Object @{ Expression = { [int]$_.Score } ; Descending = $true }))
    {
        $hypothesis = @($Hypotheses | Where-Object { $_.FindingId -eq $finding.Id })[0]
        $proposal = @($Proposals | Where-Object { $_.FindingId -eq $finding.Id })[0]

        $testAction = switch ($finding.BlockerType)
        {
            "bootstrap failed" { "InspectBootstrapThenStartAndValidate" }
            "navigation listener unavailable" { "DoctorThenStartAndValidate" }
            "navigation launch check blocking" { "InspectLaunchNavigationCheckThenStartAndValidate" }
            "bot inactive after bootstrap" { "StartAndValidateThenObserveBotActivation" }
            "health unavailable" { "RestartThenHealthProbe" }
            "launch readiness incomplete" { "DoctorThenStartAndValidate" }
            "profile drift" { "StartAndValidateThenVerifyProfile" }
            "ValidateReroute" { "ValidateReroute" }
            "ValidateCombat" { "ValidateCombat" }
            "ValidateNoProgress" { "ValidateNoProgress" }
            "LiveSession" { "LiveSession" }
            "route-follow not restored" { "StartAndValidateThenObserveFollowRoute" }
            default { "CollectEvidence" }
        }

        $expectedSignal = switch ($finding.BlockerType)
        {
            "bootstrap failed" { "StartAndValidate exits 0 and leaves the live stack active with a usable bot session." }
            "navigation listener unavailable" { "Port 47110 is listening again before any targeted live gate begins." }
            "navigation launch check blocking" { "Navigation launch check returns Ok (or non-blocking) and targeted gate promotion resumes." }
            "bot inactive after bootstrap" { "Bot status returns IsActive=true and AgentAvailable=true after bootstrap." }
            "health unavailable" { "Health returns success from both /api/health and /api/health/startup." }
            "launch readiness incomplete" { "Launch readiness returns green and StartAndValidate exits 0." }
            "profile drift" { "The loaded profile matches the supervisor-requested profile before any live gate begins." }
            "ValidateReroute" { "Trigger/apply/drop close with zero detour-only collapse." }
            "ValidateCombat" { "Kills >= 30 with complete spell coverage and clean combat runtime counters." }
            "ValidateNoProgress" { "Explicit ShortNoProgress or TimeoutNoProgress trigger followed by recovery." }
            "LiveSession" { "Three soak windows stay within repeat-stuck, deviation, and listener thresholds." }
            "route-follow not restored" { "Current goal settles into FollowRoute without Adhoc or corpse recovery contamination." }
            default { "Targeted validation produces a stable pass/fail signal and clearer evidence." }
        }

        $queue += [ordered]@{
            Priority = $finding.Priority
            Subsystem = $finding.Subsystem
            TestAction = $testAction
            Hypothesis = $(if ($null -ne $hypothesis) { $hypothesis.Hypothesis } else { $finding.Summary })
            ExpectedSignal = $expectedSignal
            BlockerType = $finding.BlockerType
            RecommendedChangeType = $(if ($null -ne $proposal) { $proposal.RecommendedChangeType } else { "Investigation" })
            EvidencePaths = @($finding.EvidencePaths)
        }
    }

    return $queue
}

function Convert-NextStepsToMarkdown
{
    param([object[]]$NextSteps)

    $lines = @("# Next Best Steps", "")
    if ($null -eq $NextSteps -or $NextSteps.Count -eq 0)
    {
        $lines += "No open next-step items were generated."
        return ($lines -join [Environment]::NewLine)
    }

    $index = 0
    foreach ($step in $NextSteps)
    {
        $index++
        $lines += ('{0}. [{1}] {2} -> `{3}`' -f $index, $step.Priority, $step.Subsystem, $step.TestAction)
        $lines += ("   Hypothesis: {0}" -f $step.Hypothesis)
        $lines += ("   Expected signal: {0}" -f $step.ExpectedSignal)
        $lines += ("   Change type: {0}" -f $step.RecommendedChangeType)
        if (@($step.EvidencePaths).Count -gt 0)
        {
            $lines += ("   Evidence: {0}" -f ((@($step.EvidencePaths)) -join ", "))
        }
        $lines += ""
    }

    return ($lines -join [Environment]::NewLine)
}

function Get-StackStateSummary
{
    param([AllowNull()][object]$LiveAssessment)

    $environment = Get-EnvironmentObservation
    $summary = [ordered]@{
        WowRunning = [bool](Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object $environment -Name "Wow" -Default $null) -Name "Running" -Default $false)
        WebPortListening = [bool](Get-NestedPropertyValue -Object $environment -Path @("Ports", "Web5000") -Default $false)
        NavigationPortListening = [bool](Get-NestedPropertyValue -Object $environment -Path @("Ports", "Navigation47110") -Default $false)
        NavigationCheckStatus = $null
        NavigationCheckMessage = $null
        NavigationSource = $null
        NavigationBlockingReason = $null
        RuntimeMode = $null
        BotActive = $null
        CurrentGoal = $null
        InvalidReason = $null
    }

    if ($null -ne $LiveAssessment)
    {
        $summary["RuntimeMode"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "RuntimeMode" -Default $null
        $summary["BotActive"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "BotActive" -Default $null
        $summary["CurrentGoal"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "CurrentGoal" -Default $null
        $summary["NavigationCheckStatus"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "NavigationCheckStatus" -Default $null
        $summary["NavigationCheckMessage"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "NavigationCheckMessage" -Default $null
        $summary["NavigationSource"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "NavigationSource" -Default $null
        $summary["NavigationBlockingReason"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "NavigationBlockingReason" -Default $null
        $summary["InvalidReason"] = Get-OptionalPropertyValue -Object $LiveAssessment -Name "InvalidReason" -Default $null
    }

    return $summary
}

function Write-LatestArtifacts
{
    param(
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$State,
        [Parameter(Mandatory = $true)][System.Collections.IDictionary]$CycleSummary,
        [Parameter(Mandatory = $true)][object[]]$Findings,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][object[]]$Incidents,
        [Parameter(Mandatory = $true)][object[]]$NextSteps
    )

    $status = [ordered]@{
        SupervisorId = $script:SupervisorId
        CurrentPhase = $CycleSummary["CurrentPhase"]
        LastCycleId = $CycleSummary["CycleId"]
        Budget = $State["Budget"]
        PromotionState = $State["PromotionState"]
        KillSwitchState = $State["KillSwitchState"]
        StackState = Get-StackStateSummary -LiveAssessment $CycleSummary["LiveAssessment"]
        TopFindings = @($Findings | Select-Object -First 3)
        TopIncidents = @($Incidents | Select-Object -First 5)
        RecentRuns = @($State["RecentRuns"])
        LatestEvidencePaths = @(
            $CycleSummary["CycleDir"],
            $CycleSummary["ValidationResultsPath"],
            $CycleSummary["FindingsPath"],
            $CycleSummary["NextStepsPath"]
        ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        NextStepsPath = $script:NextStepsLatestPath
        NextStepsMarkdownPath = $script:NextStepsLatestMarkdownPath
        UpdatedUtc = (Get-Date).ToUniversalTime().ToString("o")
    }

    [void](Write-JsonFile -Path $script:StatusLatestPath -Object $status)
    [void](Write-JsonFile -Path $script:NextStepsLatestPath -Object $NextSteps)
    [void](Write-TextFile -Path $script:NextStepsLatestMarkdownPath -Value (Convert-NextStepsToMarkdown -NextSteps $NextSteps))
    [void](Write-JsonFile -Path $script:OpenIssuesPath -Object $Findings)
    [void](Write-JsonFile -Path $script:IncidentsLatestPath -Object $Incidents)
    [void](Write-JsonFile -Path $script:RunsLatestPath -Object $State["RecentRuns"])
    foreach ($incident in @($Incidents))
    {
        Add-NdJsonRecord -Path $script:IncidentHistoryPath -Record $incident
    }
    Add-NdJsonRecord -Path $script:MetricsHistoryPath -Record ([ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            CycleId = $CycleSummary["CycleId"]
            SyntheticPassed = [bool](Get-OptionalPropertyValue -Object $CycleSummary["SyntheticBaseline"] -Name "Passed" -Default $false)
            LiveValid = [bool](Get-OptionalPropertyValue -Object $CycleSummary["LiveAssessment"] -Name "Valid" -Default $false)
            Findings = $Findings.Count
            Gates = $CycleSummary["GateResults"]
        })
}

function Invoke-OneCycle
{
    param([Parameter(Mandatory = $true)][System.Collections.IDictionary]$State)

    Sync-KillSwitchState -State $State

    $cycleId = Get-CycleId
    $cycleDir = Get-CycleDir -CycleId $cycleId
    $gitWorkspace = Get-GitWorkspaceAssessment
    $startedUtc = (Get-Date).ToUniversalTime().ToString("o")
    $manifest = [ordered]@{
        SupervisorId = $script:SupervisorId
        CycleId = $cycleId
        StartedUtc = $startedUtc
        Action = $Action
        PrimarySurface = $PrimarySurface
        Profile = $Profile
        NavProfile = $NavProfile
        DryRun = [bool]$DryRun
        EnableMutations = [bool]$EnableMutations
        PriorityGateOrder = Get-GateOrder
        GitWorkspace = $gitWorkspace
    }
    [void](Write-JsonFile -Path (Join-Path $cycleDir "cycle-manifest.json") -Object $manifest)

    $State["CurrentPhase"] = "Discover"
    $State["LastCycleId"] = $cycleId
    Save-SupervisorState -State $State
    Add-CycleObservation -CycleDir $cycleDir -Phase "Discover" -Subsystem "environment" -ArtifactSource "environment" -Context (Get-EnvironmentObservation)

    $synthetic = Invoke-SyntheticBaseline -CycleDir $cycleDir
    $State["SyntheticBaseline"]["Status"] = $(if ([bool]$synthetic["Passed"]) { "Passed" } else { "Failed" })
    $State["SyntheticBaseline"]["Passed"] = [bool]$synthetic["Passed"]
    $State["SyntheticBaseline"]["LastRunUtc"] = (Get-Date).ToUniversalTime().ToString("o")
    $State["SyntheticBaseline"]["ArtifactPath"] = $cycleDir
    Save-SupervisorState -State $State

    $bootstrapResult = $null
    $liveAssessment = $null
    $gateResults = [ordered]@{}
    $appliedChanges = @()

    $liveSurfaceAllowed = Test-LiveSurfaceAllowed -State $State
    if (-not $DryRun -and $PrimarySurface -ne "SyntheticOnly" -and $liveSurfaceAllowed -and [bool]$synthetic["Passed"])
    {
        $State["CurrentPhase"] = "Verify"
        Save-SupervisorState -State $State

        $bootstrapTag = "{0}-{1}-bootstrap" -f (Get-SafeName -Value $script:SupervisorId), $cycleId
        $bootstrapResult = Invoke-AgentControlAction -CycleDir $cycleDir -ActionName "StartAndValidate" -ArtifactTag $bootstrapTag
        $liveAssessment = Get-LiveStateAssessment
        $liveAssessment["BootstrapSucceeded"] = [bool]$bootstrapResult["ProcessResult"]["Success"]
        $liveAssessment["BootstrapArtifactDir"] = $bootstrapResult["ArtifactDir"]
        $liveAssessment["BootstrapStdoutPath"] = $bootstrapResult["ProcessResult"]["StdoutPath"]
        $liveAssessment["BootstrapStderrPath"] = $bootstrapResult["ProcessResult"]["StderrPath"]
        if (-not [bool]$liveAssessment["BootstrapSucceeded"])
        {
            $liveAssessment["Valid"] = $false
            $liveAssessment["InvalidReason"] = "bootstrap failed"
        }
        elseif (-not [string]::IsNullOrWhiteSpace([string](Get-OptionalPropertyValue -Object $liveAssessment -Name "NavigationBlockingReason" -Default "")))
        {
            $liveAssessment["Valid"] = $false
            $liveAssessment["InvalidReason"] = "navigation launch check blocking"
        }
        elseif (-not [bool](Get-OptionalPropertyValue -Object $liveAssessment -Name "BotActive" -Default $false))
        {
            $liveAssessment["Valid"] = $false
            $liveAssessment["InvalidReason"] = "bot inactive after bootstrap"
        }

        [void](Write-JsonFile -Path (Join-Path $cycleDir "live-assessment.json") -Object $liveAssessment)

        if ([bool](Get-OptionalPropertyValue -Object $liveAssessment -Name "Valid" -Default $false))
        {
            $State["CurrentPhase"] = "Verify"
            Save-SupervisorState -State $State

            for ($gateIndex = 0; $gateIndex -lt $MaxGateActionsPerCycle; $gateIndex++)
            {
                $gateName = Get-NextGateAction -State $State
                if ([string]::IsNullOrWhiteSpace($gateName))
                {
                    break
                }

                $artifactTag = "{0}-{1}-{2}" -f (Get-SafeName -Value $script:SupervisorId), $cycleId, (Get-SafeName -Value $gateName)
                $gateResult = Invoke-AgentControlAction -CycleDir $cycleDir -ActionName $gateName -ArtifactTag $artifactTag
                if ($null -eq $gateResult["Summary"])
                {
                    $gateResult["Summary"] = [ordered]@{
                        Passed = $false
                        LiveStateContaminated = $false
                        FailureReasons = @("No summary artifact was generated.")
                    }
                }

                $gateResults[$gateName] = $gateResult
                Update-GateStateFromSummary -State $State -GateName $gateName -ArtifactTag $artifactTag -SummaryPath $gateResult["SummaryPath"] -Summary $gateResult["Summary"]
                Save-SupervisorState -State $State

                if (-not [bool](Get-OptionalPropertyValue -Object $gateResult["Summary"] -Name "Passed" -Default $false))
                {
                    break
                }
            }
        }
    }
    elseif (-not $DryRun -and [bool]$synthetic["Passed"])
    {
        $liveAssessment = [ordered]@{
            TimestampUtc = (Get-Date).ToUniversalTime().ToString("o")
            Valid = $false
            InvalidReason = $(if ([bool](Get-OptionalPropertyValue -Object $State["KillSwitchState"] -Name "Enabled" -Default $false))
                {
                    "kill switch enabled"
                }
                else
                {
                    "guarded live demotion active"
                })
            LaunchReady = $false
            BotActive = $false
            AgentAvailable = $false
            CurrentGoal = $null
            RuntimeMode = $null
            RequestedProfile = $Profile
        }

        [void](Write-JsonFile -Path (Join-Path $cycleDir "live-assessment.json") -Object $liveAssessment)
    }

    $cycleSummary = [ordered]@{
        SupervisorId = $script:SupervisorId
        CycleId = $cycleId
        CycleDir = $cycleDir
        StartedUtc = $startedUtc
        CurrentPhase = "Classify"
        SyntheticBaseline = $synthetic
        Bootstrap = $bootstrapResult
        LiveAssessment = $liveAssessment
        LiveAssessmentPath = $(if ($null -ne $liveAssessment) { Join-Path $cycleDir "live-assessment.json" } else { $null })
        GateResults = $gateResults
        AppliedChanges = @()
        GitWorkspace = $gitWorkspace
        NavigationCheckStatus = $(if ($null -ne $liveAssessment) { Get-OptionalPropertyValue -Object $liveAssessment -Name "NavigationCheckStatus" -Default $null } else { $null })
        NavigationCheckMessage = $(if ($null -ne $liveAssessment) { Get-OptionalPropertyValue -Object $liveAssessment -Name "NavigationCheckMessage" -Default $null } else { $null })
        NavigationSource = $(if ($null -ne $liveAssessment) { Get-OptionalPropertyValue -Object $liveAssessment -Name "NavigationSource" -Default $null } else { $null })
        NavigationBlockingReason = $(if ($null -ne $liveAssessment) { Get-OptionalPropertyValue -Object $liveAssessment -Name "NavigationBlockingReason" -Default $null } else { $null })
        ValidationResultsPath = Join-Path $cycleDir "validation-results.json"
    }

    $findings = Get-FindingsForCycle -CycleSummary $cycleSummary -State $State
    $incidents = @(Get-IncidentsForCycle -State $State -Findings $findings -CycleId $cycleId -CycleDir $cycleDir)
    $State["IncidentQueue"] = @($incidents | Select-Object -First 25)
    Update-RetryLedgerForIncidents -State $State -Incidents $incidents
    Update-PromotionStateFromIncidents -State $State -Incidents $incidents
    Save-SupervisorState -State $State
    $hypotheses = Get-HypothesesForFindings -Findings $findings
    $proposals = Get-ProposalsForFindings -Findings $findings -State $State

    if ($EnableMutations -and -not $DryRun)
    {
        foreach ($proposal in $proposals)
        {
            if (-not [bool](Get-OptionalPropertyValue -Object $proposal -Name "AutoApplyEligible" -Default $false))
            {
                continue
            }

            $State["CurrentPhase"] = "Remediate"
            Save-SupervisorState -State $State
            $cycleSummary["CurrentPhase"] = "Remediate"
            $appliedChanges += Invoke-OperationalProposal -State $State -Proposal $proposal -CycleDir $cycleDir
            break
        }
    }

    $cycleSummary["AppliedChanges"] = $appliedChanges

    $findingsPath = Join-Path $cycleDir "findings.json"
    $incidentsPath = Join-Path $cycleDir "incidents.json"
    $hypothesesPath = Join-Path $cycleDir "hypotheses.json"
    $proposalsPath = Join-Path $cycleDir "proposed-improvements.json"
    $appliedChangesPath = Join-Path $cycleDir "applied-changes.json"
    $validationResultsPath = Join-Path $cycleDir "validation-results.json"
    $nextSteps = Get-NextStepsQueue -Findings $findings -Hypotheses $hypotheses -Proposals $proposals
    $nextStepsPath = Join-Path $cycleDir "next-steps.json"

    [void](Write-JsonFile -Path $findingsPath -Object $findings)
    [void](Write-JsonFile -Path $incidentsPath -Object $incidents)
    [void](Write-JsonFile -Path $hypothesesPath -Object $hypotheses)
    [void](Write-JsonFile -Path $proposalsPath -Object $proposals)
    [void](Write-JsonFile -Path $appliedChangesPath -Object $appliedChanges)
    [void](Write-JsonFile -Path $validationResultsPath -Object ([ordered]@{
            SyntheticBaseline = $synthetic
            Bootstrap = $bootstrapResult
            LiveAssessment = $liveAssessment
            GateResults = $gateResults
            Incidents = $incidents
        }))
    [void](Write-JsonFile -Path $nextStepsPath -Object $nextSteps)

    $cycleSummary["FindingsPath"] = $findingsPath
    $cycleSummary["IncidentsPath"] = $incidentsPath
    $cycleSummary["HypothesesPath"] = $hypothesesPath
    $cycleSummary["ProposalsPath"] = $proposalsPath
    $cycleSummary["AppliedChangesPath"] = $appliedChangesPath
    $cycleSummary["ValidationResultsPath"] = $validationResultsPath
    $cycleSummary["NextStepsPath"] = $nextStepsPath
    $cycleSummary["CurrentPhase"] = "Learn"
    $cycleSummary["CompletedUtc"] = (Get-Date).ToUniversalTime().ToString("o")

    Update-RecentRuns -State $State -CycleSummary $cycleSummary -Incidents $incidents
    $State["CurrentPhase"] = "Learn"
    Save-SupervisorState -State $State

    [void](Write-JsonFile -Path (Join-Path $cycleDir "cycle-summary.json") -Object $cycleSummary)
    Write-LatestArtifacts -State $State -CycleSummary $cycleSummary -Findings $findings -Incidents $incidents -NextSteps $nextSteps

    $State["CurrentPhase"] = "Idle"
    $State["CycleCount"] = [int]$State["CycleCount"] + 1
    Save-SupervisorState -State $State

    return $cycleSummary
}

function Show-Status
{
    $latest = Read-JsonFile -Path $script:StatusLatestPath
    $nextSteps = Read-JsonFile -Path $script:NextStepsLatestPath

    if ($null -eq $latest)
    {
        $fallbackLive = $null
        if (Test-TcpPort -Port 5000)
        {
            $fallbackLive = Get-LiveStateAssessment
        }

        $stackState = Get-StackStateSummary -LiveAssessment $fallbackLive
        Write-Host ("Supervisor: {0}" -f $script:SupervisorId)
        Write-Host ("Current phase: {0}" -f (Get-OptionalPropertyValue -Object (Get-SupervisorState) -Name "CurrentPhase" -Default "Idle"))
        Write-Host ("Stack state: WoW={0}, Web5000={1}, Nav47110={2}, Goal={3}" -f $stackState.WowRunning, $stackState.WebPortListening, $stackState.NavigationPortListening, $stackState.CurrentGoal)
        Write-Host ("Promotion: {0}" -f (Get-OptionalPropertyValue -Object (Get-OptionalPropertyValue -Object (Get-SupervisorState) -Name "PromotionState" -Default $null) -Name "EffectiveSurface" -Default $PrimarySurface))
        Write-Host "Top findings:"
        Write-Host "  - No supervisor cycle has completed yet."
        Write-Host "Latest evidence paths:"
        Write-Host ("  - {0}" -f $script:SupervisorRoot)
        Write-Host "Next best steps:"
        Write-Host "  1. Run the supervisor loop in dry-run mode to seed the living record."
        return
    }

    Write-Host ("Supervisor: {0}" -f $latest.SupervisorId)
    Write-Host ("Current phase: {0}" -f $latest.CurrentPhase)
    Write-Host ("Stack state: WoW={0}, Web5000={1}, Nav47110={2}, Runtime={3}, Active={4}, Goal={5}, InvalidReason={6}" -f `
            $latest.StackState.WowRunning, `
            $latest.StackState.WebPortListening, `
            $latest.StackState.NavigationPortListening, `
            $latest.StackState.RuntimeMode, `
            $latest.StackState.BotActive, `
            $latest.StackState.CurrentGoal, `
            $latest.StackState.InvalidReason)
    Write-Host ("Promotion: {0}, KillSwitch={1}" -f `
            (Get-OptionalPropertyValue -Object $latest.PromotionState -Name "EffectiveSurface" -Default $PrimarySurface), `
            (Get-OptionalPropertyValue -Object $latest.KillSwitchState -Name "Enabled" -Default $false))
    Write-Host ("Last cycle: {0}" -f $latest.LastCycleId)
    Write-Host "Top findings:"
    foreach ($finding in @($latest.TopFindings))
    {
        Write-Host ("  - [{0}] {1}: {2}" -f $finding.Priority, $finding.Subsystem, $finding.Summary)
    }
    if (@($latest.TopIncidents).Count -gt 0)
    {
        Write-Host "Top incidents:"
        foreach ($incident in @($latest.TopIncidents | Select-Object -First 3))
        {
            Write-Host ("  - [{0}] {1}: {2} (x{3})" -f $incident.Severity, $incident.Subsystem, $incident.Reason, $incident.OccurrenceCount)
        }
    }
    Write-Host "Latest evidence paths:"
    foreach ($path in @($latest.LatestEvidencePaths))
    {
        Write-Host ("  - {0}" -f $path)
    }
    Write-Host "Next best steps:"
    foreach ($step in @($nextSteps | Select-Object -First 3))
    {
        Write-Host ("  - [{0}] {1} -> {2}" -f $step.Priority, $step.Subsystem, $step.TestAction)
    }
}

function Show-NextSteps
{
    if (Test-Path -LiteralPath $script:NextStepsLatestMarkdownPath)
    {
        Write-Host (Get-Content -LiteralPath $script:NextStepsLatestMarkdownPath -Raw -Encoding UTF8)
        return
    }

    Write-Host "No next-steps artifact exists yet."
}

function Invoke-SupervisorLoop
{
    $null = Acquire-JsonLock -Path $script:RunLockPath -Name "AutonomousBotSupervisor"
    try
    {
        Clear-ControlFlag -Path $script:StopFlagPath
        $state = Get-SupervisorState
        $executedCycles = 0

        while (-not (Test-StopRequested))
        {
            if ($MaxCycles -gt 0 -and $executedCycles -ge $MaxCycles)
            {
                break
            }

            if (Test-PauseRequested)
            {
                Write-WarnLine "Supervisor paused."
                Wait-ForResume
            }

            $cycleSummary = Invoke-OneCycle -State $state
            $executedCycles++
            Write-Ok ("Completed supervisor cycle {0}" -f $cycleSummary["CycleId"])

            if ($MaxCycles -gt 0 -and $executedCycles -ge $MaxCycles)
            {
                break
            }

            $sleepUntil = (Get-Date).AddMinutes($SyntheticIntervalMinutes)
            while ((Get-Date) -lt $sleepUntil)
            {
                if (Test-StopRequested)
                {
                    break
                }

                if (Test-PauseRequested)
                {
                    Wait-ForResume
                }

                Start-Sleep -Seconds 5
            }
        }
    }
    finally
    {
        if ($StopServicesOnExit -and (Test-Path -LiteralPath $script:AgentControlPath))
        {
            try
            {
                $cleanupDir = Join-Path $script:SupervisorRoot ("cleanup-{0}" -f (Get-Date -Format "yyyyMMdd-HHmmss"))
                [void](Ensure-Directory -Path $cleanupDir)
                [void](Invoke-ManagedPowerShellScript -ScriptPath $script:AgentControlPath `
                        -ScriptArguments @("-Action", "Stop", "-Profile", $Profile, "-NavProfile", $NavProfile, "-ArtifactTag", ("{0}-cleanup" -f (Get-SafeName -Value $script:SupervisorId))) `
                        -CycleDir $cleanupDir `
                        -Label "agentctl-stop" `
                        -TimeoutSeconds 600)
            }
            catch
            {
                Write-WarnLine ("Cleanup stop failed: {0}" -f $_.Exception.Message)
            }
        }

        Release-JsonLock -Path $script:RunLockPath
    }
}

switch ($Action)
{
    "RunLoop"
    {
        Invoke-SupervisorLoop
        break
    }
    "Status"
    {
        Show-Status
        break
    }
    "NextSteps"
    {
        Show-NextSteps
        break
    }
    "Pause"
    {
        Set-ControlFlag -Path $script:PauseFlagPath
        Write-Ok "Supervisor pause requested."
        break
    }
    "Resume"
    {
        Clear-ControlFlag -Path $script:PauseFlagPath
        Write-Ok "Supervisor resume requested."
        break
    }
    "Stop"
    {
        Set-ControlFlag -Path $script:StopFlagPath
        Write-Ok "Supervisor stop requested."
        break
    }
}
