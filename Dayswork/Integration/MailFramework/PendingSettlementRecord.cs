using System.Collections.Generic;

namespace Dayswork.Integration.MailFramework;

// SMAPI save-data envelope written when a settlement letter is registered with MFM during
// GameLoop.Saving. MFM saves its own state before Dayswork's Saving handler fires (dependency
// load order), so the letter is lost from MFM's registry on session reload. This record lets
// MailDispatcher re-register the letter with MFM at SaveLoaded before MFM begins delivering.
internal sealed class PendingSettlementRecord
{
    public string Id { get; set; } = "";
    public string Sender { get; set; } = "";
    public string Body { get; set; } = "";
    public int EarliestDeliveryDay { get; set; }
    public List<PendingSettlementItemRecord> Items { get; set; } = new();
}

internal sealed class PendingSettlementItemRecord
{
    public string QualifiedItemId { get; set; } = "";
    public int Quantity { get; set; }
}
