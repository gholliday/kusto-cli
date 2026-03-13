using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace Kusto.Cli.Mcp;

[McpServerToolType]
public sealed class KustoTools(CliRuntime runtime)
{
    private readonly CliRuntime _runtime = runtime;

    [McpServerTool(Name = "kusto_query", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("Execute a KQL query against the selected cluster and database.")]
    public async Task<string> QueryAsync(
        [Description("The KQL query text to execute.")] string query,
        [Description("Cluster name or URL. If omitted, the default cluster is used.")] string? cluster = null,
        [Description("Database name. If omitted, the default database for the selected cluster is used.")] string? database = null,
        [Description("Include query execution statistics when Kusto returns them.")] bool showStatistics = false,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var resolvedCluster = _runtime.ConnectionResolver.ResolveCluster(config, cluster);
            var resolvedDatabase = _runtime.ConnectionResolver.ResolveDatabase(config, resolvedCluster.Url, database);
            var result = await _runtime.KustoService.ExecuteQueryAsync(
                resolvedCluster.Url, resolvedDatabase, query, showStatistics, cancellationToken);

            return new CliOutput
            {
                Table = result.Table,
                Statistics = result.Statistics,
                IsQueryResultTable = true
            };
        });
    }

    [McpServerTool(Name = "kusto_command", Destructive = true, OpenWorld = false),
     Description("Execute a Kusto management command against the selected cluster and database.")]
    public async Task<string> CommandAsync(
        [Description("The Kusto management command to execute.")] string command,
        [Description("Cluster name or URL. If omitted, the default cluster is used.")] string? cluster = null,
        [Description("Database name. If omitted, the default database for the selected cluster is used.")] string? database = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var resolvedCluster = _runtime.ConnectionResolver.ResolveCluster(config, cluster);
            var resolvedDatabase = string.IsNullOrWhiteSpace(database)
                ? config.DefaultDatabases.GetValueOrDefault(resolvedCluster.Url)
                : database;
            var result = await _runtime.KustoService.ExecuteManagementCommandAsync(
                resolvedCluster.Url, resolvedDatabase, command, queryParameters: null, cancellationToken);

            return new CliOutput { Table = result };
        });
    }

    [McpServerTool(Name = "kusto_cluster_list", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("List locally configured Kusto clusters and defaults.")]
    public async Task<string> ClusterListAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            if (config.Clusters.Count == 0)
            {
                return new CliOutput { Message = "No known clusters. Add one with: kusto cluster add <name> <url>" };
            }

            var rows = new List<IReadOnlyList<string?>>();
            foreach (var c in config.Clusters.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
            {
                config.DefaultDatabases.TryGetValue(c.Url, out var defaultDatabase);
                rows.Add(
                [
                    c.Name,
                    c.Url,
                    string.Equals(config.DefaultClusterUrl, c.Url, StringComparison.OrdinalIgnoreCase) ? "*" : string.Empty,
                    defaultDatabase
                ]);
            }

            return new CliOutput { Table = new TabularData(["Name", "Url", "Default", "DefaultDatabase"], rows) };
        });
    }

    [McpServerTool(Name = "kusto_cluster_get", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("Show details for one locally configured Kusto cluster.")]
    public async Task<string> ClusterGetAsync(
        [Description("Cluster name or URL.")] string cluster,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var found = ClusterUtilities.FindKnownCluster(config, cluster)
                ?? throw new UserFacingException($"Cluster '{cluster}' is not known.");

            var normalizedUrl = ClusterUtilities.NormalizeClusterUrl(found.Url);
            config.DefaultDatabases.TryGetValue(normalizedUrl, out var defaultDatabase);
            return new CliOutput
            {
                Properties = new Dictionary<string, string?>
                {
                    ["Name"] = found.Name,
                    ["Url"] = normalizedUrl,
                    ["Default"] = string.Equals(config.DefaultClusterUrl, normalizedUrl, StringComparison.OrdinalIgnoreCase) ? "true" : "false",
                    ["DefaultDatabase"] = defaultDatabase
                }
            };
        });
    }

    [McpServerTool(Name = "kusto_database_list", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("List databases in the selected cluster, with optional name filtering and row limiting.")]
    public async Task<string> DatabaseListAsync(
        [Description("Cluster name or URL. If omitted, the default cluster is used.")] string? cluster = null,
        [Description("Optional database name filter. Supports plain text, ^prefix, suffix$, or ^exact$.")] string? filter = null,
        [Description("Optional positive limit for the number of databases returned.")] int? take = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var resolvedCluster = _runtime.ConnectionResolver.ResolveCluster(config, cluster);
            var query = ListQueryBuilder.Build(".show databases | project DatabaseName", "DatabaseName", filter, take);
            var databases = await _runtime.KustoService.ExecuteManagementCommandAsync(
                resolvedCluster.Url, database: null, query.Command, query.Parameters, cancellationToken);

            var rows = new List<IReadOnlyList<string?>>();
            var nameColumnIndex = TabularDataUtilities.GetPreferredColumnIndex(databases, "DatabaseName");
            config.DefaultDatabases.TryGetValue(resolvedCluster.Url, out var defaultDatabase);
            foreach (var row in databases.Rows)
            {
                var databaseName = nameColumnIndex >= 0 && row.Count > nameColumnIndex ? row[nameColumnIndex] : string.Empty;
                rows.Add([databaseName, string.Equals(databaseName, defaultDatabase, StringComparison.OrdinalIgnoreCase) ? "*" : string.Empty]);
            }

            return new CliOutput { Table = new TabularData(["Database", "Default"], rows) };
        });
    }

    [McpServerTool(Name = "kusto_table_list", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("List tables in the selected database, with optional name filtering and row limiting.")]
    public async Task<string> TableListAsync(
        [Description("Cluster name or URL. If omitted, the default cluster is used.")] string? cluster = null,
        [Description("Database name. If omitted, the default database for the selected cluster is used.")] string? database = null,
        [Description("Optional table name filter. Supports plain text, ^prefix, suffix$, or ^exact$.")] string? filter = null,
        [Description("Optional positive limit for the number of tables returned.")] int? take = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var resolvedCluster = _runtime.ConnectionResolver.ResolveCluster(config, cluster);
            var resolvedDatabase = _runtime.ConnectionResolver.ResolveDatabase(config, resolvedCluster.Url, database);
            var query = ListQueryBuilder.Build(".show tables | project TableName", "TableName", filter, take);
            var result = await _runtime.KustoService.ExecuteManagementCommandAsync(
                resolvedCluster.Url, resolvedDatabase, query.Command, query.Parameters, cancellationToken);

            return new CliOutput { Table = result, IsQueryResultTable = true };
        });
    }

    [McpServerTool(Name = "kusto_table_schema", ReadOnly = true, Idempotent = true, OpenWorld = false),
     Description("Show schema details for one table in the selected database.")]
    public async Task<string> TableSchemaAsync(
        [Description("Table name.")] string table,
        [Description("Cluster name or URL. If omitted, the default cluster is used.")] string? cluster = null,
        [Description("Database name. If omitted, the default database for the selected cluster is used.")] string? database = null,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(async () =>
        {
            var config = await _runtime.ConfigStore.LoadAsync(cancellationToken);
            var resolvedCluster = _runtime.ConnectionResolver.ResolveCluster(config, cluster);
            var resolvedDatabase = _runtime.ConnectionResolver.ResolveDatabase(config, resolvedCluster.Url, database);
            var command = $".show table ['{EscapeKustoLiteral(table)}'] schema as json";
            var result = await _runtime.KustoService.ExecuteManagementCommandAsync(
                resolvedCluster.Url, resolvedDatabase, command, queryParameters: null, cancellationToken);

            if (result.Rows.Count == 0)
            {
                throw new UserFacingException($"Table '{table}' was not found.");
            }

            return new CliOutput { Properties = TabularDataUtilities.ConvertRowToProperties(result, 0) };
        });
    }

    private static async Task<string> ExecuteAsync(Func<Task<CliOutput>> action)
    {
        try
        {
            var output = await action();
            return JsonSerializer.Serialize(output, KustoToolsJsonContext.Default.CliOutput);
        }
        catch (UserFacingException ex)
        {
            throw new McpException(ex.Message, ex);
        }
    }

    private static string EscapeKustoLiteral(string input)
    {
        return input.Replace("'", "''", StringComparison.Ordinal);
    }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    PropertyNamingPolicy = System.Text.Json.Serialization.JsonKnownNamingPolicy.CamelCase)]
[System.Text.Json.Serialization.JsonSerializable(typeof(CliOutput))]
internal sealed partial class KustoToolsJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
