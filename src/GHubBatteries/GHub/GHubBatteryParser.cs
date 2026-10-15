using System.Text.Json;

namespace GHubBatteries.GHub;

/// <summary>A battery entry from G HUB's settings, keyed by G HUB's own device key.</summary>
internal sealed record GHubBatteryReading(string DeviceKey, double Percentage, bool? IsCharging);

/// <summary>
/// Extracts the <c>battery/&lt;device&gt;/percentage</c> entries from G HUB's settings document. Every other
/// key is skipped without being materialized, so the rest of the document costs almost nothing.
/// </summary>
internal static class GHubBatteryParser
{
	private const string KeyPrefix = "battery/";
	private const string KeySuffix = "/percentage";

	/// <exception cref="JsonException">The document is not a JSON object.</exception>
	public static IReadOnlyList<GHubBatteryReading> Parse(ReadOnlySpan<byte> json)
	{
		if (json.StartsWith((ReadOnlySpan<byte>)[0xEF, 0xBB, 0xBF]))
		{
			json = json[3..];
		}

		var reader = new Utf8JsonReader(json);
		if (!reader.Read() || reader.TokenType != JsonTokenType.StartObject)
		{
			throw new JsonException("The G HUB settings document is not a JSON object.");
		}

		var readings = new List<GHubBatteryReading>();
		while (reader.Read() && reader.TokenType == JsonTokenType.PropertyName)
		{
			var deviceKey = TryGetDeviceKey(reader.GetString());
			reader.Read();

			if (deviceKey is not null && reader.TokenType == JsonTokenType.StartObject)
			{
				var value = JsonElement.ParseValue(ref reader);
				if (TryRead(deviceKey, value) is { } reading)
				{
					readings.Add(reading);
				}
			}
			else
			{
				reader.Skip();
			}
		}

		return readings;
	}

	private static string? TryGetDeviceKey(string? key)
	{
		if (key is null
			|| key.Length <= KeyPrefix.Length + KeySuffix.Length
			|| !key.StartsWith(KeyPrefix, StringComparison.OrdinalIgnoreCase)
			|| !key.EndsWith(KeySuffix, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}

		var deviceKey = key[KeyPrefix.Length..^KeySuffix.Length];
		return deviceKey.Contains('/', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(deviceKey)
			? null
			: deviceKey;
	}

	private static GHubBatteryReading? TryRead(string deviceKey, JsonElement value)
	{
		if (!value.TryGetProperty("percentage", out var percentage)
			|| percentage.ValueKind != JsonValueKind.Number
			|| !percentage.TryGetDouble(out var level))
		{
			return null;
		}

		// G HUB only writes isCharging while the device charges and drops the key once it is unplugged.
		bool? isCharging = value.TryGetProperty("isCharging", out var charging)
			? charging.ValueKind switch
			{
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				_ => null
			}
			: false;

		return new GHubBatteryReading(deviceKey, level, isCharging);
	}
}
