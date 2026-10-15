namespace GHubBatteries.GHub;

internal interface IGHubSettingsSource
{
	/// <summary>Reads G HUB's current settings document.</summary>
	Task<GHubSettingsSnapshot> ReadAsync(CancellationToken cancellationToken);
}

internal enum GHubAvailability
{
	Unknown,
	Available,
	NotFound,
	UnsupportedPlatform
}

/// <summary>G HUB's settings document as UTF-8 JSON, or why it could not be read.</summary>
internal sealed record GHubSettingsSnapshot(GHubAvailability Availability, byte[] Json)
{
	public static GHubSettingsSnapshot NotFound { get; } = new(GHubAvailability.NotFound, []);

	public static GHubSettingsSnapshot UnsupportedPlatform { get; } = new(GHubAvailability.UnsupportedPlatform, []);

	public static GHubSettingsSnapshot Available(byte[] json) => new(GHubAvailability.Available, json);
}
