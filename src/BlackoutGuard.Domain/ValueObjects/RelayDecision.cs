namespace BlackoutGuard.Domain.ValueObjects;

public sealed record RelayDecision(int RelayAddress, bool Energize, string Reason)
{
    public static RelayDecision Shed(int relayAddress, string reason)
    {
        return new RelayDecision(relayAddress, false, reason);
    }

    public static RelayDecision Restore(int relayAddress, string reason)
    {
        return new RelayDecision(relayAddress, true, reason);
    }
}