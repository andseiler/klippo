using System.Collections.Concurrent;

namespace PiiGateway.Infrastructure.Services;

public class JobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _tokens = new();

    public CancellationToken Register(Guid jobId)
    {
        var cts = new CancellationTokenSource();
        _tokens[jobId] = cts;
        return cts.Token;
    }

    public bool Cancel(Guid jobId)
    {
        if (_tokens.TryGetValue(jobId, out var cts))
        {
            cts.Cancel();
            return true;
        }
        return false;
    }

    public void Remove(Guid jobId)
    {
        if (_tokens.TryRemove(jobId, out var cts))
        {
            cts.Dispose();
        }
    }
}
