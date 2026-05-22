using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Integration;

// M-16 MailDispatcher contract (Pattern P). Shaped so U-15 can add QueueCannotAffordMail(...)
// later without reshaping the adapter.
internal interface IMailDispatcher
{
    // One next-morning, no-fee letter carrying every overflow item (S-11). Guarantees no item
    // loss: if the mail backend is unavailable, items fall back to the shipping bin.
    void QueueOverflowMail(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons);

    // One next-morning, no-item warning letter listing tool-gated tasks (FD-Q7=A intent).
    void QueueToolMissingWarning(IReadOnlySet<TaskKind> tasks);
}
