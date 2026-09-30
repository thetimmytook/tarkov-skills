namespace TarkovSkills.Core.Tests;

public sealed class ToolkitPreferencesStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "TarkovSkillsTests", Guid.NewGuid().ToString("N"));
    private string PreferencesPath => Path.Combine(_directory, "toolkit-ui.json");

    [Fact]
    public void FirstLaunchShowsGettingStartedWithoutCreatingFiles()
    {
        Assert.True(new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
        Assert.False(Directory.Exists(_directory));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ChoiceSurvivesNewStoreInstance(bool showGetStarted)
    {
        new ToolkitPreferencesStore(PreferencesPath).Save(new ToolkitPreferences { ShowGetStarted = showGetStarted });

        Assert.Equal(showGetStarted, new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
        Assert.False(File.Exists(PreferencesPath + ".tmp"));
    }

    [Fact]
    public void RestoringCardReplacesCollapsedPreference()
    {
        var store = new ToolkitPreferencesStore(PreferencesPath);
        store.Save(new ToolkitPreferences { ShowGetStarted = false });
        store.Save(store.Load() with { ShowGetStarted = true });

        Assert.True(new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"show_get_started\":\"invalid\"}")]
    public void UnusablePreferenceFallsBackWithoutOverwritingFile(string json)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PreferencesPath, json);

        Assert.True(new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
        Assert.Equal(json, File.ReadAllText(PreferencesPath));
    }

    [Fact]
    public void UnknownFieldsDoNotResetSavedChoice()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(PreferencesPath, "{\"show_get_started\":false,\"future_option\":42}");

        Assert.False(new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
    }

    [Fact]
    public void UnreadablePreferenceDoesNotBlockStartup()
    {
        Directory.CreateDirectory(PreferencesPath);
        Assert.True(new ToolkitPreferencesStore(PreferencesPath).Load().ShowGetStarted);
    }

    [Fact]
    public void SaveFailureIsReportedToCaller()
    {
        Directory.CreateDirectory(_directory);
        var blockedParent = Path.Combine(_directory, "file-not-directory");
        File.WriteAllText(blockedParent, "blocked");

        Assert.Throws<IOException>(() => new ToolkitPreferencesStore(Path.Combine(blockedParent, "toolkit-ui.json"))
            .Save(new ToolkitPreferences { ShowGetStarted = false }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
