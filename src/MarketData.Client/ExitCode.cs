namespace MarketData.Client;

/// <summary>What the process returns to the shell.</summary>
internal static class ExitCode
{
    public const int Ok = 0;
    public const int ApiError = 1;
    public const int BadUsage = 2;
    public const int Unreachable = 3;
}
