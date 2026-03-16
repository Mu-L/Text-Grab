using System.IO;
using System.Text.Json;
using Text_Grab.Models;
using Text_Grab.Properties;
using Text_Grab.Services;

namespace Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempFolder;

    public SettingsServiceTests()
    {
        _tempFolder = Path.Combine(Path.GetTempPath(), $"TextGrab_SettingsService_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempFolder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempFolder))
            Directory.Delete(_tempFolder, true);
    }

    [Fact]
    public void LoadStoredRegexes_DefaultModePrefersLegacyAndKeepsLegacyPopulated()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false,
            RegexList = SerializeRegexes("legacy-regex")
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("sidecar-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("legacy-regex", loadedRegex.Id);
        Assert.Contains("legacy-regex", settings.RegexList);
        Assert.Contains("legacy-regex", File.ReadAllText(regexFilePath));
    }

    [Fact]
    public void LoadStoredRegexes_DefaultModeBackfillsLegacyFromSidecarWhenNeeded()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false,
            RegexList = string.Empty
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("recovered-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("recovered-regex", loadedRegex.Id);
        Assert.Contains("recovered-regex", settings.RegexList);
        Assert.Equal(File.ReadAllText(regexFilePath), settings.RegexList);
    }

    [Fact]
    public void LoadStoredRegexes_FileBackedModePrefersSidecarAndBackfillsLegacy()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = true,
            RegexList = SerializeRegexes("legacy-regex")
        };
        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        File.WriteAllText(regexFilePath, SerializeRegexes("sidecar-regex"));

        SettingsService service = CreateService(settings);

        StoredRegex loadedRegex = Assert.Single(service.LoadStoredRegexes());

        Assert.Equal("sidecar-regex", loadedRegex.Id);
        Assert.Contains("sidecar-regex", settings.RegexList);
        Assert.Contains("sidecar-regex", File.ReadAllText(regexFilePath));
    }

    [Fact]
    public void SavePostGrabCheckStates_FileBackedModeWritesBothStores()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = true
        };
        SettingsService service = CreateService(settings);

        service.SavePostGrabCheckStates(new Dictionary<string, bool>
        {
            ["Fix GUIDs"] = true
        });

        string filePath = Path.Combine(_tempFolder, "PostGrabCheckStates.json");
        Assert.Contains("Fix GUIDs", settings.PostGrabCheckStates);
        Assert.True(File.Exists(filePath));
        Assert.Contains("Fix GUIDs", File.ReadAllText(filePath));
        Assert.True(service.LoadPostGrabCheckStates()["Fix GUIDs"]);
    }

    [Fact]
    public void ClearingManagedSettingClearsLegacyAndSidecar()
    {
        Settings settings = new()
        {
            EnableFileBackedManagedSettings = false
        };
        SettingsService service = CreateService(settings);

        service.SaveStoredRegexes(
        [
            new StoredRegex
            {
                Id = "clear-me",
                Name = "Clear Me",
                Pattern = ".*"
            }
        ]);

        string regexFilePath = Path.Combine(_tempFolder, "RegexList.json");
        Assert.NotEmpty(settings.RegexList);
        Assert.True(File.Exists(regexFilePath));

        settings.RegexList = string.Empty;

        Assert.Equal(string.Empty, settings.RegexList);
        Assert.False(File.Exists(regexFilePath));
        Assert.Empty(service.LoadStoredRegexes());
    }

    private SettingsService CreateService(Settings settings) =>
        new(
            settings,
            localSettings: null,
            managedJsonSettingsFolderPath: _tempFolder,
            saveClassicSettingsChanges: false);

    private static string SerializeRegexes(string id) =>
        JsonSerializer.Serialize(new[]
        {
            new StoredRegex
            {
                Id = id,
                Name = $"{id} name",
                Pattern = @"INV-\d+",
                Description = "transition test pattern"
            }
        });
}
