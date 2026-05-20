using Dayswork.Core.Domain;

namespace Dayswork.Core.Shifts;

public abstract record ShiftIntent;

public sealed record IntentMoveToTile(TileCoord Destination) : ShiftIntent;
public sealed record IntentPerformTaskAt(TileCoord Tile, TaskKind Task) : ShiftIntent;
public sealed record IntentDepositInShippingBin : ShiftIntent;
public sealed record IntentExitFarm : ShiftIntent;
