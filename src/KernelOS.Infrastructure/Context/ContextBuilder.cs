using KernelOS.Core.Context;
using KernelOS.Core.Memory;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Context;

public sealed class ContextBuilder : IContextBuilder
{
    private readonly IMemoryStore memoryStore;
    private readonly IContextTokenEstimator tokenEstimator;
    private readonly ContextOptions options;

    public ContextBuilder(IMemoryStore memoryStore, IContextTokenEstimator tokenEstimator, IOptions<ContextOptions> options)
    {
        this.memoryStore = memoryStore;
        this.tokenEstimator = tokenEstimator;
        this.options = options.Value;
    }

    public async Task<ContextBuildResult> BuildAsync(ContextBuildRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(ContextStatus.Cancelled);

        var maxTokens = request.MaxTokens ?? options.DefaultMaxTokens;
        var maxItems = request.MaxItems ?? options.DefaultMaxItems;
        if (request.Results is null || maxTokens <= 0 || maxTokens > options.MaxAllowedTokens || maxItems <= 0 || maxItems > options.MaxAllowedItems || request.MinimumHybridScore is < 0 or > 1)
            return new(ContextStatus.InvalidRequest);

        var warnings = new List<ContextWarning>();
        var items = new List<ContextItem>();
        var citations = new List<ContextCitation>();
        var seenItemIds = new HashSet<Guid>();
        var estimatedTokens = 0;
        var truncated = false;

        var ranked = request.Results
            .Select((result, index) => new { Result = result, Index = index })
            .Where(entry => entry.Result.HybridScore >= request.MinimumHybridScore)
            .OrderByDescending(entry => entry.Result.HybridScore)
            .ThenBy(entry => entry.Index)
            .ThenBy(entry => entry.Result.MemoryId);

        try
        {
            foreach (var entry in ranked)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!seenItemIds.Add(entry.Result.MemoryId))
                {
                    warnings.Add(new("CONTEXT_DUPLICATE_ITEM_SKIPPED", "A duplicate context item was skipped."));
                    continue;
                }

                if (items.Count >= maxItems)
                {
                    truncated = true;
                    warnings.Add(new("CONTEXT_ITEM_LIMIT_REACHED", "The context item limit was reached."));
                    break;
                }

                var memory = await memoryStore.QueryAsync(new(MemoryItemId: entry.Result.MemoryId, Limit: 1), cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                if (memory.Status == MemoryStatus.Cancelled) return new(ContextStatus.Cancelled);
                if (memory.Status != MemoryStatus.Success) return new(ContextStatus.Failed, Error: "Context memory resolution failed.");

                var document = memory.Documents?.SingleOrDefault();
                var memoryItem = document?.Items.SingleOrDefault(item => item.Id == entry.Result.MemoryId);
                if (document is null || memoryItem is null)
                {
                    truncated = true;
                    warnings.Add(new("CONTEXT_ITEM_NOT_FOUND", "A referenced context item was not found."));
                    continue;
                }

                var itemTokens = tokenEstimator.Estimate(memoryItem.Content);
                cancellationToken.ThrowIfCancellationRequested();
                if (itemTokens > maxTokens - estimatedTokens)
                {
                    truncated = true;
                    warnings.Add(new("CONTEXT_TOKEN_BUDGET_REACHED", "The context token budget was reached."));
                    break;
                }

                var citationId = $"C{items.Count + 1}";
                items.Add(new(document.Id, memoryItem.Id, memoryItem.Content, entry.Result.HybridScore, memoryItem.Source, items.Count, itemTokens, citationId));
                citations.Add(new(citationId, document.Id, memoryItem.Id, memoryItem.Source));
                estimatedTokens += itemTokens;
            }
        }
        catch (OperationCanceledException) { return new(ContextStatus.Cancelled); }
        catch { return new(ContextStatus.Failed, Error: "Context building failed."); }

        if (cancellationToken.IsCancellationRequested) return new(ContextStatus.Cancelled);
        var pack = new ContextPack(items, citations, estimatedTokens, maxTokens, truncated, warnings.Count == 0 ? null : warnings);
        var status = items.Count == 0 ? ContextStatus.NoContext : warnings.Count == 0 ? ContextStatus.Success : ContextStatus.PartialSuccess;
        return new(status, pack, warnings.Count == 0 ? null : warnings);
    }
}
