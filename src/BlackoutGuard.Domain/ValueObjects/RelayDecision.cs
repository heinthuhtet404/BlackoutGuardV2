namespace BlackoutGuard.Domain.ValueObjects;

public sealed record RelayDecision(int RelayAddress, bool Energize, string Reason);
