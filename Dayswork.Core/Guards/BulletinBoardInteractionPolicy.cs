namespace Dayswork.Core.Guards;

public enum BulletinBoardInteractionAction
{
    None,
    BlockedByMultiplayer,
    OpenHire,
    OpenManage,
}

public static class BulletinBoardInteractionPolicy
{
    public static BulletinBoardInteractionAction Evaluate(
        bool isMultiplayer,
        bool hireClicked,
        bool manageClicked)
    {
        if (isMultiplayer)
            return BulletinBoardInteractionAction.BlockedByMultiplayer;

        if (hireClicked)
            return BulletinBoardInteractionAction.OpenHire;

        if (manageClicked)
            return BulletinBoardInteractionAction.OpenManage;

        return BulletinBoardInteractionAction.None;
    }
}
