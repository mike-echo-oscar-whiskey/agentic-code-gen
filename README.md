# Agent-Orchestrated Data Code Generator

A user states a goal; a **Coding Agent** generates TypeScript against the
[Met Museum Collection API](https://metmuseum.github.io/), a **Review Agent**
critiques it, and the user watches both work in real time.

When the review comes back `changes-requested`, the orchestrator feeds the
findings to the Coding Agent for one revision round and the reviewer
re-judges — the UI keeps the superseded version and the findings that
killed it.

- **Backend**: .NET 10 Minimal API, Anthropic Messages API (official C# SDK)
- **Frontend**: Angular 22 (signals, strict TS), live agent timeline over SSE
- **Tests**: 51 backend (xUnit + NSubstitute + AwesomeAssertions), 3 frontend (vitest)

## Running it

```bash
# Backend (terminal 1) — key lives in user secrets, never in the repo
dotnet user-secrets set "Anthropic:ApiKey" "<key>" --project api/AgentCodeGen.Api
dotnet run --project api/AgentCodeGen.Api          # http://localhost:5117

# Frontend (terminal 2)
cd web && npm install && npm start                 # http://localhost:4200 (proxies /api)
```

Without an API key the backend falls back to a stubbed workflow, so the full
UI still demos end-to-end offline.

Tests: `dotnet test api` and `cd web && CI=true ng test --watch=false`.

## How it works

```
Angular ──POST /api/runs──────────▶ Minimal API
   ▲                                    │ Task.Run
   │◀──SSE /api/runs/{id}/events──  AgentWorkflow
   │◀──GET /api/runs/{id}───────        │
                                        ├─ CodingAgent ──▶ Anthropic (forced tool: emit_code)
                                        │      ▲ grounding: one real, trimmed Met /objects sample
                                        ├─ 4 validation gates (deterministic)
                                        ├─ ReviewAgent ──▶ Anthropic (forced tool: emit_review)
                                        └─ changes-requested? → revise (once) → gates → re-review
```

`InMemoryRunStore` holds run state and fans events out to SSE subscribers via
`Channel<T>` — replay for late subscribers, live streaming while running.

## Design decisions

**Structured output, never prose parsing.** Both agents answer through a
strict tool schema (`additionalProperties: false`, forced `tool_choice`).
The payload is validated again at the boundary (required fields, closed
severity enum) before it becomes a domain type. `strict` guarantees shape,
not truth — hence the second validation.

**The goal and all fetched data are untrusted input.** The user's goal
travels in a *user* turn, never concatenated into the system prompt. The Met
sample is trimmed to a structural field allowlist (free-text fields like
`tags`/`constituents` are dropped — the classic injection carrier), capped,
and delimited as "data, not instructions". Honest caveat: prompt hardening
is not a reliable control. The controls I actually rely on are the
deterministic gates and the next point.

**Generated code is an artifact, never executed.** A deliberate choice, not
an omission. Executing untrusted generated code needs real isolation
(container sandboxes, gVisor); showing it for human review is the stronger
default for this scope. The UI renders it as escaped text — no `innerHTML`,
so a generated `<script>` can't become stored XSS.

**Deterministic gates before display.** Four checks run on every artifact:
banned constructs (`eval`, `Function`, `child_process`), secret-shaped
literals, a Met-only host allowlist, and dependency resolution against the
real npm registry (models hallucinate package names; attackers pre-register
the popular hallucinations). Gates are informational badges here; in CI they
would block.

**Grounding beats bigger prompts.** One real API response, trimmed and
cached, makes the generated types match the actual field names
(`artistDisplayName`, `primaryImageSmall`) instead of a plausible guess.
Grounding failure degrades to ungrounded generation — never fails the run.

**SSE, not WebSockets.** The event flow is strictly server→client, so SSE is
the right size: plain HTTP, auto-reconnect in the browser, no extra protocol.
A snapshot endpoint covers reconnects and refreshes.

**Vendor behind a seam.** `IStructuredOutputClient` is the only surface the
agents see; `AnthropicStructuredOutputClient` is the only file that knows the
vendor SDK. Explicit per-call timeout (the SDK default is far beyond an
interactive budget), `max_tokens` stop-reason handled, errors mapped to
domain errors. Swapping vendors — or putting a cheaper model on the review
step — is one file plus config.

**The revise loop is bounded.** One revision round (`MaxRevisions = 1`).
Convergence isn't guaranteed — a reviewer can keep finding minor issues
forever, and each round costs two model calls. One round fixes the majors;
beyond that, escalating to the human beats burning tokens.

**Functional error handling.** Hand-rolled `Option<T>`/`Either<Error,T>` in
the domain, matched away at the edges. Agent failures are values, not
exceptions; the workflow fails fast and the timeline shows exactly which
agent failed and why.

## Testing philosophy

Deterministic and probabilistic concerns are separated:

- **Deterministic (blocking, in the suite):** prompt construction (exact
  request shape — proves the goal never enters the system prompt), payload
  validation, gates, orchestration order, SSE plumbing through
  `WebApplicationFactory`. The LLM is stubbed everywhere.
- **Probabilistic (not asserted in CI):** whether the model writes *good*
  code. That's what the Review Agent, the gates, and — with more time — a
  golden set with property assertions are for. Asserting exact strings on
  model output would be testing the weather.

## What I'd do with more time

1. **`tsc --noEmit` gate** — compile the artifact; parse failures are
   deterministic defects.
2. **Persistence + run history** — the store interface is ready; swap
   in-memory for SQLite.
3. **Resilience polish** — retry with jitter on 429/5xx only,
   circuit-breaker on the vendor client, token budgets per run.
4. **Observability** — OpenTelemetry GenAI attributes (model, token usage,
   finish reason) per agent call; currently structured logs only.
