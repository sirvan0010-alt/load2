namespace MailLoadTester;

internal enum DeliveryState
{
    Pending = 0,
    InFlight = 1,
    Accepted = 2,
    Failed = 3
}

internal sealed class DeliveryLedger
{
    private readonly int[] _states;

    public DeliveryLedger(int messageCount)
    {
        if (messageCount < 0)
            throw new ArgumentOutOfRangeException(nameof(messageCount));
        _states = new int[messageCount + 1];
    }

    public int Count => _states.Length - 1;

    public DeliveryState GetState(int messageId)
    {
        ValidateId(messageId);
        return (DeliveryState)Volatile.Read(ref _states[messageId]);
    }

    public bool TryClaim(int messageId)
    {
        ValidateId(messageId);
        while (true)
        {
            var current = (DeliveryState)Volatile.Read(ref _states[messageId]);
            if (current is DeliveryState.Accepted or DeliveryState.InFlight)
                return false;

            if (Interlocked.CompareExchange(ref _states[messageId],
                    (int)DeliveryState.InFlight, (int)current) == (int)current)
                return true;
        }
    }

    public bool TryMarkAccepted(int messageId)
    {
        ValidateId(messageId);
        return Interlocked.CompareExchange(ref _states[messageId],
            (int)DeliveryState.Accepted, (int)DeliveryState.InFlight) == (int)DeliveryState.InFlight;
    }

    public bool MarkFailed(int messageId)
    {
        ValidateId(messageId);
        return Interlocked.CompareExchange(ref _states[messageId],
            (int)DeliveryState.Failed, (int)DeliveryState.InFlight) == (int)DeliveryState.InFlight;
    }

    public int CountAccepted() => CountState(DeliveryState.Accepted);
    public int CountFailed() => CountState(DeliveryState.Failed);
    public int CountPending() => CountState(DeliveryState.Pending);
    public int CountInFlight() => CountState(DeliveryState.InFlight);

    private int CountState(DeliveryState state)
    {
        var count = 0;
        for (var i = 1; i < _states.Length; i++)
            if ((DeliveryState)Volatile.Read(ref _states[i]) == state)
                count++;
        return count;
    }

    private void ValidateId(int messageId)
    {
        if ((uint)messageId == 0 || messageId > Count)
            throw new ArgumentOutOfRangeException(nameof(messageId));
    }
}
