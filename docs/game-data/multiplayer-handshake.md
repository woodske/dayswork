# Multiplayer connection ordering and early kicks

Confirmed 2026-09-08 against the installed `StardewModdingAPI.dll` and
`Stardew Valley.dll` under `X:\Steam\steamapps\common\Stardew Valley`, using focused
`ilspycmd -t` decompiles. These facts supplement
[multiplayer-and-ownership.md](multiplayer-and-ownership.md) and resolve its open question
about vanilla guests' connection timing.

## Peer context arrives before the world snapshot, including vanilla guests

`StardewModdingAPI.Framework.SMultiplayer.OnServerProcessingMessage` handles a SMAPI
mod-context message (`255`) by registering the peer, exchanging contexts, and raising
`PeerContextReceived`.

For a player-introduction message (`2`) with no registered context, it creates a vanilla
`MultiplayerPeer`, calls `AddPeer(peer, canBeHost: false)`, and only then invokes the
`resume()` callback. `AddPeer` defaults `raiseEvent` to true and raises
`PeerContextReceived`. After `resume()` returns, the outer handler raises
`PeerConnected`.

The transport's `resume()` callback reads the joining farmer and invokes
`GameServer.checkFarmhandRequest`. On acceptance, that method calls its `approve()`
callback, adds/broadcasts the farmer, sends all always-active locations, sends the
relevant sleeping/disconnection location, and sends the server introduction.

Therefore the installed SMAPI exposes `PeerContextReceived` before those location
snapshots for **both SMAPI and vanilla guests**. Removing a custom NPC synchronously
in that event precedes the world snapshot in this code path. `PeerConnected` is too
late to provide that guarantee. An in-game test should still verify the result on the
actual transport and installed mod combination.

## `kick(playerId)` cannot disconnect a not-yet-approved player

`GameServer.kick(long)` just calls `kick` on each transport. The base
`StardewValley.Network.Server.kick` does nothing.

The installed transport implementations all need a mapping created during approval:

| Transport | Kick lookup | Where the mapping is added |
|---|---|---|
| `StardewValley.SDKs.Steam.SteamNetServer` | `FarmerConnectionMap.TryGetValue(disconnectee, ...)`; `sendMessage` also requires this mapping. | The `approve` delegate passed by `HandleFarmhandRequest` to `GameServer.checkFarmhandRequest`. |
| `StardewValley.Network.LidgrenServer` | `peers.ContainsLeft(disconnectee)` before disconnecting the associated connection. | The `approve` delegate inside `parseDataMessageFromClient`. |
| `StardewValley.SDKs.GogGalaxy.GalaxyNetServer` | `peers.ContainsLeft(disconnectee)` before kicking the associated Galaxy connection. | The `approve` delegate inside `onReceiveMessage`. |

`PeerContextReceived` runs before any of those approval delegates for a newly joining
player. Calling `Game1.server.kick(playerId)` there consequently does not disconnect
that pending connection. The event is still useful for immediately removing unsafe
custom NPCs; a policy that also kicks the peer must perform the actual kick once the
connection mapping exists, and must not skip the early cleanup while waiting.

## The shared admission method precedes approval and location serialization

Rechecked 2026-09-08 while assessing a possible Harmony alternative:
`GameServer.checkFarmhandRequest(string userId, string connectionId, NetFarmerRoot farmer,
Action<OutgoingMessage> sendMessage, Action approve)` performs the admission checks before calling
`approve()`, adding the player, and sending location snapshots. All three transports above call
this shared method. Its arguments include a connection-specific send callback, so that callback
does not depend on the approved player-id mapping used by `kick`.

The vanilla `rejectFarmhandRequest` helper is private. It calls `sendAvailableFarmhands` and logs
the rejection; it does **not** itself disconnect the transport. A patch that skips admission could
prevent the world snapshot, but skipping the method is not equivalent to a complete kick or a
well-explained client rejection. Connection cleanup, peer bookkeeping, retries, and the client
response would still need to be designed and tested.

## Mod-message broadcasts include the sending screen by default

`SMultiplayer.BroadcastModMessage` sets its local-delivery flag true by default. When
`toPlayerIds` is supplied, the flag becomes whether that list includes the current
player's ID. With the flag true it invokes `OnModMessageReceived` locally as well as
sending to the selected peers. Its model carries `Game1.player.UniqueMultiplayerID`
as the sender.

Message handlers that receive a broadcast therefore also run on the sending screen;
authority checks should distinguish host announcements from peer requests rather
than assuming broadcasts exclude the host.
