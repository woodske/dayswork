using StardewModdingAPI.Events;

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

    internal void ResetForSaveLoaded() =>
        _resettable.ResetForSessionBoundary(SessionResetBoundary.SaveLoaded);

    internal void ResetForReturnedToTitle() =>
        _resettable.ResetForSessionBoundary(SessionResetBoundary.ReturnedToTitle);

    public void OnSaveLoaded(object? sender, SaveLoadedEventArgs e) => ResetForSaveLoaded();

    public void OnReturnedToTitle(object? sender, ReturnedToTitleEventArgs e) => ResetForReturnedToTitle();
}
