namespace GHubBatteries.GHub;

/// <summary>Where G HUB keeps its settings database on the current operating system.</summary>
internal interface IGHubSettingsLocation
{
	/// <summary>The path of <c>settings.db</c>, or null when G HUB is not supported on this platform.</summary>
	string? GetSettingsDatabasePath();
}

internal sealed class WindowsGHubSettingsLocation : IGHubSettingsLocation
{
	public string GetSettingsDatabasePath() => Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"LGHUB",
		"settings.db");
}

internal sealed class UnsupportedGHubSettingsLocation : IGHubSettingsLocation
{
	public string? GetSettingsDatabasePath() => null;
}
