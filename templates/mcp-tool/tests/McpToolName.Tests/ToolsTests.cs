using McpToolName.Mcp;

namespace McpToolName.Tests;

public sealed class ToolsTests
{
    [Fact]
    public void Hello_ReturnsGreeting()
    {
        var tools = new Tools();
        var result = Tools.Hello("World");
        Assert.Equal("Hello, World!", result);
    }
}
