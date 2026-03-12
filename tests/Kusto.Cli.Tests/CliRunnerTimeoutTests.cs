namespace Kusto.Cli.Tests;

public sealed class CliRunnerTimeoutTests
{
    [Fact]
    public void ResolveRequestTimeout_UsesDefaultWhenNoOverridesExist()
    {
        var timeout = CliRunner.ResolveRequestTimeout(null, new KustoConfig(), null);
        Assert.Equal(TimeSpan.FromMinutes(CliRunner.DefaultRequestTimeoutMinutes), timeout);
    }

    [Fact]
    public void ResolveRequestTimeout_PrefersOptionOverEnvironmentAndConfig()
    {
        var config = new KustoConfig
        {
            RequestTimeoutMinutes = 9
        };

        var timeout = CliRunner.ResolveRequestTimeout(3, config, "7");
        Assert.Equal(TimeSpan.FromMinutes(3), timeout);
    }

    [Fact]
    public void ResolveRequestTimeout_UsesEnvironmentWhenOptionIsMissing()
    {
        var config = new KustoConfig
        {
            RequestTimeoutMinutes = 9
        };

        var timeout = CliRunner.ResolveRequestTimeout(null, config, "7");
        Assert.Equal(TimeSpan.FromMinutes(7), timeout);
    }

    [Fact]
    public void ResolveRequestTimeout_UsesConfigWhenNoHigherPriorityOverrideExists()
    {
        var config = new KustoConfig
        {
            RequestTimeoutMinutes = 9
        };

        var timeout = CliRunner.ResolveRequestTimeout(null, config, null);
        Assert.Equal(TimeSpan.FromMinutes(9), timeout);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveRequestTimeout_RejectsNonPositiveOptionValues(int value)
    {
        var exception = Assert.Throws<UserFacingException>(() => CliRunner.ResolveRequestTimeout(value, new KustoConfig(), null));
        Assert.Contains("--timeout", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveRequestTimeout_RejectsInvalidEnvironmentValue()
    {
        var exception = Assert.Throws<UserFacingException>(() => CliRunner.ResolveRequestTimeout(null, new KustoConfig(), "abc"));
        Assert.Contains(CliRunner.TimeoutEnvironmentVariableName, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ResolveRequestTimeout_RejectsNonPositiveConfigValues(int value)
    {
        var config = new KustoConfig
        {
            RequestTimeoutMinutes = value
        };

        var exception = Assert.Throws<UserFacingException>(() => CliRunner.ResolveRequestTimeout(null, config, null));
        Assert.Contains(CliRunner.TimeoutConfigPropertyName, exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
