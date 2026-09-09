namespace Dayswork.Core.Net;

/// <summary>
/// Every Dayswork message payload. These are plain DTOs serialized by SMAPI with the same
/// Newtonsoft settings the save data uses, so they live in Core (no SMAPI types) and are covered by
/// round-trip tests.
/// <para>
/// A contract travels as the serializer's own JSON envelope (<c>SaveDataSerializer.SerializeOne</c>)
/// rather than as a DTO of its own: it is already schema-versioned, already skips malformed
/// payloads, and reusing it means a contract has exactly one wire format whether it is going into
/// modData or over the network.
/// </para>
/// </summary>
public sealed class HelloMessage
{
    /// <summary>The sender's <see cref="DaysworkProtocol.Version"/>.</summary>
    public int ProtocolVersion { get; set; }

    /// <summary>The sender's mod version, for logs and the mismatch warning.</summary>
    public string ModVersion { get; set; } = "";

    /// <summary>The host's current stand-down state, included so a late joiner has the same banner
    /// before it can open an editable office flow.</summary>
    public bool Suspended { get; set; }
    public SuspensionReason SuspensionReason { get; set; }
    public string SuspendedPlayerName { get; set; } = "";
}

/// <summary>Host → peer, answering <see cref="HelloMessage"/>. Carries the same fields plus
/// whether the host currently has Dayswork suspended.</summary>
public sealed class HelloAckMessage
{
    public int ProtocolVersion { get; set; }
    public string ModVersion { get; set; } = "";
}

/// <summary>Client → host when the hiring hub opens. The host answers with the parts of its world
/// a client cannot see for itself.</summary>
public sealed class MenuSnapshotRequestMessage
{
    public string RequestId { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string OfficeId { get; set; } = "";
}

/// <summary>
/// Host → client: the two pickers a client cannot populate from its own synced world (expansion
/// locations, and chests outside the farm), plus the requester's upgrade state, which lives in
/// host save data.
/// </summary>
public sealed class MenuSnapshotResponseMessage
{
    public string RequestId { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string OfficeId { get; set; } = "";

    /// <summary>Expansion locations the client may select, as internal location names.</summary>
    public List<SnapshotLocation> ExpansionLocations { get; set; } = new();

    /// <summary>Chests outside the farm that may be used as machine-group input chests.</summary>
    public List<SnapshotChest> OffFarmChests { get; set; } = new();

    public bool SpeedPurchased { get; set; }
    public bool Speed2Purchased { get; set; }
    public bool EnergyPurchased { get; set; }
}

public sealed class SnapshotLocation
{
    public string LocationName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsAvailable { get; set; }
}

public sealed class SnapshotChest
{
    public string LocationName { get; set; } = "";
    public string LocationDisplayName { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
    public string DisplayName { get; set; } = "";

    /// <summary>True for an Auto-Grabber's internal chest, which is a valid machine input source
    /// but never a deposit destination — the pickers differ on that, so the flag travels.</summary>
    public bool IsAutoGrabber { get; set; }
}

/// <summary>Client → host: commit this contract to this office.</summary>
public sealed class ContractCommitRequestMessage
{
    public string RequestId { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string OfficeId { get; set; } = "";

    /// <summary>The contract, in the save serializer's envelope format.</summary>
    public string ContractJson { get; set; } = "";

    /// <summary>True when this replaces an existing contract rather than hiring anew.</summary>
    public bool IsEdit { get; set; }

    /// <summary>The revision the draft was built against. Ignored when <see cref="IsEdit"/> is
    /// false (there is nothing to be stale against).</summary>
    public int ExpectedRevision { get; set; }
}

public sealed class ContractCommitResponseMessage
{
    public string RequestId { get; set; } = "";
    public bool Accepted { get; set; }

    /// <summary>The committed contract's new revision, when accepted.</summary>
    public int Revision { get; set; }

    /// <summary>Set when <see cref="Accepted"/> is false.</summary>
    public ContractRejectionCode? Code { get; set; }

    /// <summary>Free-text detail for the log; never shown to the player.</summary>
    public string Detail { get; set; } = "";
}

public enum ContractActionKind
{
    Pause,
    Resume,
    Cancel,
    PurchaseUpgrade,
    ClaimOffice,
}

/// <summary>Client → host: a state change that needs no draft.</summary>
public sealed class ContractActionRequestMessage
{
    public string RequestId { get; set; } = "";
    public int ProtocolVersion { get; set; }
    public string OfficeId { get; set; } = "";
    public ContractActionKind Action { get; set; }

    /// <summary>For <see cref="ContractActionKind.PurchaseUpgrade"/>: the upgrade kind's name.</summary>
    public string UpgradeKind { get; set; } = "";
}

public sealed class ContractActionResponseMessage
{
    public string RequestId { get; set; } = "";
    public ContractActionKind Action { get; set; }
    public bool Accepted { get; set; }
    public ContractRejectionCode? Code { get; set; }
    public string Detail { get; set; } = "";

    /// <summary>The requester's upgrade state after a purchase, so the client's menu can redraw
    /// without a second round trip.</summary>
    public bool SpeedPurchased { get; set; }
    public bool Speed2Purchased { get; set; }
    public bool EnergyPurchased { get; set; }
}

/// <summary>
/// Host → owner: a shift notice that would have been a HUD message if the owner were at the
/// keyboard. Carries the i18n key and its tokens rather than rendered text, so the message is
/// shown in the recipient's own language.
/// </summary>
public sealed class OwnerNoticeMessage
{
    public string TranslationKey { get; set; } = "";
    public Dictionary<string, string> Tokens { get; set; } = new();

    /// <summary>True for an error-styled HUD message, false for an informational one.</summary>
    public bool IsError { get; set; }
}

public enum SuspensionReason
{
    /// <summary>A connected peer does not have Dayswork installed.</summary>
    NoMod,

    /// <summary>A connected peer has Dayswork with a different protocol version.</summary>
    Version,
}

/// <summary>Host → everyone: Dayswork is suspended (or has resumed) because of a peer that cannot
/// safely share the session.</summary>
public sealed class SuspensionMessage
{
    public SuspensionReason Reason { get; set; }

    /// <summary>The offending player's name, for the chat line.</summary>
    public string PlayerName { get; set; } = "";
}
