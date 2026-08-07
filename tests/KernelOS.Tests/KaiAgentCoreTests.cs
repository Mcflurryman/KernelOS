using KernelOS.Core; using KernelOS.Core.Conversation; using KernelOS.Core.Kai; using KernelOS.Core.Rag; using KernelOS.Infrastructure.Kai; using Microsoft.Extensions.Options;
namespace KernelOS.Tests;
public sealed class KaiAgentCoreTests
{
 [Fact] public async Task PlannerModeIsSafelyUnavailableWithoutExecution(){var r=await Agent().HandleAsync(new("delete files",PreferredMode:KaiMode.Planner));Assert.Equal(KaiStatus.Failed,r.Status);Assert.Equal("KAI_PLANNER_UNAVAILABLE",r.Warnings![0].Code);}
 [Fact] public async Task AutoDefaultsToChatAndExplicitDocumentQueryUsesRag(){Assert.Equal(KaiMode.Chat,(await Agent().HandleAsync(new("hello"))).ModeUsed);Assert.Equal(KaiMode.Rag,(await Agent().HandleAsync(new("search en mis documentos"))).ModeUsed);}
 [Fact] public async Task ExplicitRagNoContextIsPreservedAndAutoFallsBack(){var no=new FakeRag(RagStatus.NoContext);Assert.Equal(KaiStatus.NoContext,(await Agent(rag:no).HandleAsync(new("x",PreferredMode:KaiMode.Rag))).Status);var fallback=await Agent(rag:no).HandleAsync(new("en mis documentos x"));Assert.Equal(KaiMode.Chat,fallback.ModeUsed);Assert.Contains(fallback.Warnings!,x=>x.Code=="KAI_RAG_NO_CONTEXT_FALLBACK");}
 private static KaiAgent Agent(FakeRag? rag=null)=>new(new FakeConversation(),new DeterministicKaiIntentRouter(),rag??new FakeRag(RagStatus.Success),new FakeChat(),Options.Create(new KaiOptions()));
 private sealed class FakeConversation:IConversationContextBuilder { public Task<ConversationContextResult> BuildAsync(ConversationContextRequest r,CancellationToken t=default)=>Task.FromResult(new ConversationContextResult(ConversationContextStatus.Success,new ConversationContextPack([],0,1,false))); }
 private sealed class FakeRag(RagStatus status):IRagPipeline { public Task<RagResponse> AnswerAsync(RagRequest r,CancellationToken t=default)=>Task.FromResult(new RagResponse(status)); }
 private sealed class FakeChat:IChatModel { public Task<ChatResponse> SendAsync(ChatRequest r,CancellationToken t=default)=>Task.FromResult(new ChatResponse("ok","fake",0,true,null)); }
}
