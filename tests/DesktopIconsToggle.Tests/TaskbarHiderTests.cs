namespace DesktopIconsToggle.Tests;

public class TaskbarHiderTests
{
    private const uint AutoHideBit = 0x0000001;    // Win32.ABS_AUTOHIDE
    private const uint AlwaysOnTopBit = 0x0000002; // Win32.ABS_ALWAYSONTOP

    [Fact]
    public void SettingAutoHidePreservesAlwaysOnTop()
    {
        uint result = TaskbarHider.ApplyAutoHide(AlwaysOnTopBit, autoHide: true);

        Assert.Equal(AutoHideBit | AlwaysOnTopBit, result);
    }

    [Fact]
    public void ClearingAutoHidePreservesAlwaysOnTop()
    {
        uint result = TaskbarHider.ApplyAutoHide(AutoHideBit | AlwaysOnTopBit, autoHide: false);

        Assert.Equal(AlwaysOnTopBit, result);
    }

    [Fact]
    public void SettingAutoHideDoesNotTouchUnrelatedBits()
    {
        const uint unrelatedBits = 0xF0;
        uint result = TaskbarHider.ApplyAutoHide(unrelatedBits, autoHide: true);

        Assert.Equal(unrelatedBits | AutoHideBit, result);
    }

    [Fact]
    public void ClearingAutoHideDoesNotTouchUnrelatedBits()
    {
        const uint unrelatedBits = 0xF0;
        uint result = TaskbarHider.ApplyAutoHide(unrelatedBits | AutoHideBit, autoHide: false);

        Assert.Equal(unrelatedBits, result);
    }

    [Fact]
    public void SettingAutoHideWhenAlreadySetIsIdempotent()
    {
        uint result = TaskbarHider.ApplyAutoHide(AutoHideBit | AlwaysOnTopBit, autoHide: true);

        Assert.Equal(AutoHideBit | AlwaysOnTopBit, result);
    }

    [Fact]
    public void ClearingAutoHideWhenAlreadyClearIsIdempotent()
    {
        uint result = TaskbarHider.ApplyAutoHide(AlwaysOnTopBit, autoHide: false);

        Assert.Equal(AlwaysOnTopBit, result);
    }
}
