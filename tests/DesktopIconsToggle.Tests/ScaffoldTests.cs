namespace DesktopIconsToggle.Tests;

public class ScaffoldTests
{
    [Fact]
    public void TestProjectReferencesTheApp()
    {
        var assembly = typeof(DesktopIconsToggle.Program).Assembly;
        Assert.Equal("DesktopIconsToggle", assembly.GetName().Name);
    }
}
