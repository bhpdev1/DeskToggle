namespace DesktopIconsToggle.Tests;

public class ToggleThrottleTests
{
    [Fact]
    public void FirstRequestIsAlwaysAccepted()
    {
        var throttle = new ToggleThrottle(250);

        Assert.True(throttle.ShouldAccept(0));
    }

    [Fact]
    public void RequestInsideTheIntervalIsRefused()
    {
        var throttle = new ToggleThrottle(250);
        throttle.ShouldAccept(0);

        Assert.False(throttle.ShouldAccept(100));
    }

    [Fact]
    public void RequestExactlyAtTheIntervalIsAccepted()
    {
        var throttle = new ToggleThrottle(250);
        throttle.ShouldAccept(0);

        Assert.True(throttle.ShouldAccept(250));
    }

    [Fact]
    public void RequestAfterTheIntervalIsAccepted()
    {
        var throttle = new ToggleThrottle(250);
        throttle.ShouldAccept(0);

        Assert.True(throttle.ShouldAccept(300));
    }

    [Fact]
    public void ARefusedRequestDoesNotExtendTheWindow()
    {
        // Three presses at 0, 100 and 300 with a 250 ms interval: the second is refused
        // and must not reset the clock, or the third (200 ms after the second) would also
        // be refused instead of being 300 ms clear of the first accepted press.
        var throttle = new ToggleThrottle(250);

        Assert.True(throttle.ShouldAccept(0));
        Assert.False(throttle.ShouldAccept(100));
        Assert.True(throttle.ShouldAccept(300));
    }

    [Fact]
    public void BackwardsClockIsAccepted()
    {
        var throttle = new ToggleThrottle(250);
        throttle.ShouldAccept(1000);

        // The caller's clock went backwards; accept rather than lock the feature out
        // until the clock catches back up to where it was.
        Assert.True(throttle.ShouldAccept(500));
    }

    [Fact]
    public void LongBurstYieldsExactlyTheExpectedNumberOfAcceptances()
    {
        var throttle = new ToggleThrottle(250);
        int accepted = 0;

        for (long t = 0; t <= 2000; t += 50)
        {
            if (throttle.ShouldAccept(t))
            {
                accepted++;
            }
        }

        // Presses every 50 ms from 0 to 2000 inclusive (41 presses). With a 250 ms
        // interval, an accepted press blocks the next four (50, 100, 150, 200 ms later)
        // and the fifth (250 ms later) is accepted: one acceptance every 5 presses.
        // Accepted at t = 0, 250, 500, 750, 1000, 1250, 1500, 1750, 2000 -> 9 acceptances.
        Assert.Equal(9, accepted);
    }
}
