using System.Threading.Channels;
using PiiGateway.Core.Interfaces.Services;

namespace PiiGateway.Infrastructure.Services;

public class DocumentProcessingQueue : IDocumentProcessingQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(
        new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public async Task EnqueueAsync(Guid jobId)
    {
        await _channel.Writer.WriteAsync(jobId);
    }
}
