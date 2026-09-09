namespace Dayswork.Core.Net;

/// <summary>
/// The wire contract between Dayswork peers. Every message DTO in this namespace, and the shape of
/// the data written into building modData, is covered by <see cref="Version"/>: bump it whenever
/// either changes in a way an older build could misread.
/// <para>
/// Compatibility is equality, not ordering — a peer whose protocol differs by any amount has its
/// requests rejected (<see cref="ContractRejectionCode.VersionMismatch"/>) and, if it has no
/// Dayswork at all, triggers the host's suspend-or-kick policy. Patch releases that change nothing
/// on the wire keep the same protocol version and interoperate freely.
/// </para>
/// </summary>
public static class DaysworkProtocol
{
    /// <summary>Bumped on any change to the message DTOs or the modData formats.</summary>
    public const int Version = 3;

    /// <summary>The mod id every message is addressed to.</summary>
    public const string ModId = "Bindicle.Dayswork";

    // Message type names. Sent as the SMAPI message type, so they are part of the protocol.
    public const string Hello = "Hello";
    public const string HelloAck = "HelloAck";
    public const string MenuSnapshotRequest = "MenuSnapshotRequest";
    public const string MenuSnapshotResponse = "MenuSnapshotResponse";
    public const string ContractCommitRequest = "ContractCommitRequest";
    public const string ContractCommitResponse = "ContractCommitResponse";
    public const string ContractActionRequest = "ContractActionRequest";
    public const string ContractActionResponse = "ContractActionResponse";
    public const string OwnerNotice = "OwnerNotice";
    public const string DaysworkPaused = "DaysworkPaused";
    public const string DaysworkResumed = "DaysworkResumed";
}
