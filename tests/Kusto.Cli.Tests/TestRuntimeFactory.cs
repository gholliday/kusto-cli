using Microsoft.Extensions.Logging;

namespace Kusto.Cli.Tests;

internal static class TestRuntimeFactory
{
    public static CliRuntime Create(KustoConfig config, IKustoService? kustoService = null)
    {
        var loggerFactory = LoggerFactory.Create(_ => { });
        return new CliRuntime(
            loggerFactory,
            loggerFactory.CreateLogger("test"),
            new HttpClient(),
            new InMemoryConfigStore(config),
            new KustoConnectionResolver(),
            kustoService ?? new FakeKustoService(),
            new OutputFormatter());
    }

    internal sealed class InMemoryConfigStore(KustoConfig config) : IConfigStore
    {
        private KustoConfig _config = config;

        public Task<KustoConfig> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_config);
        }

        public Task SaveAsync(KustoConfig config, CancellationToken cancellationToken)
        {
            _config = config;
            return Task.CompletedTask;
        }
    }

    internal sealed class FakeKustoService : IKustoService
    {
        public Func<string, string?, string, IReadOnlyDictionary<string, string>?, CancellationToken, Task<TabularData>>? OnExecuteManagementCommandAsync { get; init; }
        public Func<string, string, string, bool, CancellationToken, Task<QueryExecutionResult>>? OnExecuteQueryAsync { get; init; }

        public Task<TabularData> ExecuteManagementCommandAsync(string clusterUrl, string? database, string command, IReadOnlyDictionary<string, string>? queryParameters, CancellationToken cancellationToken)
        {
            return OnExecuteManagementCommandAsync is null
                ? Task.FromResult(TabularData.Empty)
                : OnExecuteManagementCommandAsync(clusterUrl, database, command, queryParameters, cancellationToken);
        }

        public Task<QueryExecutionResult> ExecuteQueryAsync(string clusterUrl, string database, string query, bool includeStatistics, CancellationToken cancellationToken)
        {
            return OnExecuteQueryAsync is null
                ? Task.FromResult(new QueryExecutionResult(TabularData.Empty, null))
                : OnExecuteQueryAsync(clusterUrl, database, query, includeStatistics, cancellationToken);
        }
    }
}
