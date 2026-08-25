namespace DesktopIconsToggle.Tests;

public class SettingsTests
{
    private sealed class FakeStore : IRegistryValueStore
    {
        public readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);

        public string? Read(string name) => Values.TryGetValue(name, out var v) ? v : null;
        public void Write(string name, string value) => Values[name] = value;
        public void Delete(string name) => Values.Remove(name);
    }

    [Fact]
    public void LoadFromEmptyStoreYieldsAllDefaults()
    {
        var settings = new Settings(new FakeStore());
        settings.Load();

        Assert.Equal(Settings.DefaultHotkeyModifiers, settings.HotkeyModifiers);
        Assert.Equal(Settings.DefaultHotkeyVirtualKey, settings.HotkeyVirtualKey);
        Assert.Equal(Settings.DefaultHideTaskbar, settings.HideTaskbar);
        Assert.Equal(Settings.DefaultTaskbarHiddenByUs, settings.TaskbarHiddenByUs);
        Assert.Equal(Settings.DefaultHotkeyConfigured, settings.HotkeyConfigured);
    }

    [Fact]
    public void DefaultsMatchDocumentedValues()
    {
        Assert.Equal(0x000Cu, Settings.DefaultHotkeyModifiers);
        Assert.Equal(0x44u, Settings.DefaultHotkeyVirtualKey);
        Assert.False(Settings.DefaultHideTaskbar);
        Assert.False(Settings.DefaultTaskbarHiddenByUs);
        Assert.False(Settings.DefaultHotkeyConfigured);
    }

    [Fact]
    public void SaveThenLoadRoundTripsAllValues()
    {
        var store = new FakeStore();
        var settings = new Settings(store)
        {
            HotkeyModifiers = 0x0003,
            HotkeyVirtualKey = 0x41,
            HideTaskbar = true,
            TaskbarHiddenByUs = true,
            HotkeyConfigured = true,
        };
        settings.Save();

        var reloaded = new Settings(store);
        reloaded.Load();

        Assert.Equal(0x0003u, reloaded.HotkeyModifiers);
        Assert.Equal(0x41u, reloaded.HotkeyVirtualKey);
        Assert.True(reloaded.HideTaskbar);
        Assert.True(reloaded.TaskbarHiddenByUs);
        Assert.True(reloaded.HotkeyConfigured);
    }

    [Fact]
    public void HideTaskbarTrueRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { HideTaskbar = true }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.True(reloaded.HideTaskbar);
    }

    [Fact]
    public void HideTaskbarFalseRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { HideTaskbar = false }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.False(reloaded.HideTaskbar);
    }

    [Fact]
    public void TaskbarHiddenByUsTrueRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { TaskbarHiddenByUs = true }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.True(reloaded.TaskbarHiddenByUs);
    }

    [Fact]
    public void TaskbarHiddenByUsFalseRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { TaskbarHiddenByUs = false }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.False(reloaded.TaskbarHiddenByUs);
    }

    [Fact]
    public void IconsHiddenByUsTrueRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { IconsHiddenByUs = true }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.True(reloaded.IconsHiddenByUs);
    }

    [Fact]
    public void IconsHiddenByUsFalseRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { IconsHiddenByUs = false }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.False(reloaded.IconsHiddenByUs);
    }

    [Fact]
    public void HotkeyConfiguredTrueRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { HotkeyConfigured = true }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.True(reloaded.HotkeyConfigured);
    }

    [Fact]
    public void HotkeyConfiguredFalseRoundTrips()
    {
        var store = new FakeStore();
        new Settings(store) { HotkeyConfigured = false }.Save();

        var reloaded = new Settings(store);
        reloaded.Load();
        Assert.False(reloaded.HotkeyConfigured);
    }

    [Fact]
    public void HotkeyConfiguredDefaultsToFalseWhenAbsent()
    {
        var store = new FakeStore();
        store.Write("HideTaskbar", "1");

        var settings = new Settings(store);
        settings.Load();

        Assert.False(settings.HotkeyConfigured);
        Assert.True(settings.HideTaskbar);
    }

    [Fact]
    public void HotkeyConfiguredFallsBackOnGarbageWithoutDisturbingNeighbours()
    {
        var store = new FakeStore();
        store.Write("HotkeyConfigured", "banane");
        store.Write("HotkeyModifiers", "3");
        store.Write("HotkeyVirtualKey", "65");
        store.Write("HideTaskbar", "1");
        store.Write("TaskbarHiddenByUs", "1");

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(Settings.DefaultHotkeyConfigured, settings.HotkeyConfigured);
        Assert.Equal(3u, settings.HotkeyModifiers);
        Assert.Equal(65u, settings.HotkeyVirtualKey);
        Assert.True(settings.HideTaskbar);
        Assert.True(settings.TaskbarHiddenByUs);
    }

    [Fact]
    public void IconsHiddenByUsDefaultsToFalseWhenAbsent()
    {
        var store = new FakeStore();
        store.Write("HideTaskbar", "1");

        var settings = new Settings(store);
        settings.Load();

        Assert.False(settings.IconsHiddenByUs);
        Assert.True(settings.HideTaskbar);
    }

    [Fact]
    public void IconsHiddenByUsFallsBackOnGarbage()
    {
        var store = new FakeStore();
        store.Write("IconsHiddenByUs", "banane");

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(Settings.DefaultIconsHiddenByUs, settings.IconsHiddenByUs);
    }

    [Fact]
    public void CorruptHotkeyModifiersFallsBackToDefaultWithoutBreakingNeighbours()
    {
        var store = new FakeStore();
        store.Write("HotkeyModifiers", "banane");
        store.Write("HotkeyVirtualKey", "65");
        store.Write("HideTaskbar", "1");
        store.Write("TaskbarHiddenByUs", "1");

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(Settings.DefaultHotkeyModifiers, settings.HotkeyModifiers);
        Assert.Equal(65u, settings.HotkeyVirtualKey);
        Assert.True(settings.HideTaskbar);
        Assert.True(settings.TaskbarHiddenByUs);
    }

    [Fact]
    public void EmptyStringValueFallsBackToDefault()
    {
        var store = new FakeStore();
        store.Write("HotkeyVirtualKey", "");

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(Settings.DefaultHotkeyVirtualKey, settings.HotkeyVirtualKey);
    }

    [Fact]
    public void CorruptHideTaskbarFallsBackToDefaultWithoutBreakingNeighbours()
    {
        var store = new FakeStore();
        store.Write("HotkeyModifiers", "3");
        store.Write("HotkeyVirtualKey", "65");
        store.Write("HideTaskbar", "banane");
        store.Write("TaskbarHiddenByUs", "1");

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(3u, settings.HotkeyModifiers);
        Assert.Equal(65u, settings.HotkeyVirtualKey);
        Assert.Equal(Settings.DefaultHideTaskbar, settings.HideTaskbar);
        Assert.True(settings.TaskbarHiddenByUs);
    }

    [Fact]
    public void PartiallyPopulatedStoreFillsMissingValuesWithDefaults()
    {
        var store = new FakeStore();
        store.Write("HotkeyModifiers", "3");
        // HotkeyVirtualKey, HideTaskbar, TaskbarHiddenByUs left absent.

        var settings = new Settings(store);
        settings.Load();

        Assert.Equal(3u, settings.HotkeyModifiers);
        Assert.Equal(Settings.DefaultHotkeyVirtualKey, settings.HotkeyVirtualKey);
        Assert.Equal(Settings.DefaultHideTaskbar, settings.HideTaskbar);
        Assert.Equal(Settings.DefaultTaskbarHiddenByUs, settings.TaskbarHiddenByUs);
    }

    [Fact]
    public void SaveWritesAllValueNames()
    {
        var store = new FakeStore();
        new Settings(store).Save();

        Assert.True(store.Values.ContainsKey("HotkeyModifiers"));
        Assert.True(store.Values.ContainsKey("HotkeyVirtualKey"));
        Assert.True(store.Values.ContainsKey("HideTaskbar"));
        Assert.True(store.Values.ContainsKey("TaskbarHiddenByUs"));
        Assert.True(store.Values.ContainsKey("IconsHiddenByUs"));
        Assert.True(store.Values.ContainsKey("HotkeyConfigured"));
    }

    [Fact]
    public void SaveWritesBooleansAsOneAndZero()
    {
        var store = new FakeStore();
        new Settings(store) { HideTaskbar = true, TaskbarHiddenByUs = false }.Save();

        Assert.Equal("1", store.Values["HideTaskbar"]);
        Assert.Equal("0", store.Values["TaskbarHiddenByUs"]);
    }

    [Fact]
    public void SaveWritesIntegersAsDecimalStrings()
    {
        var store = new FakeStore();
        new Settings(store) { HotkeyModifiers = 0x000B, HotkeyVirtualKey = 0x1B }.Save();

        Assert.Equal("11", store.Values["HotkeyModifiers"]);
        Assert.Equal("27", store.Values["HotkeyVirtualKey"]);
    }

    [Fact]
    public void SubKeyConstantIsTheDocumentedPath()
    {
        Assert.Equal(@"Software\DesktopIconsToggle", Settings.SubKey);
    }
}
