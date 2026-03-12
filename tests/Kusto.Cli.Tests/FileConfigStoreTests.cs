namespace Kusto.Cli.Tests;

public sealed class FileConfigStoreTests
{
    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripsRequestTimeoutMinutes()
    {
        var tempDirectory = Path.Combine(Path.GetTempPath(), "kusto-cli-tests", Guid.NewGuid().ToString("N"));
        var configPath = Path.Combine(tempDirectory, "config.json");
        var store = new FileConfigStore(configPath);

        try
        {
            await store.SaveAsync(
                new KustoConfig
                {
                    RequestTimeoutMinutes = 7
                },
                CancellationToken.None);

            var config = await store.LoadAsync(CancellationToken.None);
            Assert.Equal(7, config.RequestTimeoutMinutes);
        }
        finally
        {
            if (Directory.Exists(tempDirectory))
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
        }
    }
}
