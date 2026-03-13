using System.Text.Json;
using Kusto.Cli.Mcp;

namespace Kusto.Cli.Tests;

public sealed class KustoToolsTests
{
    [Fact]
    public async Task QueryAsync_UsesDefaultClusterAndDatabase()
    {
        var config = new KustoConfig
        {
            Clusters =
            [
                new KnownCluster
                {
                    Name = "help",
                    Url = "https://help.kusto.windows.net"
                }
            ],
            DefaultClusterUrl = "https://help.kusto.windows.net",
            DefaultDatabases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["https://help.kusto.windows.net"] = "Samples"
            }
        };

        var fakeService = new TestRuntimeFactory.FakeKustoService
        {
            OnExecuteQueryAsync = (clusterUrl, database, query, includeStatistics, _) =>
            {
                Assert.Equal("https://help.kusto.windows.net", clusterUrl);
                Assert.Equal("Samples", database);
                Assert.Equal("StormEvents | take 1", query);
                Assert.True(includeStatistics);

                return Task.FromResult(new QueryExecutionResult(
                    new TabularData(["State"], [["WA"]]),
                    new QueryStatistics { ExecutionTimeSec = 0.25 }));
            }
        };

        using var runtime = TestRuntimeFactory.Create(config, fakeService);
        var tools = new KustoTools(runtime);

        var json = await tools.QueryAsync(
            query: "StormEvents | take 1",
            showStatistics: true);

        var output = JsonDocument.Parse(json).RootElement;
        Assert.Equal("State", output.GetProperty("table").GetProperty("columns")[0].GetString());
        Assert.Equal("WA", output.GetProperty("table").GetProperty("rows")[0][0].GetString());
        Assert.Equal(0.25, output.GetProperty("statistics").GetProperty("executionTimeSec").GetDouble());
    }

    [Fact]
    public async Task QueryAsync_ThrowsMcpExceptionForUserErrors()
    {
        using var runtime = TestRuntimeFactory.Create(new KustoConfig());
        var tools = new KustoTools(runtime);

        var ex = await Assert.ThrowsAsync<ModelContextProtocol.McpException>(
            () => tools.QueryAsync(query: "StormEvents | take 1"));

        Assert.Equal("No default cluster is configured. Set one with 'kusto cluster set-default <name|url>'.", ex.Message);
    }

    [Fact]
    public async Task ClusterListAsync_ReturnsConfiguredClusters()
    {
        var config = new KustoConfig
        {
            Clusters =
            [
                new KnownCluster { Name = "help", Url = "https://help.kusto.windows.net" }
            ],
            DefaultClusterUrl = "https://help.kusto.windows.net"
        };

        using var runtime = TestRuntimeFactory.Create(config);
        var tools = new KustoTools(runtime);

        var json = await tools.ClusterListAsync();

        var output = JsonDocument.Parse(json).RootElement;
        var rows = output.GetProperty("table").GetProperty("rows");
        Assert.Equal(1, rows.GetArrayLength());
        Assert.Equal("help", rows[0][0].GetString());
    }

    [Fact]
    public async Task ClusterGetAsync_ReturnsClusterDetails()
    {
        var config = new KustoConfig
        {
            Clusters =
            [
                new KnownCluster { Name = "help", Url = "https://help.kusto.windows.net" }
            ],
            DefaultClusterUrl = "https://help.kusto.windows.net"
        };

        using var runtime = TestRuntimeFactory.Create(config);
        var tools = new KustoTools(runtime);

        var json = await tools.ClusterGetAsync(cluster: "help");

        var output = JsonDocument.Parse(json).RootElement;
        Assert.Equal("https://help.kusto.windows.net", output.GetProperty("properties").GetProperty("Url").GetString());
    }
}
