using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public abstract record ShiftIntent;

public sealed record IntentMoveToTile(TileCoord Destination) : ShiftIntent;
public sealed record IntentPerformTaskAt(TileCoord Tile, TaskKind Task) : ShiftIntent;
public sealed record IntentPlayEmote(int EmoteId) : ShiftIntent;
public sealed record IntentTeleportToTile(TileCoord Destination) : ShiftIntent;
public sealed record IntentTeleportHome : ShiftIntent;
public sealed record IntentDepositInShippingBin : ShiftIntent;
public sealed record IntentDepositAtChest(ChestRef Chest) : ShiftIntent;
public sealed record IntentExitFarm : ShiftIntent;
