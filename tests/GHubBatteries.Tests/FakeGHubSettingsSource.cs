using System.Text;
using GHubBatteries.GHub;

namespace GHubBatteries.Tests;

internal sealed class FakeGHubSettingsSource : IGHubSettingsSource
{
	private GHubSettingsSnapshot _snapshot = GHubSettingsSnapshot.Available(Encoding.UTF8.GetBytes("{}"));
	private Exception? _failure;

	public int Reads { get; private set; }

	public void Returns(GHubSettingsSnapshot snapshot)
	{
		_snapshot = snapshot;
		_failure = null;
	}

	public void ReturnsJson(string json) => Returns(GHubSettingsSnapshot.Available(Encoding.UTF8.GetBytes(json)));

	public void Fails(Exception failure) => _failure = failure;

	public Task<GHubSettingsSnapshot> ReadAsync(CancellationToken cancellationToken)
	{
		Reads++;
		return _failure is null ? Task.FromResult(_snapshot) : Task.FromException<GHubSettingsSnapshot>(_failure);
	}
}

internal static class GHubJson
{
	public static string Battery(string deviceKey, double percentage, bool? isCharging = null)
	{
		var charging = isCharging is { } value ? $"\"isCharging\":{(value ? "true" : "false")}," : string.Empty;
		return $"\"battery/{deviceKey}/percentage\":{{{charging}\"millivolts\":3672,\"percentage\":{percentage.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"time\":\"2026-06-27T21:36:05Z\"}}";
	}

	public static string Settings(params string[] entries) => "{\"language\":\"en_US\"," + string.Join(',', entries.Append("\"devices/known\":{\"knownList\":[]}")) + "}";
}
