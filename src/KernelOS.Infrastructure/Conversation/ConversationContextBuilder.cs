using KernelOS.Core.Context;
using KernelOS.Core.Conversation;
using Microsoft.Extensions.Options;
namespace KernelOS.Infrastructure.Conversation;
public sealed class ConversationContextBuilder(IContextTokenEstimator tokenEstimator, IOptions<ConversationContextOptions> options) : IConversationContextBuilder
{
    private readonly ConversationContextOptions options = options.Value;
    public Task<ConversationContextResult> BuildAsync(ConversationContextRequest request, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested) return Task.FromResult(new ConversationContextResult(ConversationContextStatus.Cancelled));
        var maxTokens=request.MaxTokens ?? options.DefaultMaxTokens; var maxTurns=request.MaxTurns ?? options.DefaultMaxTurns;
        if (request.History is null || (request.CurrentUserMessage is not null && string.IsNullOrWhiteSpace(request.CurrentUserMessage)) || maxTokens<=0 || maxTokens>options.MaxAllowedTokens || maxTurns<=0 || maxTurns>options.MaxAllowedTurns || request.History.Any(turn => turn.Id==Guid.Empty || !Enum.IsDefined(turn.Role) || string.IsNullOrWhiteSpace(turn.Content)) || request.History.Select(turn=>turn.Id).Distinct().Count()!=request.History.Count) return Task.FromResult(new ConversationContextResult(ConversationContextStatus.InvalidRequest));
        if (request.History.Count==0) return Task.FromResult(new ConversationContextResult(ConversationContextStatus.NoContext, new ConversationContextPack([],0,maxTokens,false)));
        try { var selected=new List<ConversationContextItem>(); var tokens=0; var warnings=new List<ConversationContextWarning>(); var truncated=false;
            for(var index=request.History.Count-1;index>=0;index--){ cancellationToken.ThrowIfCancellationRequested(); if(selected.Count>=maxTurns){truncated=true;warnings.Add(new("CONVERSATION_TURN_LIMIT_REACHED","The conversation turn limit was reached."));break;} var turn=request.History[index]; var estimate=tokenEstimator.Estimate(turn.Content); if(estimate>maxTokens-tokens){truncated=true;warnings.Add(new("CONVERSATION_TOKEN_BUDGET_REACHED","The conversation token budget was reached."));break;} selected.Add(new(turn.Id,turn.Role,turn.Content,0,estimate,turn.CreatedAt));tokens+=estimate; }
            selected.Reverse(); var items=selected.Select((item,index)=>item with { Order=index }).ToArray(); var pack=new ConversationContextPack(items,tokens,maxTokens,truncated,warnings.Count==0?null:warnings); return Task.FromResult(new ConversationContextResult(warnings.Count==0?ConversationContextStatus.Success:ConversationContextStatus.PartialSuccess,pack,warnings.Count==0?null:warnings)); }
        catch(OperationCanceledException){return Task.FromResult(new ConversationContextResult(ConversationContextStatus.Cancelled));} catch{return Task.FromResult(new ConversationContextResult(ConversationContextStatus.Failed));}
    }
}
