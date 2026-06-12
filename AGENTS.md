# Repository Guidance

## DevContext MCP

Use the `devcontext` MCP server for questions about indexed internal NuGet
packages, public .NET symbols, implementation examples, and company
documentation. Prefer it over model memory or general web search for those
subjects.

Follow this workflow:

1. Call `resolve_library` with the package, client, or implementation concept.
2. For NuGet libraries, call `list_versions` and select a version compatible
   with the project. Prefer the project's referenced version when available.
3. Call `query_docs` for implementation guidance and examples, or `get_symbol`
   for a specific public type or member.
4. Preserve citation URIs and mention important warnings or insufficient
   evidence in the answer.

For company documentation, resolve or query `docs:company-docs`. Do not call
`list_versions` or `get_symbol` for company documentation.

Do not invent APIs when DevContext returns `not_found`,
`insufficient_evidence`, or `not_ready`. Inspect the local repository for
additional evidence and clearly state any remaining uncertainty.

The server is configured as `devcontext` in `.codex/config.toml` at
`http://127.0.0.1:2222/mcp`. If its tools are unavailable, verify the local
server is running and start a new Codex session after it becomes available.
