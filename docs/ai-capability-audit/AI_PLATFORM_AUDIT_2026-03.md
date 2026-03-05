# AI Platform Capability Audit (March 2026)

Captured on: **2026-03-05**

## Scope

This audit covers: Claude Code, OpenCode, Cursor, Windsurf, Aider, Continue, Cline, GitHub Copilot, Devin, SWE-agent, OpenAI Codex, OpenHands, Replit Agent, Amazon Q Developer, and Gemini Code Assist.

## Methodology

1. Collect official docs links per platform.
2. Normalize capabilities into 12 fixed domains.
3. Score each row using WorkflowValue = (25*Utility + 25*Applicability + 30*Integration + 20*Reliability)/5.
4. Classify integration status as native, composed, partial, or external-only.

## Source Index (Official Docs)

| Platform | Official sources |
|---|---|
| Claude Code | [https://docs.anthropic.com/en/docs/claude-code/overview](https://docs.anthropic.com/en/docs/claude-code/overview)<br/>[https://docs.anthropic.com/en/docs/claude-code/mcp](https://docs.anthropic.com/en/docs/claude-code/mcp) |
| OpenCode | [https://opencode.ai/docs/](https://opencode.ai/docs/)<br/>[https://opencode.ai/docs/tools/](https://opencode.ai/docs/tools/)<br/>[https://opencode.ai/docs/config/](https://opencode.ai/docs/config/) |
| Cursor | [https://docs.cursor.com/welcome](https://docs.cursor.com/welcome)<br/>[https://docs.cursor.com/context/rules-for-ai](https://docs.cursor.com/context/rules-for-ai)<br/>[https://docs.cursor.com/background-agents/overview](https://docs.cursor.com/background-agents/overview) |
| Windsurf | [https://docs.windsurf.com/windsurf/cascade/memories](https://docs.windsurf.com/windsurf/cascade/memories)<br/>[https://docs.windsurf.com/windsurf/cascade/terminal](https://docs.windsurf.com/windsurf/cascade/terminal)<br/>[https://docs.windsurf.com/windsurf/workflows](https://docs.windsurf.com/windsurf/workflows) |
| Aider | [https://aider.chat/docs/](https://aider.chat/docs/)<br/>[https://aider.chat/docs/usage/commands.html](https://aider.chat/docs/usage/commands.html)<br/>[https://aider.chat/docs/usage/lint-test.html](https://aider.chat/docs/usage/lint-test.html) |
| Continue | [https://docs.continue.dev/intro](https://docs.continue.dev/intro)<br/>[https://docs.continue.dev/customize/context-providers](https://docs.continue.dev/customize/context-providers)<br/>[https://docs.continue.dev/customize/deep-dives/config-yaml](https://docs.continue.dev/customize/deep-dives/config-yaml) |
| Cline | [https://docs.cline.bot/](https://docs.cline.bot/)<br/>[https://docs.cline.bot/features/plan-and-act](https://docs.cline.bot/features/plan-and-act)<br/>[https://docs.cline.bot/features/mcp-servers](https://docs.cline.bot/features/mcp-servers) |
| GitHub Copilot | [https://docs.github.com/en/copilot](https://docs.github.com/en/copilot)<br/>[https://docs.github.com/en/copilot/how-tos/use-copilot-agents/coding-agent/extend-copilot-coding-agent-with-mcp](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/coding-agent/extend-copilot-coding-agent-with-mcp)<br/>[https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/configure-automatic-code-review-by-copilot](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/configure-automatic-code-review-by-copilot) |
| Devin | [https://docs.devin.ai/](https://docs.devin.ai/)<br/>[https://docs.devin.ai/get-started/quickstart](https://docs.devin.ai/get-started/quickstart)<br/>[https://docs.devin.ai/work-with-devin/devin-search/deepwiki](https://docs.devin.ai/work-with-devin/devin-search/deepwiki) |
| SWE-agent | [https://swe-agent.com/latest/](https://swe-agent.com/latest/)<br/>[https://swe-agent.com/latest/usage/quickstart/](https://swe-agent.com/latest/usage/quickstart/)<br/>[https://swe-agent.com/latest/usage/tools/](https://swe-agent.com/latest/usage/tools/) |
| OpenAI Codex | [https://platform.openai.com/docs/guides/codex](https://platform.openai.com/docs/guides/codex)<br/>[https://platform.openai.com/docs/guides/tools](https://platform.openai.com/docs/guides/tools)<br/>[https://platform.openai.com/docs/guides/mcp](https://platform.openai.com/docs/guides/mcp) |
| OpenHands | [https://docs.all-hands.dev/](https://docs.all-hands.dev/)<br/>[https://docs.all-hands.dev/usage/getting-started](https://docs.all-hands.dev/usage/getting-started)<br/>[https://github.com/All-Hands-AI/OpenHands](https://github.com/All-Hands-AI/OpenHands) |
| Replit Agent | [https://docs.replit.com/replitai/agent](https://docs.replit.com/replitai/agent)<br/>[https://docs.replit.com/replitai/app-building-guide](https://docs.replit.com/replitai/app-building-guide) |
| Amazon Q Developer | [https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/what-is.html](https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/what-is.html)<br/>[https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/command-line.html](https://docs.aws.amazon.com/amazonq/latest/qdeveloper-ug/command-line.html)<br/>[https://aws.amazon.com/q/developer/](https://aws.amazon.com/q/developer/) |
| Gemini Code Assist | [https://cloud.google.com/gemini/docs/codeassist/overview](https://cloud.google.com/gemini/docs/codeassist/overview)<br/>[https://cloud.google.com/products/gemini/code-assist](https://cloud.google.com/products/gemini/code-assist) |

## Platform Summary

| Platform | Native | Composed | Partial | External-only | Avg Workflow Value |
|---|---:|---:|---:|---:|---:|
| OpenAI Codex | 8 | 3 | 1 | 0 | 86.4 |
| SWE-agent | 6 | 3 | 2 | 1 | 78.1 |
| Devin | 9 | 3 | 0 | 0 | 89.9 |
| OpenHands | 6 | 6 | 0 | 0 | 86.8 |
| Gemini Code Assist | 2 | 7 | 2 | 1 | 77.6 |
| Amazon Q Developer | 3 | 6 | 2 | 1 | 78.1 |
| Replit Agent | 3 | 9 | 0 | 0 | 84 |
| GitHub Copilot | 5 | 5 | 2 | 0 | 84.7 |
| Cursor | 1 | 10 | 1 | 0 | 79.6 |
| OpenCode | 8 | 4 | 0 | 0 | 84.6 |
| Claude Code | 8 | 3 | 1 | 0 | 90.4 |
| Windsurf | 3 | 9 | 0 | 0 | 84 |
| Cline | 5 | 7 | 0 | 0 | 86.7 |
| Continue | 1 | 8 | 2 | 1 | 73.1 |
| Aider | 3 | 4 | 4 | 1 | 75 |

## Top Capability Rows by Workflow Value

| Platform | Domain | Status | Workflow Value |
|---|---|---|---:|
| GitHub Copilot | Agent orchestration and autonomy | native | 100 |
| GitHub Copilot | Code analysis and refactoring | native | 100 |
| Gemini Code Assist | Safety, approval, and permission models | native | 100 |
| Aider | Code analysis and refactoring | native | 100 |
| Aider | Test generation and execution | native | 100 |
| Aider | Shell and filesystem control | native | 100 |
| GitHub Copilot | Debugging and observability | native | 100 |
| Amazon Q Developer | Debugging and observability | native | 100 |
| Amazon Q Developer | Safety, approval, and permission models | native | 100 |
| Gemini Code Assist | Code analysis and refactoring | native | 100 |
| GitHub Copilot | Workflow automation and CI/CD hooks | native | 100 |
| GitHub Copilot | Safety, approval, and permission models | native | 100 |
| Amazon Q Developer | Code analysis and refactoring | native | 100 |
| Claude Code | Test generation and execution | native | 100 |
| Claude Code | Code analysis and refactoring | native | 100 |
| Claude Code | Safety, approval, and permission models | native | 100 |
| Claude Code | Agent orchestration and autonomy | native | 100 |
| Claude Code | Shell and filesystem control | native | 100 |
| Claude Code | Debugging and observability | native | 100 |
| Claude Code | API and external integrations (including MCP/tool ecosystems) | native | 100 |
| SWE-agent | Agent orchestration and autonomy | native | 96 |
| Devin | Safety, approval, and permission models | native | 96 |
| Devin | Workflow automation and CI/CD hooks | native | 96 |
| SWE-agent | Code analysis and refactoring | native | 96 |
| SWE-agent | Debugging and observability | native | 96 |

## Notes

1. Browser/UI control is uneven across platforms.
2. MCP/tool ecosystem support is broad but implementation details differ.
3. Scores represent point-in-time docs evidence captured on 2026-03-05.
