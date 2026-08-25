namespace DesktopIconsToggle.Tests;

public class TaskbarPolicyTests
{
    [Fact]
    public void RestoringWithoutPriorAutoHideClearsAutoHideAndIsNotOwnedByUs()
    {
        (bool autoHide, bool ownedByUs) = TaskbarPolicy.Decide(hidden: false, originalAutoHide: false);

        Assert.False(autoHide);
        Assert.False(ownedByUs);
    }

    [Fact]
    public void RestoringWithPriorAutoHideKeepsAutoHideOnAndIsNotOwnedByUs()
    {
        // The case that matters: a user who already had auto-hide on must get it back,
        // and this app must not claim credit for a setting that was always theirs.
        (bool autoHide, bool ownedByUs) = TaskbarPolicy.Decide(hidden: false, originalAutoHide: true);

        Assert.True(autoHide);
        Assert.False(ownedByUs);
    }

    [Fact]
    public void HidingWithoutPriorAutoHideTurnsAutoHideOnAndIsOwnedByUs()
    {
        (bool autoHide, bool ownedByUs) = TaskbarPolicy.Decide(hidden: true, originalAutoHide: false);

        Assert.True(autoHide);
        Assert.True(ownedByUs);
    }

    [Fact]
    public void HidingWithPriorAutoHideKeepsAutoHideOnAndIsNotOwnedByUs()
    {
        // The case that matters: a user who already had auto-hide on keeps it while we
        // hide the icons too, and this app still must not claim ownership of it — nothing
        // changed from their original setting, so there is nothing for us to restore later.
        (bool autoHide, bool ownedByUs) = TaskbarPolicy.Decide(hidden: true, originalAutoHide: true);

        Assert.True(autoHide);
        Assert.False(ownedByUs);
    }
}
