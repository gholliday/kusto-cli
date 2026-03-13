# Session Prompt: Create a new CLI + MCP tool

Use this prompt in a new Copilot CLI session to scaffold and implement a new tool following the patterns established in `kusto-cli`.

---

## Prompt

```
I need to create a new CLI tool with MCP server support, following the exact patterns from https://github.com/gholliday/kusto-cli (branch: mcp-wrapper-design).

### What I want to build
- **Tool name**: [e.g. cosmos, servicebus, storage]
- **What it connects to**: [e.g. Azure Cosmos DB, Azure Service Bus]
- **Core operations**: [e.g. list databases, query containers, send messages]

### Architecture (copy from kusto-cli)
1. **Scaffold from template**: Run `dotnet new mcp-tool -n {Name} --toolPrefix {prefix} --serverName {prefix}-mcp -o {dir}` (template already installed — if not, install from the kusto-cli repo's `templates/mcp-tool` directory)

2. **CLI project** (`src/{Name}/{Name}.csproj`): The standalone CLI tool
   - Use System.CommandLine for subcommands (`{tool} {noun} {verb}`)
   - Auth via `DefaultAzureCredential` only (guidance: "run az login")
   - Config persisted to `~/.{toolname}/config.json` via `IConfigStore` pattern
   - Errors use `UserFacingException` for actionable messages
   - Output via `IOutputFormatter` supporting human/json/markdown formats
   - Source-generated JSON serializers for AOT compatibility

3. **MCP project** (`src/{Name}.Mcp/{Name}.Mcp.csproj`): The MCP server wrapper
   - Uses `ModelContextProtocol` NuGet (v1.1.0) + `Microsoft.Extensions.Hosting`
   - `[McpServerToolType]` class with `[McpServerTool]` attributed methods
   - Tool names: underscore-separated (`{prefix}_query`, `{prefix}_list`, etc.) — dots are NOT allowed by some MCP clients
   - Set `ReadOnly`, `Destructive`, `Idempotent`, `OpenWorld` annotations on each tool
   - Parameter descriptions via `[Description]` attributes — these are the LLM's docs
   - Convert `UserFacingException` → `McpException` so MCP clients see clean errors
   - Services injected via DI (register in Program.cs, accept as constructor/method params)
   - `setup` and `install` subcommands for MCP client config (McpSetup.cs is already generated)
   - STDIO transport via `WithStdioServerTransport()` (newline-delimited JSON, NOT Content-Length)

4. **Tests** (`tests/{Name}.Tests/`): xUnit tests
   - Test tool methods directly (they're just async methods)
   - Use delegate-based fakes for service interfaces (see TestRuntimeFactory.cs pattern in kusto-cli)

5. **Publishing**: Self-contained single-file to `~/.{toolname}/bin/`
   ```
   dotnet publish src/{Name}.Mcp -c Release -r win-arm64 --self-contained -p:PublishSingleFile=true -p:PublishAot=false -o ~/.{toolname}/bin
   ```

### Key lessons from kusto-cli
- SDK uses newline-delimited JSON for STDIO, NOT Content-Length framing
- `builder.Logging.ClearProviders()` before adding console — prevents stdout pollution
- `AppContext.BaseDirectory` not `Assembly.Location` (empty in single-file apps)
- Launcher scripts need `%~dp0` (cmd) / `$(dirname "$0")` (sh) for path resolution
- `PublishAot=true` in csproj but publish with `-p:PublishAot=false` if missing C++ build tools
- OSC 8 hyperlinks for terminal clickable paths: `\e]8;;{uri}\e\\{text}\e]8;;\e\\`
- Guard ANSI with `Environment.UserInteractive && !Console.IsOutputRedirected`

### Reference implementation
Look at these files in kusto-cli for the established patterns:
- `src/Kusto.Cli.Mcp/KustoTools.cs` — 7 tool implementations with annotations
- `src/Kusto.Cli.Mcp/Program.cs` — Host builder with DI wiring
- `src/Kusto.Cli.Mcp/McpSetup.cs` — Setup/install with 4-client config snippets
- `src/Kusto.Cli/Contracts.cs` — Interface definitions (IConfigStore, IKustoService, etc.)
- `src/Kusto.Cli/CliRunner.cs` — CreateRuntime() composition root
- `tests/Kusto.Cli.Tests/TestRuntimeFactory.cs` — Fake service infrastructure
```

---

## Quick reference: Template usage

```powershell
# Install template (one-time)
dotnet new install ./templates/mcp-tool

# Create new tool
dotnet new mcp-tool -n CosmosDbTool --toolPrefix cosmos --serverName cosmos-mcp -o C:\code\cosmos-tool

# Build & test
dotnet build CosmosDbTool.slnx
dotnet test CosmosDbTool.slnx

# Run MCP server (dev)
dotnet run --project src/CosmosDbTool.Mcp

# Publish
dotnet publish src/CosmosDbTool.Mcp -c Release -r win-arm64 --self-contained -p:PublishSingleFile=true -p:PublishAot=false -o ~/.cosmos/bin
```
