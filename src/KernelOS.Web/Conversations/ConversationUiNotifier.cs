namespace KernelOS.Web.Conversations;

public sealed class ConversationUiNotifier
{
    public event Func<Task>? RefreshRequested;

    public async Task RequestRefreshAsync()
    {
        if (RefreshRequested is not null) await RefreshRequested.Invoke();
    }
}
