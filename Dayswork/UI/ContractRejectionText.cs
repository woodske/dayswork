using Dayswork.Core.Net;
using Dayswork.Integration;

namespace Dayswork.UI;

/// <summary>
/// Turns the host's rejection code into a line the player can act on. The codes are deliberately
/// specific — "the chest you picked is gone" is a different problem from "you can't afford this" —
/// so each gets its own sentence rather than a single "the host said no".
/// </summary>
internal static class ContractRejectionText
{
    internal static string Describe(ContractRejectionCode? code) =>
        I18nHelper.Get(KeyFor(code));

    private static string KeyFor(ContractRejectionCode? code) =>
        code switch
        {
            ContractRejectionCode.VersionMismatch => "ui.net.rejected.version_mismatch",
            ContractRejectionCode.OfficeGone      => "ui.net.rejected.office_gone",
            ContractRejectionCode.NotOwner        => "ui.net.rejected.not_owner",
            ContractRejectionCode.Stale           => "ui.net.rejected.stale",
            ContractRejectionCode.Paused          => "ui.net.rejected.paused",
            ContractRejectionCode.InvalidScope    => "ui.net.rejected.invalid_scope",
            ContractRejectionCode.ChestMissing    => "ui.net.rejected.chest_missing",
            ContractRejectionCode.MachineMissing  => "ui.net.rejected.machine_missing",
            ContractRejectionCode.PondMissing     => "ui.net.rejected.pond_missing",
            ContractRejectionCode.CannotAfford    => "ui.net.rejected.cannot_afford",
            ContractRejectionCode.InvalidAction   => "ui.net.rejected.invalid_action",
            ContractRejectionCode.TermsChanged    => "ui.net.rejected.terms_changed",
            _                                     => "ui.net.rejected.unknown",
        };
}
