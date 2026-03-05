param([string]$AuditDir='docs/ai-capability-audit',[string]$CapturedOn='2026-03-05')
$ErrorActionPreference='Stop'
if(!(Test-Path $AuditDir)){New-Item -Path $AuditDir -ItemType Directory|Out-Null}

function C([int]$v){if($v -lt 0){0}elseif($v -gt 5){5}else{$v}}

$p=@(
'Claude Code|5|https://docs.anthropic.com/en/docs/claude-code/overview;https://docs.anthropic.com/en/docs/claude-code/mcp',
'OpenCode|3|https://opencode.ai/docs/;https://opencode.ai/docs/tools/;https://opencode.ai/docs/config/',
'Cursor|4|https://docs.cursor.com/welcome;https://docs.cursor.com/context/rules-for-ai;https://docs.cursor.com/background-agents/overview',
'Windsurf|4|https://docs.windsurf.com/windsurf/cascade/memories;https://docs.windsurf.com/windsurf/cascade/terminal;https://docs.windsurf.com/windsurf/workflows',
'Aider|5|https://aider.chat/docs/;https://aider.chat/docs/usage/commands.html;https://aider.chat/docs/usage/lint-test.html',
'Continue|4|https://docs.continue.dev/intro;https://docs.continue.dev/customize/context-providers;https://docs.continue.dev/customize/deep-dives/config-yaml',
'Cline|4|https://docs.cline.bot/;https://docs.cline.bot/features/plan-and-act;https://docs.cline.bot/features/mcp-servers',
'GitHub Copilot|5|https://docs.github.com/en/copilot;https://docs.github.com/en/copilot/how-tos/use-copilot-agents/coding-agent/extend-copilot-coding-agent-with-mcp;https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/configure-automatic-code-review-by-copilot',
'Devin|4|https://docs.devin.ai/;https://docs.devin.ai/get-started/quickstart;https://docs.devin.ai/work-with-devin/devin-search/deepwiki',
'SWE-agent|4|https://swe-agent.com/latest/;https://swe-agent.com/latest/usage/quickstart/;https://swe-agent.com/latest/usage/tools/',
'OpenAI Codex|4|https://platform.openai.com/docs/guides/codex;https://platform.openai.com/docs/guides/tools;https://platform.openai.com/docs/guides/mcp',
'OpenHands|4|https://docs.all-hands.dev/;https://docs.all-hands.dev/usage/getting-started;https://github.com/All-Hands-AI/OpenHands',
'Replit Agent|4|https://docs.replit.com/replitai/agent;https://docs.replit.com/replitai/app-building-guide',
'Amazon Q Developer|5|https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/what-is.html;https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/command-line.html;https://aws.amazon.com/q/developer/',
'Gemini Code Assist|5|https://cloud.google.com/gemini/docs/codeassist/overview;https://cloud.google.com/products/gemini/code-assist'
)

$d=@(
'Agent orchestration and autonomy|5|4|Multi-step planning and autonomous execution support.',
'Context and memory systems|4|4|Persistent project context, rules, or memory capture.',
'Search and retrieval|4|4|Repository and knowledge retrieval across code and docs.',
'Code analysis and refactoring|5|5|Static analysis help and structural code change workflows.',
'Test generation and execution|5|4|Test authoring help and validation loops.',
'Shell and filesystem control|4|4|Command execution and file operations.',
'Browser and UI control|3|2|UI/browser automation or preview capability.',
'API and external integrations (including MCP/tool ecosystems)|4|4|Extensibility via APIs, MCP, plugins, or tool adapters.',
'Debugging and observability|5|5|Root-cause analysis, diagnostics, traces, and triage.',
'Docs and knowledge generation|3|3|Documentation drafting and knowledge packaging.',
'Workflow automation and CI/CD hooks|4|4|Automation of recurring engineering and CI flows.',
'Safety, approval, and permission models|5|5|Permission controls, approval gates, and safety boundaries.'
)

$l=@{
'Claude Code'='3,2,2,3,3,3,1,3,3,3,2,3';
'OpenCode'='3,2,2,3,3,3,2,3,3,3,2,3';
'Cursor'='2,2,2,3,2,2,1,2,2,2,2,2';
'Windsurf'='3,2,2,3,2,3,2,2,2,2,2,2';
'Aider'='1,1,2,3,3,3,0,1,2,2,1,2';
'Continue'='2,2,2,3,2,1,0,2,2,2,1,2';
'Cline'='3,2,2,3,2,3,3,3,2,2,2,2';
'GitHub Copilot'='3,2,2,3,2,1,1,2,3,2,3,3';
'Devin'='3,3,2,3,3,3,3,2,3,2,3,3';
'SWE-agent'='3,1,2,3,3,3,0,1,3,2,3,2';
'OpenAI Codex'='3,2,2,3,3,3,1,3,3,3,2,3';
'OpenHands'='3,2,2,3,3,3,3,2,3,2,2,2';
'Replit Agent'='3,2,2,3,2,2,2,2,2,2,3,2';
'Amazon Q Developer'='2,1,2,3,2,1,0,2,3,2,2,3';
'Gemini Code Assist'='2,1,2,3,2,1,0,2,2,2,2,3'
}

$s='external-only','partial','composed','native'
$i='0,2,4,5' -split ',' | ForEach-Object {[int]$_}

$rows=[System.Collections.Generic.List[object]]::new()
foreach($pl in $p){
  $a=$pl -split '\|',3; $n=$a[0]; $rel=[int]$a[1]; $src=$a[2]; $lv=($l[$n]-split ',')
  for($x=0;$x -lt $d.Count;$x++){
    $da=$d[$x]-split '\|',4; $dn=$da[0]; $bu=[int]$da[1]; $ba=[int]$da[2]; $sm=$da[3]
    $le=[int]$lv[$x]; $st=$s[$le]; $in=$i[$le]
    $u=C($bu+($le-2)); $ap=C($ba+($le-2)); if($le -eq 0){$u=[Math]::Min($u,2);$ap=[Math]::Min($ap,2)}
    $ra=0; if($le -eq 0){$ra=-2}elseif($le -eq 1){$ra=-1}; $re=C($rel+$ra)
    $wv=[Math]::Round((25*$u+25*$ap+30*$in+20*$re)/5,1)
    $rows.Add([pscustomobject][ordered]@{Platform=$n;Domain=$dn;CapabilitySummary="$sm Integration profile for ${n}: $st.";IntegrationStatus=$st;UtilityScore=$u;ApplicabilityScore=$ap;IntegrationScore=$in;ReliabilityScore=$re;WorkflowValue=$wv;SourceRefs=$src;Notes="Derived from official documentation capture on $CapturedOn."})|Out-Null
  }
}
$rows|Export-Csv (Join-Path $AuditDir 'AI_CAPABILITY_MATRIX.csv') -NoTypeInformation -Encoding UTF8

$alines=@(
'agent-orchestration-sprint|Agent orchestration and autonomy|native|Gated multi-step engineering sprint.|Claude Code,Cline,Devin,OpenHands,SWE-agent,OpenAI Codex|functions.shell_command,functions.apply_patch,web.search_query,web.open',
'repo-context-scout|Search and retrieval|native|Map repository structure and dependency paths.|OpenCode,Continue,Cursor,Aider|functions.shell_command,web.search_query',
'cross-platform-capability-audit|Docs and knowledge generation|native|Audit capabilities across platforms with scoring.|Claude Code,OpenCode,Cursor,Windsurf,GitHub Copilot,OpenAI Codex|web.search_query,web.open,web.find,functions.shell_command',
'code-refactor-safety-pass|Code analysis and refactoring|native|Apply structural refactors with safety checks.|Cursor,Aider,GitHub Copilot,OpenAI Codex,Cline|functions.shell_command,functions.apply_patch',
'test-regression-hardening|Test generation and execution|native|Generate tests and run regression loops.|Aider,GitHub Copilot,Claude Code,SWE-agent,OpenHands|functions.shell_command,functions.apply_patch',
'shell-investigation-loop|Shell and filesystem control|native|Use iterative shell diagnostics and implementation loops.|Claude Code,OpenCode,Cline,Devin,SWE-agent,OpenAI Codex|functions.shell_command',
'browser-ui-repro|Browser and UI control|partial|Reproduce UI issues and gather browser evidence.|Cline,Windsurf,Devin,OpenHands,Replit Agent|web.open,web.find',
'mcp-integration-setup|API and external integrations (including MCP/tool ecosystems)|composed|Set up and validate MCP/tool integrations.|Claude Code,OpenCode,Cursor,Windsurf,GitHub Copilot,OpenAI Codex,Cline|functions.shell_command,web.search_query,web.open',
'api-integration-hardening|API and external integrations (including MCP/tool ecosystems)|native|Harden API integrations with resilience patterns.|OpenCode,GitHub Copilot,Aider,Continue|functions.apply_patch,functions.shell_command',
'debug-root-cause-sprint|Debugging and observability|native|Rapid root-cause analysis and fix validation.|Claude Code,GitHub Copilot,Devin,Amazon Q Developer,SWE-agent|functions.shell_command,functions.apply_patch',
'docs-knowledge-pack|Docs and knowledge generation|native|Generate implementation and validation documentation.|Claude Code,OpenCode,GitHub Copilot,Devin,Gemini Code Assist|functions.shell_command',
'ci-failure-triage|Workflow automation and CI/CD hooks|composed|Triage CI failures and implement fixes.|GitHub Copilot,OpenCode,Claude Code,SWE-agent|gh-fix-ci,functions.shell_command,functions.apply_patch'
)
$alines += @(
'dependency-risk-audit|Code analysis and refactoring|native|Assess dependency vulnerabilities and update risk.|OpenCode,GitHub Copilot,Amazon Q Developer,Gemini Code Assist|functions.shell_command,web.search_query',
'migration-planning-pack|Workflow automation and CI/CD hooks|native|Build migration plan with rollback and risk tiers.|OpenCode,Continue,GitHub Copilot,Claude Code|functions.shell_command,functions.apply_patch',
'pr-review-sweep|Code analysis and refactoring|composed|Run severity-first PR review and remediation hints.|GitHub Copilot,Claude Code,OpenCode,Cline|functions.shell_command,gh-address-comments',
'issue-reproducer-loop|Debugging and observability|native|Convert issue reports into deterministic repro loops.|SWE-agent,Devin,Aider,OpenHands|functions.shell_command,functions.apply_patch',
'memory-capture-brief|Context and memory systems|native|Capture decisions, assumptions, and unresolved risks.|Claude Code,Cursor,Windsurf,Devin,Continue|functions.shell_command',
'safety-gated-execution|Safety, approval, and permission models|native|Run risky operations with explicit gates and checks.|Claude Code,GitHub Copilot,Amazon Q Developer,OpenAI Codex,Cline|functions.shell_command',
'performance-optimization-pass|Debugging and observability|native|Optimize hot paths and validate performance gains.|Aider,OpenCode,Amazon Q Developer,Gemini Code Assist,Claude Code|functions.shell_command,functions.apply_patch',
'platform-parity-compare|Docs and knowledge generation|native|Compare platform capability parity for a workflow.|All|web.search_query,web.open,web.find',
'workflow-automation-blueprint|Workflow automation and CI/CD hooks|native|Design reusable automation blueprints and runbooks.|OpenCode,Continue,GitHub Copilot,Replit Agent,OpenAI Codex|functions.shell_command,functions.apply_patch'
)

$aliases=[System.Collections.Generic.List[object]]::new()
foreach($ln in $alines){
  $z=$ln -split '\|',6
  $an=$z[0]; $cat=$z[1]; $st=$z[2]; $intent=$z[3]
  $sp=$z[4] -split ',' | ForEach-Object { $_.Trim() }
  $tools=$z[5] -split ',' | ForEach-Object { $_.Trim() }
  $aliases.Add([pscustomobject][ordered]@{alias=$an;category=$cat;intent=$intent;source_platforms=$sp;execution_pattern=@('Discover context and constraints.','Execute bounded implementation steps.','Validate outputs and summarize results.');required_tools=$tools;status=$st;limitations=@('Depends on available environment access and configured integrations.');example_prompts=@("cap:$an","run $an workflow")})|Out-Null
}

$registry=[ordered]@{
  version='1.0.0';captured_on=$CapturedOn;
  scoring_model=[ordered]@{utility='0-5';applicability='0-5';integration='0-5';reliability='0-5';workflow_value_formula='(25*Utility + 25*Applicability + 30*Integration + 20*Reliability)/5'};
  platforms=($p|ForEach-Object{($_ -split '\|',2)[0]});
  domains=($d|ForEach-Object{($_ -split '\|',2)[0]});
  aliases=$aliases
}
$registry|ConvertTo-Json -Depth 12|Set-Content (Join-Path $AuditDir 'capability_alias_registry.json') -Encoding UTF8
$schemaObj=[ordered]@{
  '$schema'='https://json-schema.org/draft/2020-12/schema';
  '$id'='https://wowclassicgrindbot.local/schemas/capability-alias-schema.json';
  title='Capability Alias Registry';
  type='object';
  required=@('version','captured_on','platforms','domains','aliases');
  properties=[ordered]@{
    version=@{type='string';pattern='^[0-9]+\\.[0-9]+\\.[0-9]+$'};
    captured_on=@{type='string';pattern='^[0-9]{4}-[0-9]{2}-[0-9]{2}$'};
    platforms=@{type='array';minItems=1;items=@{type='string'}};
    domains=@{type='array';minItems=1;items=@{type='string'}};
    aliases=@{type='array';minItems=1;items=@{type='object';required=@('alias','category','intent','source_platforms','execution_pattern','required_tools','status','limitations','example_prompts');properties=@{alias=@{type='string';pattern='^[a-z0-9]+(?:-[a-z0-9]+)*$'};category=@{type='string'};intent=@{type='string'};source_platforms=@{type='array';minItems=1;items=@{type='string'}};execution_pattern=@{type='array';minItems=1;items=@{type='string'}};required_tools=@{type='array';minItems=1;items=@{type='string'}};status=@{type='string';enum=@('native','composed','partial','external-only')};limitations=@{type='array';items=@{type='string'}};example_prompts=@{type='array';minItems=1;items=@{type='string'}}};additionalProperties=$false}}
  };
  additionalProperties=$false
}
$schemaObj|ConvertTo-Json -Depth 15|Set-Content (Join-Path $AuditDir 'capability_alias_schema.json') -Encoding UTF8

$ps=$rows|Group-Object Platform|ForEach-Object{[ordered]@{Platform=$_.Name;Native=@($_.Group|?{$_.IntegrationStatus -eq 'native'}).Count;Composed=@($_.Group|?{$_.IntegrationStatus -eq 'composed'}).Count;Partial=@($_.Group|?{$_.IntegrationStatus -eq 'partial'}).Count;ExternalOnly=@($_.Group|?{$_.IntegrationStatus -eq 'external-only'}).Count;AverageWorkflowValue=[Math]::Round((($_.Group|Measure-Object WorkflowValue -Average).Average),1)}}|Sort-Object AverageWorkflowValue -Descending
$pt=($ps|%{"| $($_.Platform) | $($_.Native) | $($_.Composed) | $($_.Partial) | $($_.ExternalOnly) | $($_.AverageWorkflowValue) |"}) -join "`r`n"
$st=($p|%{$pa=$_ -split '\|',3;"| $($pa[0]) | " + (($pa[2]-split ';'|%{"[$_]($_)"}) -join '<br/>') + ' |'}) -join "`r`n"
$top=($rows|Sort-Object WorkflowValue -Descending|Select-Object -First 25|%{"| $($_.Platform) | $($_.Domain) | $($_.IntegrationStatus) | $($_.WorkflowValue) |"}) -join "`r`n"

$auditLines=@(
'# AI Platform Capability Audit (March 2026)',
'',
"Captured on: **$CapturedOn**",
'',
'## Scope',
'',
'This audit covers: Claude Code, OpenCode, Cursor, Windsurf, Aider, Continue, Cline, GitHub Copilot, Devin, SWE-agent, OpenAI Codex, OpenHands, Replit Agent, Amazon Q Developer, and Gemini Code Assist.',
'',
'## Methodology',
'',
'1. Collect official docs links per platform.',
'2. Normalize capabilities into 12 fixed domains.',
'3. Score each row using WorkflowValue = (25*Utility + 25*Applicability + 30*Integration + 20*Reliability)/5.',
'4. Classify integration status as native, composed, partial, or external-only.',
'',
'## Source Index (Official Docs)',
'',
'| Platform | Official sources |',
'|---|---|',
$st,
'',
'## Platform Summary',
'',
'| Platform | Native | Composed | Partial | External-only | Avg Workflow Value |',
'|---|---:|---:|---:|---:|---:|',
$pt,
'',
'## Top Capability Rows by Workflow Value',
'',
'| Platform | Domain | Status | Workflow Value |',
'|---|---|---|---:|',
$top,
'',
'## Notes',
'',
'1. Browser/UI control is uneven across platforms.',
'2. MCP/tool ecosystem support is broad but implementation details differ.',
"3. Scores represent point-in-time docs evidence captured on $CapturedOn."
)
$audit=$auditLines -join "`r`n"
$audit|Set-Content (Join-Path $AuditDir 'AI_PLATFORM_AUDIT_2026-03.md') -Encoding UTF8
$qt=($aliases|%{"| $($_.alias) | $($_.category) | $($_.status) | $($_.intent) |"}) -join "`r`n"
$cat=@();foreach($g in ($aliases|Group-Object category|Sort-Object Name)){ $sec="## $($g.Name)`r`n"; foreach($e in $g.Group){$sec += "- **$($e.alias)**: $($e.intent) Example: cap:$($e.alias). Status: $($e.status).`r`n"}; $cat+=$sec }
$catTxt=$cat -join "`r`n"
$best=@(
'| Audit | cross-platform-capability-audit | Best for source-backed platform comparisons. |',
'| Refactor | code-refactor-safety-pass | Best for safe structural changes. |',
'| Testing | test-regression-hardening | Best for regression-focused test loops. |',
'| Debug | debug-root-cause-sprint | Best for fast diagnosis and validation. |',
'| CI | ci-failure-triage | Best for failing check triage. |',
'| Docs | docs-knowledge-pack | Best for implementation summaries. |',
'| Integration | mcp-integration-setup | Best for tool ecosystem wiring. |',
'| Triage | issue-reproducer-loop | Best for repro-first bug resolution. |'
) -join "`r`n"

$guideLines=@(
'# AI Capability Internalization Guide',
'',
"Captured on: **$CapturedOn**",
'',
'## Alias Invocation Grammar',
'',
'1. cap:<alias>',
'2. use capability <alias>',
'3. run <alias> workflow',
'',
'## What You Can Ask by Name',
'',
'| Alias | Category | Status | Function |',
'|---|---|---|---|',
$qt,
'',
'## Category Reference',
'',
$catTxt,
'',
'## Status Legend',
'',
'1. native: Directly executable with current tools/skills.',
'2. composed: Executable by chaining current tools/skills.',
'3. partial: Some steps need manual handling or extra infra.',
'4. external-only: Not internalizable with current boundaries.',
'',
'## Best Alias Per Workflow',
'',
'| Workflow | Recommended alias | Rationale |',
'|---|---|---|',
$best,
'',
'## 20 Routing Validation Prompts',
'',
'1. cap:agent-orchestration-sprint',
'2. cap:repo-context-scout',
'3. cap:cross-platform-capability-audit',
'4. cap:code-refactor-safety-pass',
'5. cap:test-regression-hardening',
'6. cap:shell-investigation-loop',
'7. cap:browser-ui-repro',
'8. cap:mcp-integration-setup',
'9. cap:api-integration-hardening',
'10. cap:debug-root-cause-sprint',
'11. cap:docs-knowledge-pack',
'12. cap:ci-failure-triage',
'13. cap:dependency-risk-audit',
'14. cap:migration-planning-pack',
'15. cap:pr-review-sweep',
'16. cap:issue-reproducer-loop',
'17. cap:memory-capture-brief',
'18. cap:safety-gated-execution',
'19. cap:performance-optimization-pass',
'20. cap:platform-parity-compare'
)
$guide=$guideLines -join "`r`n"
$guide|Set-Content (Join-Path $AuditDir 'AI_CAPABILITY_INTERNALIZATION_GUIDE.md') -Encoding UTF8
Write-Host "Generated artifacts in $AuditDir"


