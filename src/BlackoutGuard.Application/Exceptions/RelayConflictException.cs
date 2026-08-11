namespace BlackoutGuard.Application.Exceptions;

public class RelayConflictException : Exception
{
    public int RelayAddress { get; }
    public string? ConflictingLoadName { get; }

    public RelayConflictException(int relayAddress, string? conflictingLoadName)
        : base($"Port {relayAddress} is already assigned to {conflictingLoadName ?? "another load"}")
    {
        RelayAddress = relayAddress;
        ConflictingLoadName = conflictingLoadName;
    }
}
