using System.Threading.Channels;

namespace ProductStoreAPI.Services;

public class ScanQueue
  {
      private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();
      public ValueTask EnqueueAsync(Guid productId) => _channel.Writer.WriteAsync(productId);
      public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
  }