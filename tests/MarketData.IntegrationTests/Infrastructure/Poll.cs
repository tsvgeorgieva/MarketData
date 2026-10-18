namespace MarketData.IntegrationTests.Infrastructure;

public static class Poll
{
    /// <summary>Waits for work done in the background until the condition holds, failing the test after the timeout.</summary>
    public static async Task UntilAsync(Func<Task<bool>> condition, string because, int timeoutSeconds = 20)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                Assert.Fail($"Timed out waiting for: {because}");
            }

            await Task.Delay(100, TestContext.Current.CancellationToken);
        }
    }
}
