using System.Collections.Generic;
using Dayswork.Core.Domain;
using Dayswork.Core.Inventory;

namespace Dayswork.Integration;

// M-16 MailDispatcher contract (Patterns P + U). Letters are no-fee and from "Your farmhand".
internal interface IMailDispatcher
{
    // One next-morning settlement letter carrying every overflow item (S-11) AND any refund gold
    // (DEV-U15-04). Sends nothing when there are no items and no refund. Guarantees no item loss:
    // if the mail backend is unavailable, items fall back to the shipping bin and gold is credited
    // directly.
    void QueueSettlement(IReadOnlyList<ItemStack> items, IReadOnlySet<OverflowReason> reasons, int refundGold);

    // One same-day, text-only notice that the recurring contract's daily deposit was unaffordable
    // (FR-PAY-04, FD-Q5=A). Sent each unaffordable morning.
    void QueueCannotAffordNotice(Contract contract, int shortfall);

    // One same-day festival courtesy letter (DEV-U15-02). Text-only for a recurring contract;
    // carries the refunded deposit gold for a one-time contract skipped by a festival.
    void QueueFestivalNotice(Contract contract, int refundGold);
}
