using System.Collections.Concurrent;
using LocalRagLab.Api.Domain;
using LocalRagLab.Api.Options;
using Microsoft.Extensions.Options;

namespace LocalRagLab.Api.Services;

public interface IRagTraceStore
{
    void Add(RagTraceRecord trace);
    RagTraceRecord? Get(string traceId);
    IReadOnlyList<RagTraceRecord> GetRecent(int count);
    void Clear();
}

public sealed class InMemoryRagTraceStore : IRagTraceStore
{
    private readonly ConcurrentDictionary<string, RagTraceRecord> _traces = new();
    private readonly ConcurrentQueue<string> _order = new();
    private readonly int _capacity;

    public InMemoryRagTraceStore(IOptions<RagOptions> options)
    {
        _capacity = Math.Max(10, options.Value.TraceCapacity);
    }

    public void Add(RagTraceRecord trace)
    {
        _traces[trace.TraceId] = trace;
        _order.Enqueue(trace.TraceId);

        while (_traces.Count > _capacity && _order.TryDequeue(out var oldestTraceId))
        {
            _traces.TryRemove(oldestTraceId, out _);
        }
    }

    public RagTraceRecord? Get(string traceId) =>
        _traces.TryGetValue(traceId, out var trace) ? trace : null;

    public IReadOnlyList<RagTraceRecord> GetRecent(int count) =>
        _traces.Values
            .OrderByDescending(trace => trace.StartedAt)
            .Take(Math.Clamp(count, 1, _capacity))
            .ToArray();

    public void Clear()
    {
        _traces.Clear();
        while (_order.TryDequeue(out _))
        {
        }
    }
}
