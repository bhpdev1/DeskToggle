namespace DesktopIconsToggle.Tests;

public class AutostartTests
{
    private sealed class FakeStore : IRegistryValueStore
    {
        public readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);

        public string? Read(string name) => Values.TryGetValue(name, out var v) ? v : null;
        public void Write(string name, string value) => Values[name] = value;
        public void Delete(string name) => Values.Remove(name);
    }

    private const string ExePath = @"C:\Tools\DesktopIconsToggle.exe";

    [Fact]
    public void DisabledWhenValueMissing()
    {
        var autostart = new Autostart(new FakeStore(), ExePath);
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void EnableWritesTheQuotedExePath()
    {
        var store = new FakeStore();
        new Autostart(store, ExePath).Enable();
        Assert.Equal($"\"{ExePath}\"", store.Values[Autostart.ValueName]);
    }

    [Fact]
    public void EnabledWhenValueMatches()
    {
        var store = new FakeStore();
        store.Write(Autostart.ValueName, $"\"{ExePath}\"");
        Assert.True(new Autostart(store, ExePath).IsEnabled());
    }

    [Fact]
    public void PathComparisonIgnoresCase()
    {
        var store = new FakeStore();
        store.Write(Autostart.ValueName, "\"c:\\tools\\desktopiconstoggle.exe\"");
        Assert.True(new Autostart(store, ExePath).IsEnabled());
    }

    [Fact]
    public void StaleValueForAnotherPathCountsAsDisabled()
    {
        var store = new FakeStore();
        store.Write(Autostart.ValueName, @"""C:\Old\DesktopIconsToggle.exe""");
        Assert.False(new Autostart(store, ExePath).IsEnabled());
    }

    [Fact]
    public void DisableRemovesTheValue()
    {
        var store = new FakeStore();
        var autostart = new Autostart(store, ExePath);
        autostart.Enable();
        autostart.Disable();
        Assert.False(store.Values.ContainsKey(Autostart.ValueName));
    }

    [Fact]
    public void ToggleFlipsAndReturnsTheNewState()
    {
        var autostart = new Autostart(new FakeStore(), ExePath);
        Assert.True(autostart.Toggle());
        Assert.True(autostart.IsEnabled());
        Assert.False(autostart.Toggle());
        Assert.False(autostart.IsEnabled());
    }

    [Fact]
    public void ToggleRepairsAStalePath()
    {
        var store = new FakeStore();
        store.Write(Autostart.ValueName, @"""C:\Old\DesktopIconsToggle.exe""");
        var autostart = new Autostart(store, ExePath);
        Assert.True(autostart.Toggle());
        Assert.Equal($"\"{ExePath}\"", store.Values[Autostart.ValueName]);
    }
}
