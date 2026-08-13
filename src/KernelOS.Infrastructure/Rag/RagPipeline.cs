using KernelOS.Core;
using KernelOS.Core.Context;
using KernelOS.Core.HybridSearch;
using KernelOS.Core.Rag;
using Microsoft.Extensions.Options;

namespace KernelOS.Infrastructure.Rag;

public sealed class RagPipeline : IRagPipeline
{
    private readonly IHybridSearchEngine hybridSearch;
    private readonly IContextBuilder contextBuilder;
    private readonly IRagPromptBuilder promptBuilder;
    private readonly IChatModel chatModel;
    private readonly RagOptions options;

    public RagPipeline(IHybridSearchEngine hybridSearch, IContextBuilder contextBuilder, IRagPromptBuilder promptBuilder, IChatModel chatModel, IOptions<RagOptions> options)
    {
        this.hybridSearch = hybridSearch; this.contextBuilder = contextBuilder; this.promptBuilder = promptBuilder; this.chatModel = chatModel; this.options = options.Value;
    }

    public async Task<RagResponse> AnswerAsync(RagRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return new(RagStatus.Cancelled);
        var topK = request.TopK ?? options.DefaultTopK;
        var maxTokens = request.MaxContextTokens ?? options.DefaultContextTokens;
        if (string.IsNullOrWhiteSpace(request.Query) || request.Query.Length > options.MaxQueryCharacters || topK <= 0 || topK > options.MaxTopK || maxTokens <= 0 || maxTokens > options.MaxContextTokens || request.MaxContextItems is <= 0 || request.MinimumHybridScore is < 0 or > 1)
            return new(RagStatus.InvalidRequest);

        var hybrid = await hybridSearch.SearchAsync(new(request.Query, topK, request.MinimumHybridScore), cancellationToken);
        if (hybrid.Status == HybridSearchStatus.Cancelled || cancellationToken.IsCancellationRequested) return new(RagStatus.Cancelled);
        if (hybrid.Status == HybridSearchStatus.NoResults) return NoContext(hybrid.Results?.Count ?? 0, null, null);
        if (hybrid.Status == HybridSearchStatus.ProviderUnavailable) return new(RagStatus.ProviderUnavailable);
        if (hybrid.Status == HybridSearchStatus.InvalidRequest) return new(RagStatus.InvalidRequest);
        if (hybrid.Status == HybridSearchStatus.Failed) return new(RagStatus.Failed);

        var warnings = Copy(hybrid.Warnings);
        if (hybrid.Status == HybridSearchStatus.PartialSuccess) warnings.Add(new("RAG_RETRIEVAL_PARTIAL", "Retrieval returned partial results."));
        var context = await contextBuilder.BuildAsync(new(hybrid.Results ?? [], maxTokens, request.MaxContextItems, request.MinimumHybridScore), cancellationToken);
        if (context.Status == ContextStatus.Cancelled || cancellationToken.IsCancellationRequested) return new(RagStatus.Cancelled);
        if (context.Status == ContextStatus.InvalidRequest) return new(RagStatus.InvalidRequest);
        if (context.Status == ContextStatus.Failed) return new(RagStatus.Failed);
        if (context.Status == ContextStatus.NoContext || context.Pack?.Items.Count is not > 0) return NoContext(hybrid.Results?.Count ?? 0, context.Pack, warnings);
        warnings.AddRange(Copy(context.Warnings));
        if (context.Status == ContextStatus.PartialSuccess) warnings.Add(new("RAG_CONTEXT_PARTIAL", "Context selection returned partial results."));
        if (context.Pack.Truncated) warnings.Add(new("RAG_CONTEXT_TRUNCATED", "Context selection reached a configured limit."));

        if (cancellationToken.IsCancellationRequested) return new(RagStatus.Cancelled);
        var chat = await chatModel.SendAsync(promptBuilder.Build(request.Query, context.Pack, request.History), cancellationToken);
        if (cancellationToken.IsCancellationRequested || string.Equals(chat.Error, "cancelled", StringComparison.Ordinal)) return new(RagStatus.Cancelled);
        if (!chat.Success) return new(RagStatus.Failed, Model: chat.Model);
        var citations = context.Pack.Citations.Select(citation => new RagCitation(citation.CitationId, citation.MemoryDocumentId, citation.MemoryItemId, citation.Source.SafeReference, citation.Source.DisplayReference)).ToArray();
        var status = warnings.Count == 0 ? RagStatus.Success : RagStatus.PartialSuccess;
        return new(status, chat.Message, citations, warnings.Count == 0 ? null : warnings, Info(hybrid.Results?.Count ?? 0, context.Pack), chat.Model);
    }

    private static RagResponse NoContext(int candidates, ContextPack? pack, List<RagWarning>? warnings) => new(RagStatus.NoContext, Context: pack is null ? new(candidates, 0, 0, false) : Info(candidates, pack), Warnings: warnings?.Count > 0 ? warnings : null);
    private static RagContextInfo Info(int candidates, ContextPack pack) => new(candidates, pack.Items.Count, pack.EstimatedTokens, pack.Truncated);
    private static List<RagWarning> Copy(IReadOnlyList<HybridSearchWarning>? warnings) => (warnings ?? []).Select(warning => new RagWarning(warning.Code, warning.Message)).ToList();
    private static IEnumerable<RagWarning> Copy(IReadOnlyList<ContextWarning>? warnings) => (warnings ?? []).Select(warning => new RagWarning(warning.Code, warning.Message));
}
