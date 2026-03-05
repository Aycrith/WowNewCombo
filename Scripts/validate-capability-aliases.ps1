param(
    [string]$AuditDir = "docs/ai-capability-audit"
)

$ErrorActionPreference = "Stop"

$registryPath = Join-Path $AuditDir "capability_alias_registry.json"
$schemaPath = Join-Path $AuditDir "capability_alias_schema.json"
$matrixPath = Join-Path $AuditDir "AI_CAPABILITY_MATRIX.csv"
$guidePath = Join-Path $AuditDir "AI_CAPABILITY_INTERNALIZATION_GUIDE.md"

$errors = [System.Collections.Generic.List[string]]::new()

foreach ($path in @($registryPath, $schemaPath, $matrixPath, $guidePath)) {
    if (!(Test-Path $path)) {
        $errors.Add("Missing required file: $path") | Out-Null
    }
}

if ($errors.Count -eq 0) {
    $registry = Get-Content -Raw $registryPath | ConvertFrom-Json
    $null = Get-Content -Raw $schemaPath | ConvertFrom-Json
    $matrix = Import-Csv $matrixPath
    $guide = Get-Content -Raw $guidePath

    foreach ($field in @("version", "captured_on", "platforms", "domains", "aliases")) {
        if (-not $registry.PSObject.Properties.Name.Contains($field)) {
            $errors.Add("Registry missing required field '$field'.") | Out-Null
        }
    }

    if ($registry.aliases.Count -lt 1) {
        $errors.Add("Registry aliases array is empty.") | Out-Null
    }

    $allowedStatuses = @("native", "composed", "partial", "external-only")
    $aliasNames = @($registry.aliases | ForEach-Object { $_.alias })
    $duplicateAliases = $aliasNames | Group-Object | Where-Object { $_.Count -gt 1 }
    foreach ($dup in $duplicateAliases) {
        $errors.Add("Duplicate alias detected: $($dup.Name)") | Out-Null
    }

    foreach ($alias in $registry.aliases) {
        foreach ($field in @("alias", "category", "intent", "source_platforms", "execution_pattern", "required_tools", "status", "limitations", "example_prompts")) {
            if (-not $alias.PSObject.Properties.Name.Contains($field)) {
                $errors.Add("Alias '$($alias.alias)' missing field '$field'.") | Out-Null
            }
        }

        if ($alias.alias -notmatch "^[a-z0-9]+(?:-[a-z0-9]+)*$") {
            $errors.Add("Alias '$($alias.alias)' is not kebab-case.") | Out-Null
        }

        if ($allowedStatuses -notcontains $alias.status) {
            $errors.Add("Alias '$($alias.alias)' has invalid status '$($alias.status)'.") | Out-Null
        }

        if ($alias.example_prompts.Count -lt 1) {
            $errors.Add("Alias '$($alias.alias)' has no example prompts.") | Out-Null
        }

        if ($guide -notmatch [regex]::Escape($alias.alias)) {
            $errors.Add("Alias '$($alias.alias)' is missing from the guide.") | Out-Null
        }
    }

    foreach ($platform in $registry.platforms) {
        $rows = @($matrix | Where-Object { $_.Platform -eq $platform })
        if ($rows.Count -ne $registry.domains.Count) {
            $errors.Add("Coverage failure for '$platform': expected $($registry.domains.Count) rows, found $($rows.Count).") | Out-Null
            continue
        }

        foreach ($domain in $registry.domains) {
            $row = $rows | Where-Object { $_.Domain -eq $domain } | Select-Object -First 1
            if ($null -eq $row) {
                $errors.Add("Coverage failure for '$platform': missing domain '$domain'.") | Out-Null
                continue
            }
            if ($allowedStatuses -notcontains $row.IntegrationStatus) {
                $errors.Add("Coverage failure for '$platform' domain '$domain': invalid status '$($row.IntegrationStatus)'.") | Out-Null
            }
        }
    }

    $availableTools = @(
        "functions.shell_command",
        "functions.apply_patch",
        "functions.view_image",
        "web.search_query",
        "web.open",
        "web.find",
        "gh-fix-ci",
        "gh-address-comments",
        "darkages-prp-creator",
        "gemdirect-orchestration",
        "gemdirect-prp-taskboard-seeder",
        "notion-knowledge-capture",
        "notion-meeting-intelligence",
        "notion-research-documentation",
        "notion-spec-to-implementation",
        "prp-implementation-flow",
        "swarm-autonomous-dev",
        "swarm-execution-flow"
    )

    foreach ($alias in $registry.aliases | Where-Object { $_.status -in @("native", "composed") }) {
        foreach ($tool in $alias.required_tools) {
            if ($availableTools -notcontains $tool) {
                $errors.Add("Constraint failure: alias '$($alias.alias)' requires unavailable tool/skill '$tool'.") | Out-Null
            }
        }
    }

    function Resolve-Alias {
        param([string]$Prompt)

        if ($Prompt -match "cap:([a-z0-9-]+)") { return $matches[1] }
        if ($Prompt -match "use capability ([a-z0-9-]+)") { return $matches[1] }
        if ($Prompt -match "run ([a-z0-9-]+) workflow") { return $matches[1] }
        return $null
    }

    $routingPrompts = @(
        "cap:agent-orchestration-sprint",
        "use capability repo-context-scout",
        "run cross-platform-capability-audit workflow",
        "cap:code-refactor-safety-pass",
        "cap:test-regression-hardening",
        "cap:shell-investigation-loop",
        "cap:browser-ui-repro",
        "cap:mcp-integration-setup",
        "cap:api-integration-hardening",
        "cap:debug-root-cause-sprint",
        "cap:docs-knowledge-pack",
        "cap:ci-failure-triage",
        "cap:dependency-risk-audit",
        "cap:migration-planning-pack",
        "cap:pr-review-sweep",
        "cap:issue-reproducer-loop",
        "cap:memory-capture-brief",
        "cap:safety-gated-execution",
        "cap:performance-optimization-pass",
        "cap:platform-parity-compare"
    )

    foreach ($prompt in $routingPrompts) {
        $resolved = Resolve-Alias -Prompt $prompt
        if ([string]::IsNullOrWhiteSpace($resolved)) {
            $errors.Add("Routing failure: unresolved prompt '$prompt'.") | Out-Null
            continue
        }
        if ($aliasNames -notcontains $resolved) {
            $errors.Add("Routing failure: alias '$resolved' not present in registry.") | Out-Null
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "Validation failed:" -ForegroundColor Red
    foreach ($e in $errors) {
        Write-Host " - $e" -ForegroundColor Red
    }
    exit 1
}

Write-Host "Validation passed." -ForegroundColor Green
