namespace DesktopIconsToggle;

/// <summary>
/// Decides what to do with the taskbar's auto-hide setting, and whether this app owns
/// the result. Pure so the four cases can be tested; the caller performs the effects.
/// </summary>
internal static class TaskbarPolicy
{
    /// <param name="hidden">Whether the app wants the taskbar hidden right now.</param>
    /// <param name="originalAutoHide">The user's own setting, read at startup.</param>
    /// <returns>
    /// The auto-hide value to write, and whether the app is responsible for it — the
    /// latter decides whether a crash-recovery marker is stored.
    /// </returns>
    public static (bool AutoHide, bool OwnedByUs) Decide(bool hidden, bool originalAutoHide)
        => (hidden || originalAutoHide, hidden && !originalAutoHide);
}
