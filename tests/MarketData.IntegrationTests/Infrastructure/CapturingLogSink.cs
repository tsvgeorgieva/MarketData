using System.Collections.Concurrent;
using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace MarketData.IntegrationTests.Infrastructure;

/// <summary>Collects the rendered text of every log event the host writes.</summary>
public sealed class CapturingLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> _messages = new();

    public IReadOnlyCollection<string> Messages => _messages;

    public void Emit(LogEvent logEvent) => _messages.Enqueue(logEvent.RenderMessage(CultureInfo.InvariantCulture) + logEvent.Exception);
}
