using System.Text.Json;
using System.Text.Json.Nodes;

namespace McpToolName.Mcp;

public static class McpSetup
{
    private const string ServerName = "toolservername";

    public static void PrintSetupInstructions()
    {
        var exePath = ResolveServerCommand();
        var useAnsi = Environment.UserInteractive && !Console.IsOutputRedirected;

        Console.WriteLine($"{ServerName} setup instructions");
        Console.WriteLine(new string('=', $"{ServerName} setup instructions".Length));
        Console.WriteLine();
        Console.WriteLine($"Run '{ServerName} install' to automatically add the server to your");
        Console.WriteLine("GitHub Copilot CLI configuration, or copy one of the snippets below.");
        Console.WriteLine();

        PrintCopilotCliSnippet(exePath, useAnsi);
        PrintVsCodeSnippet(exePath, useAnsi);
        PrintClaudeDesktopSnippet(exePath, useAnsi);
        PrintVisualStudioSnippet(exePath, useAnsi);
    }

    public static int InstallToConfig()
    {
        var configPath = GetCopilotConfigPath();
        var exePath = ResolveServerCommand();
        var useAnsi = Environment.UserInteractive && !Console.IsOutputRedirected;

        try
        {
            JsonObject root;
            if (File.Exists(configPath))
            {
                var existingJson = File.ReadAllText(configPath);
                root = JsonNode.Parse(existingJson, documentOptions: new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip })?.AsObject()
                    ?? new JsonObject();
            }
            else
            {
                root = new JsonObject();
            }

            var servers = root["mcpServers"]?.AsObject();
            if (servers is null)
            {
                servers = new JsonObject();
                root["mcpServers"] = servers;
            }

            if (servers.ContainsKey(ServerName))
            {
                Console.WriteLine($"'{ServerName}' already exists in {FileLink(configPath, useAnsi)}.");
                Console.WriteLine("Remove it first if you want to reinstall.");
                return 1;
            }

            var serverEntry = new JsonObject
            {
                ["type"] = "local",
                ["command"] = exePath.Command,
                ["args"] = new JsonArray(exePath.Args.Select(a => JsonValue.Create(a)).ToArray<JsonNode>()),
                ["tools"] = new JsonArray(JsonValue.Create("*"))
            };

            servers.Add(ServerName, serverEntry);

            var directory = Path.GetDirectoryName(configPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(configPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
            Console.WriteLine($"Added '{ServerName}' to {FileLink(configPath, useAnsi)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to update {configPath}: {ex.Message}");
            return 1;
        }
    }

    private static void PrintCopilotCliSnippet(ServerCommand cmd, bool useAnsi)
    {
        var configPath = GetCopilotConfigPath();
        Console.WriteLine($"GitHub Copilot CLI — {FileLink(configPath, useAnsi)}");
        Console.WriteLine("  User-level config shared by Copilot CLI sessions.");
        Console.WriteLine($$"""
{
  "mcpServers": {
    "toolservername": {
      "type": "local",
      "command": "{{cmd.Command}}",
      "args": [{{FormatArgs(cmd.Args)}}],
      "tools": ["*"]
    }
  }
}
""");
        Console.WriteLine();
    }

    private static void PrintVsCodeSnippet(ServerCommand cmd, bool useAnsi)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ".vscode", "mcp.json");
        Console.WriteLine($"VS Code — {FileLink(configPath, useAnsi)}");
        Console.WriteLine("  .vscode/mcp.json in repository root (workspace-level).");
        Console.WriteLine($$"""
{
  "servers": {
    "toolservername": {
      "command": "{{cmd.Command}}",
      "args": [{{FormatArgs(cmd.Args)}}]
    }
  }
}
""");
        Console.WriteLine();
    }

    private static void PrintClaudeDesktopSnippet(ServerCommand cmd, bool useAnsi)
    {
        var configPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Claude", "claude_desktop_config.json");
        Console.WriteLine($"Claude Desktop — {FileLink(configPath, useAnsi)}");
        Console.WriteLine("  User-level config for Claude Desktop app.");
        Console.WriteLine($$"""
{
  "mcpServers": {
    "toolservername": {
      "command": "{{cmd.Command}}",
      "args": [{{FormatArgs(cmd.Args)}}]
    }
  }
}
""");
        Console.WriteLine();
    }

    private static void PrintVisualStudioSnippet(ServerCommand cmd, bool useAnsi)
    {
        var configPath = Path.Combine(Directory.GetCurrentDirectory(), ".mcp.json");
        Console.WriteLine($"Visual Studio — {FileLink(configPath, useAnsi)}");
        Console.WriteLine("  .mcp.json at solution or repository root.");
        Console.WriteLine($$"""
{
  "mcpServers": {
    "toolservername": {
      "type": "stdio",
      "command": "{{cmd.Command}}",
      "args": [{{FormatArgs(cmd.Args)}}]
    }
  }
}
""");
        Console.WriteLine();
    }

    private static string FileLink(string filePath, bool useAnsi)
    {
        var fullPath = Path.GetFullPath(filePath);
        if (!useAnsi)
        {
            return fullPath;
        }

        var uri = new Uri(fullPath).AbsoluteUri;
        return $"\u001b]8;;{uri}\u001b\\{fullPath}\u001b]8;;\u001b\\";
    }

    private static ServerCommand ResolveServerCommand()
    {
        var currentExePath = Environment.ProcessPath;

        if (currentExePath is not null
            && !currentExePath.Contains("dotnet", StringComparison.OrdinalIgnoreCase)
            && File.Exists(currentExePath))
        {
            var normalized = currentExePath.Replace('\\', '/');
            return new ServerCommand(normalized, []);
        }

        return new ServerCommand("dotnet", ["run", "--project", FindProjectPath()]);
    }

    private static string FindProjectPath()
    {
        var baseDir = AppContext.BaseDirectory;
        if (baseDir is not null)
        {
            var dir = new DirectoryInfo(baseDir);
            while (dir is not null)
            {
                var csproj = Path.Combine(dir.FullName, "McpToolName.Mcp.csproj");
                if (File.Exists(csproj))
                {
                    return dir.FullName.Replace('\\', '/');
                }

                dir = dir.Parent;
            }
        }

        return $"./src/McpToolName.Mcp";
    }

    private static string GetCopilotConfigPath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".copilot", "mcp-config.json");
    }

    private static string FormatArgs(string[] args)
    {
        return string.Join(", ", args.Select(a => $"\"{a}\""));
    }

    private sealed record ServerCommand(string Command, string[] Args);
}
