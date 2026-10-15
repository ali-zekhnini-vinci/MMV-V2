## graphify

This project can use a local knowledge graph in `graphify-out/` (god nodes, community structure, cross-file relationships).
The graph is local and not versioned (`graphify-out/` is git-ignored). If `graphify-out/graph.json` is missing or stale, rebuild it with `graphify update .` (AST-only, local; never run LLM/cloud semantic extraction).

Only the Graphify **CLI** is set up. No Graphify MCP server is configured, and no cloud graph exists.

Usage (recommended, not mandatory):
- For codebase questions, `graphify query "<question>"`, `graphify path "<A>" "<B>"` and `graphify explain "<concept>"` return a scoped subgraph, often smaller than raw grep output. Direct reading and searching of files remain allowed.
- Read `graphify-out/GRAPH_REPORT.md` only for broad architecture review.
- After modifying code, run `graphify update .` to keep the graph current.

## DOCUMENTATION GATE

Mandatory before every delivery, report or status change (`READY_FOR_OWNER_REVIEW`, commit, push request):

1. Read the documents affected by the change before editing them.
2. Synchronize, according to impact: `README.md`, `docs/phase/NN.md`, `docs/phase/NN/SOURCE_MAP.md`, `IMPLEMENTATION_SPEC.md`, `QA_MATRIX.md`, `GATES_AND_DECISIONS.md`, and `docs/ADR_ET_DECISIONS.md` / backlog (`docs/PERIMETRE_ET_SEQUENCE.md`).
3. Check that every internal path cited in those documents exists, and that statuses are consistent across documents (phase states, PASS/BLOCKED/NOT_TESTED, HTTP vs HTTPS, tool CLI vs MCP).
4. Never declare `READY_FOR_OWNER_REVIEW` while any document carries an obsolete status.
5. Never falsify history: add dated entries instead of rewriting past results, PASS statuses, GO decisions or IN/OUT scopes. Never mark something approved, merged or PASS without the matching evidence or owner decision.
6. Commands that were not re-run since the last change are reported as `NOT_RERUN`, not PASS.
