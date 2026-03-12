namespace Kusto.Cli.Tests;

public sealed class KustoHttpClientFactoryTests
{
    [Fact]
    public void Create_UsesFiveMinuteDefaultTimeout()
    {
        using var client = KustoHttpClientFactory.Create();
        Assert.Equal(TimeSpan.FromMinutes(CliRunner.DefaultRequestTimeoutMinutes), client.Timeout);
    }

    [Fact]
    public void CreateHandler_ConfiguresConnectCallback()
    {
        using var handler = KustoHttpClientFactory.CreateHandler();
        Assert.NotNull(handler.ConnectCallback);
    }

    [Fact]
    public void KeepAliveSettings_MatchExpectedValues()
    {
        Assert.Equal(60, KustoHttpClientFactory.KeepAliveIdleSeconds);
        Assert.Equal(30, KustoHttpClientFactory.KeepAliveIntervalSeconds);
        Assert.Equal(5, KustoHttpClientFactory.KeepAliveRetryCount);
    }
}
