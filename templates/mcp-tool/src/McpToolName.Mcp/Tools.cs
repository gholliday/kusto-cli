using System.ComponentModel;
using ModelContextProtocol.Server;

namespace McpToolName.Mcp;

[McpServerToolType]
public sealed class Tools
{
    // Example read-only tool
    [McpServerTool(Name = "tooltoolprefix_hello", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("Say hello. Replace this with your first real tool.")]
    public static string Hello(
        [Description("Name to greet.")] string name)
    {
        return $"Hello, {name}!";
    }

    // Example async tool with DI — uncomment and adapt when you have services
    // [McpServerTool(Name = "tooltoolprefix_query", ReadOnly = true, Idempotent = true, OpenWorld = false),
    //  Description("Execute a query.")]
    // public async Task<string> QueryAsync(
    //     IMyService myService,
    //     [Description("The query text.")] string query,
    //     CancellationToken cancellationToken = default)
    // {
    //     var result = await myService.QueryAsync(query, cancellationToken);
    //     return JsonSerializer.Serialize(result);
    // }
}
