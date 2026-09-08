using Dayswork.Core.Net;
using StardewModdingAPI;
using StardewValley;

namespace Dayswork.Net;

/// <summary>
/// The one place Dayswork puts a message on the wire. Everything is addressed to Dayswork's own mod
/// id, so a peer without the mod never sees any of it.
/// </summary>
internal sealed class NetChannel
{
    private static readonly string[] DaysworkOnly = { DaysworkProtocol.ModId };

    private readonly IMultiplayerHelper _multiplayer;

    public NetChannel(IMultiplayerHelper multiplayer) => _multiplayer = multiplayer;

    /// <summary>Sends to the host. Only meaningful from a remote client — a split-screen guest
    /// shares the host's process and calls the handler directly instead.</summary>
    public void SendToHost<TMessage>(string messageType, TMessage message) where TMessage : class =>
        Send(messageType, message, Game1.MasterPlayer.UniqueMultiplayerID);

    public void Send<TMessage>(string messageType, TMessage message, long playerId) where TMessage : class =>
        _multiplayer.SendMessage(message, messageType, modIDs: DaysworkOnly, playerIDs: new[] { playerId });

    public void Broadcast<TMessage>(string messageType, TMessage message) where TMessage : class =>
        _multiplayer.SendMessage(message, messageType, modIDs: DaysworkOnly);
}
