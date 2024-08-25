using System.Runtime.CompilerServices;

namespace Gensokyo.Ran;

// Allowing for the use of await on a CancellationToken.
public readonly struct CancellationTokenAwaiter(CancellationToken cancellationToken) : ICriticalNotifyCompletion
{
    public bool IsCompleted => cancellationToken.IsCancellationRequested;

    public object GetResult()
    {
        if (IsCompleted)
        {
            // Troll, it was a CancellationToken, not a Task.
            throw new OperationCanceledException();
        }
        
        throw new InvalidOperationException("How did we get here?");
    }
    
    public void OnCompleted(Action continuation)
    {
        cancellationToken.Register(continuation);
    }

    public void UnsafeOnCompleted(Action continuation)
    {
        cancellationToken.Register(continuation);
    }
}
    
public static class CancellationTokenUtilities
{
    public static CancellationTokenAwaiter GetAwaiter(this CancellationToken cancellationToken) => new(cancellationToken);
}