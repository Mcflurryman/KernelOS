using System.Threading.Channels;
using KernelOS.Core.Memory;
using KernelOS.Core.SemanticIndex;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.SemanticIndex;

public sealed record QueuedMemoryMutation(long Generation, MemoryMutationCommitted Mutation);

public sealed class SemanticMutationBuffer : IMemoryMutationObserver
{
    private readonly object gate = new();
    private readonly Channel<QueuedMemoryMutation> channel;
    private readonly ISemanticIndexCoordinator coordinator;
    private readonly ILogger<SemanticMutationBuffer> logger;

    public SemanticMutationBuffer(ISemanticIndexCoordinator coordinator, IOptions<SemanticIndexMaintenanceOptions> options, ILogger<SemanticMutationBuffer> logger)
    {
        this.coordinator = coordinator;
        this.logger = logger;
        channel = Channel.CreateBounded<QueuedMemoryMutation>(new BoundedChannelOptions(options.Value.QueueCapacity) { FullMode = BoundedChannelFullMode.Wait, SingleReader = true, SingleWriter = false });
    }

    public ChannelReader<QueuedMemoryMutation> Reader => channel.Reader;

    public Task ObserveAsync(MemoryMutationCommitted mutation, CancellationToken cancellationToken = default)
    {
        try
        {
            lock (gate)
            {
                var generation = coordinator.RegisterMutation();
                if (!channel.Writer.TryWrite(new(generation, mutation)))
                {
                    coordinator.MarkDirty();
                    SemanticMutationBufferLog.Full(logger, generation, mutation.Type);
                }
            }
        }
        catch { coordinator.MarkDirty(); SemanticMutationBufferLog.Failed(logger, mutation.Type); }
        return Task.CompletedTask;
    }
}

internal static partial class SemanticMutationBufferLog
{
    [LoggerMessage(EventId = 80, Level = LogLevel.Warning, Message = "Semantic maintenance queue is full at generation {Generation} for {MutationType}; a rebuild is required.")]
    internal static partial void Full(ILogger logger, long generation, MemoryMutationType mutationType);
    [LoggerMessage(EventId = 82, Level = LogLevel.Warning, Message = "Semantic maintenance mutation registration failed for {MutationType}; a rebuild is required.")]
    internal static partial void Failed(ILogger logger, MemoryMutationType mutationType);
}
