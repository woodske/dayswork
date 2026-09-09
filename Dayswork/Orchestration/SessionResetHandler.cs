using StardewModdingAPI.Events;
using Dayswork.Guards;

namespace Dayswork.Orchestration;

internal enum SessionResetBoundary
{
    SaveLoaded,
    ReturnedToTitle,
}

internal interface ISessionBoundaryResettable
{
    void ResetForSessionBoundary(SessionResetBoundary boundary);
}

internal sealed class SessionResetHandler
{
    private readonly ISessionBoundaryResettable _resettable;

    public SessionResetHandler(ISessionBoundaryResettable resettable) => _resettable = resettable;

    internal void ResetForSaveLoaded(bool isHostScreen)
    {
        if (isHostScreen)
            _resettable.ResetForSessionBoundary(SessionResetBoundary.SaveLoaded);
    }

    internal void ResetForReturnedToTitle(bool isHostScreen)
    {
        if (isHostScreen)
            _resettable.ResetForSessionBoundary(SessionResetBoundary.ReturnedToTitle);
    }

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => ResetForSaveLoaded(Authority.IsHost);

    public void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => ResetForReturnedToTitle(Authority.IsHost);
}
