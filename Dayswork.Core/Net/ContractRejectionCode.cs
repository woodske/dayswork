namespace Dayswork.Core.Net;

/// <summary>
/// Why the host refused a client's contract commit or action. The client turns the code into a
/// localized line and keeps the player's draft, so every value here has an i18n key
/// (<c>ui.net.rejected.*</c>).
/// </summary>
public enum ContractRejectionCode
{
    /// <summary>The sender's protocol version is not this host's.</summary>
    VersionMismatch,

    /// <summary>The office was demolished between the draft and the commit.</summary>
    OfficeGone,

    /// <summary>The sender is neither the office's owner nor the host.</summary>
    NotOwner,

    /// <summary>The contract changed on the host since the draft was built.</summary>
    Stale,

    /// <summary>Dayswork is suspended on the host (an incompatible peer is connected).</summary>
    Paused,

    /// <summary>The draft does not pass the host's own confirm gate.</summary>
    InvalidScope,

    /// <summary>A referenced chest no longer exists in the host's world.</summary>
    ChestMissing,

    /// <summary>A referenced machine no longer exists in the host's world.</summary>
    MachineMissing,

    /// <summary>A referenced fish pond no longer exists in the host's world.</summary>
    PondMissing,

    /// <summary>The owner's wallet cannot cover the up-front price.</summary>
    CannotAfford,

    /// <summary>The requested action does not apply to the contract's current state (pausing a
    /// cancelled contract, cancelling while its shift is running, hiring over an open contract).</summary>
    InvalidAction,
}
