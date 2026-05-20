using Dayswork.Core.Domain;
using Microsoft.Xna.Framework;

namespace Dayswork.UI;

// UI-only DTOs for ZoneAndChestMenu. Never persisted — ChestRef inside ChestEntry
// is what flows into ContractDraft.Destinations.

internal sealed record ChestEntry(ChestRef Ref, string DisplayName, string GroupLabel);

internal sealed record BuildingOutline(string LocationName, Rectangle TileBounds, string DisplayName);
