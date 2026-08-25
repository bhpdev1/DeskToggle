namespace DesktopIconsToggle.Tests;

public class ClickDetectorTests
{
    // 500 ms double-click time => 750 ms accepted window.
    private static ClickDetector Detector() => new(500);

    [Fact]
    public void SingleClickDoesNotFire()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
    }

    [Fact]
    public void SecondClickInsideWindowFires()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.True(detector.RegisterClick(1_400));
    }

    [Fact]
    public void SecondClickOnTheWindowBoundaryFires()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.True(detector.RegisterClick(1_750));
    }

    [Fact]
    public void SecondClickOutsideWindowDoesNotFire()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.False(detector.RegisterClick(1_751));
    }

    [Fact]
    public void ClickAfterAMissedPairStillArms()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.False(detector.RegisterClick(5_000)); // too late, re-arms
        Assert.True(detector.RegisterClick(5_200));  // pairs with 5_000
    }

    [Fact]
    public void TripleClickFiresExactlyOnce()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.True(detector.RegisterClick(1_200));
        Assert.False(detector.RegisterClick(1_400)); // pair consumed, this one re-arms
    }

    [Fact]
    public void QuadrupleClickFiresTwice()
    {
        var detector = Detector();
        Assert.False(detector.RegisterClick(1_000));
        Assert.True(detector.RegisterClick(1_200));
        Assert.False(detector.RegisterClick(1_400));
        Assert.True(detector.RegisterClick(1_600));
    }

    [Fact]
    public void ClockGoingBackwardsDoesNotFire()
    {
        // GetTickCount wraps to zero roughly every 49.7 days of uptime.
        var detector = Detector();
        Assert.False(detector.RegisterClick(4_294_967_000));
        Assert.False(detector.RegisterClick(100));
        Assert.True(detector.RegisterClick(300));  // the post-wrap click armed normally
    }
}
