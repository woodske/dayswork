using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Integration;

// M-16 MailDispatcher contract (Patterns P + U). Letters are no-fee and from "Your farmhand".
internal interface IMailDispatcher
{
    // One next-morning settlement letter carrying every overflow item (S-11). Sends nothing when
    // there are no items. Guarantees no item loss: if the mail backend is unavailable, items fall
    // back to the shipping bin. (U-21 BR-END-03 removed shift-end refund settlement — the
    // refund parameter on this signature no longer exists; festival refunds for one-time
    // contracts remain on QueueFestivalNotice, see BR-CAL-03.)
    void QueueSettlement(IReadOnlyList<ItemStack> items, IReadOnlyList<OverflowCategory> categories);

    // One same-day, text-only notice that the recurring contract's fixed daily price was
    // unaffordable. Sent each unaffordable morning.
    void QueueCannotAffordNotice(Contract contract, int dailyPrice, int shortfall);

    // One same-day, text-only notice that a recurring contract's saved scope/settings no longer
    // produce a valid rebuilt contract for today and must be reviewed on the board.
    void QueueNeedsAttentionNotice(Contract contract);

    // One same-day festival courtesy letter. Text-only for a recurring contract; carries the
    // refunded one-time contract price for a one-time contract skipped by a festival.
    void QueueFestivalNotice(Contract contract, int refundGold);
}
