using Kusto.Cli;
using Kusto.Cli.Mcp;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

if (args is ["setup"])
{
    McpSetup.PrintSetupInstructions();
    return 0;
}

if (args is ["install"])
{
    return McpSetup.InstallToConfig();
}

if (args.Length > 0)
{
    Console.Error.WriteLine($"Unknown command '{args[0]}'. Usage:");
    Console.Error.WriteLine("  kusto-mcp          Run the MCP server over STDIO");
    Console.Error.WriteLine("  kusto-mcp setup    Print client configuration snippets");
    Console.Error.WriteLine("  kusto-mcp install  Add to GitHub Copilot CLI config");
    return 1;
}

var builder = Host.CreateApplicationBuilder();
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddSingleton(_ => CliRunner.CreateRuntime(requestedLogLevel: null));
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new() { Name = "kusto-mcp", Version = typeof(KustoTools).Assembly.GetName().Version?.ToString() ?? "0.0.0" };
    })
    .WithStdioServerTransport()
    .WithTools<KustoTools>();

await builder.Build().RunAsync();
return 0;
