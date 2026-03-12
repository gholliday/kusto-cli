using System.Globalization;
using Microsoft.Extensions.Logging;

namespace Kusto.Cli;

public static class CliRunner
{
    internal const int DefaultRequestTimeoutMinutes = 5;
    internal const string TimeoutEnvironmentVariableName = "KUSTO_TIMEOUT_MINUTES";
    internal const string TimeoutConfigPropertyName = "requestTimeoutMinutes";

    public static async Task<int> RunAsync(
        string formatToken,
        string? logLevelToken,
        int? timeoutMinutes,
        Func<CliRuntime, CancellationToken, Task<CliOutput>> commandAction,
        CancellationToken cancellationToken)
    {
        OutputFormat format;
        LogLevel? logLevel;

        try
        {
            format = ParseOutputFormatToken(formatToken);
            logLevel = ParseLogLevelToken(logLevelToken);
        }
        catch (Exception ex)
        {
            ConsoleOutput.WriteError(ErrorMapper.Map(ex));
            return 1;
        }

        using var runtime = CreateRuntime(logLevel);

        try
        {
            var config = await runtime.ConfigStore.LoadAsync(cancellationToken);
            runtime.HttpClient.Timeout = ResolveRequestTimeout(timeoutMinutes, config);

            var output = await commandAction(runtime, cancellationToken);
            var renderedOutput = runtime.OutputFormatter.Format(output, format);
            if (!string.IsNullOrWhiteSpace(renderedOutput))
            {
                Console.Out.WriteLine(renderedOutput);
            }

            return 0;
        }
        catch (Exception ex)
        {
            runtime.Logger.LogError(ex, "Command execution failed.");
            ConsoleOutput.WriteError(ErrorMapper.Map(ex));
            return 1;
        }
    }

    public static OutputFormat ParseOutputFormatToken(string formatToken)
    {
        return formatToken.ToLowerInvariant() switch
        {
            "human" => OutputFormat.Human,
            "json" => OutputFormat.Json,
            "markdown" => OutputFormat.Markdown,
            "md" => OutputFormat.Markdown,
            _ => throw new UserFacingException($"'{formatToken}' is not a valid output format. Use one of: human, json, markdown, md.")
        };
    }

    public static LogLevel? ParseLogLevelToken(string? logLevelToken)
    {
        if (string.IsNullOrWhiteSpace(logLevelToken))
        {
            return null;
        }

        if (Enum.TryParse<LogLevel>(logLevelToken, true, out var parsed))
        {
            return parsed;
        }

        throw new UserFacingException(
            $"'{logLevelToken}' is not a valid log level. Use Trace, Debug, Information, Warning, Error, Critical, or None.");
    }

    internal static TimeSpan ResolveRequestTimeout(int? timeoutMinutes, KustoConfig config)
    {
        return ResolveRequestTimeout(timeoutMinutes, config, Environment.GetEnvironmentVariable(TimeoutEnvironmentVariableName));
    }

    internal static TimeSpan ResolveRequestTimeout(int? timeoutMinutes, KustoConfig config, string? environmentTimeoutValue)
    {
        if (timeoutMinutes is int optionMinutes)
        {
            return TimeSpan.FromMinutes(ValidateTimeoutMinutes(optionMinutes, "The --timeout option"));
        }

        if (!string.IsNullOrWhiteSpace(environmentTimeoutValue))
        {
            return TimeSpan.FromMinutes(ParseTimeoutMinutes(environmentTimeoutValue, $"Environment variable '{TimeoutEnvironmentVariableName}'"));
        }

        if (config.RequestTimeoutMinutes is int configuredMinutes)
        {
            return TimeSpan.FromMinutes(ValidateTimeoutMinutes(configuredMinutes, $"Config property '{TimeoutConfigPropertyName}'"));
        }

        return TimeSpan.FromMinutes(DefaultRequestTimeoutMinutes);
    }

    public static CliRuntime CreateRuntime(LogLevel? requestedLogLevel, string? configPath = null, TextWriter? stderrWriter = null, string? logFilePath = null)
    {
        var loggerFactory = LoggingFactoryBuilder.Create(requestedLogLevel, logFilePath, stderrWriter);
        var logger = loggerFactory.CreateLogger("kusto");
        var configStore = new FileConfigStore(configPath);
        var connectionResolver = new KustoConnectionResolver();
        var tokenProvider = new AzureTokenProvider();
        var httpClient = KustoHttpClientFactory.Create();
        var kustoService = new KustoHttpService(httpClient, tokenProvider, loggerFactory.CreateLogger<KustoHttpService>());
        var tableSchemaProvider = new TableSchemaProvider(
            kustoService,
            new SchemaCacheSettingsResolver(),
            loggerFactory.CreateLogger<TableSchemaProvider>());
        var formatter = new OutputFormatter();

        return new CliRuntime(
            loggerFactory,
            logger,
            httpClient,
            configStore,
            connectionResolver,
            kustoService,
            tableSchemaProvider,
            formatter);
    }

    private static int ParseTimeoutMinutes(string value, string sourceDescription)
    {
        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMinutes))
        {
            throw new UserFacingException($"{sourceDescription} must be a positive whole number of minutes.");
        }

        return ValidateTimeoutMinutes(parsedMinutes, sourceDescription);
    }

    private static int ValidateTimeoutMinutes(int timeoutMinutes, string sourceDescription)
    {
        if (timeoutMinutes <= 0)
        {
            throw new UserFacingException($"{sourceDescription} must be a positive whole number of minutes.");
        }

        return timeoutMinutes;
    }
}
