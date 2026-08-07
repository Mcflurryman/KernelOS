using KernelOS.Core.Context;
using KernelOS.Core;

namespace KernelOS.Core.Rag;

public interface IRagPromptBuilder
{
    ChatRequest Build(string query, ContextPack contextPack);
}
