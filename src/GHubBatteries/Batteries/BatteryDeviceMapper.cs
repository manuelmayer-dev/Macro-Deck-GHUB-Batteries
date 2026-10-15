using GHubBatteries.GHub;
using MacroDeck.Sdk.Batteries;

namespace GHubBatteries.Batteries;

internal static class BatteryDeviceMapper
{
	private static readonly (string Keyword, string Kind)[] _kindKeywords =
	[
		("headset", BatteryDeviceKinds.Headphones),
		("headphone", BatteryDeviceKinds.Headphones),
		("mouse", BatteryDeviceKinds.Mouse),
		("keyboard", BatteryDeviceKinds.Keyboard),
		("speaker", BatteryDeviceKinds.Speaker)
	];

	/// <summary>The device Macro Deck should show for a reading, or null when the key cannot be published.</summary>
	public static BatteryDevice? Map(GHubBatteryReading reading)
	{
		if (reading.DeviceKey.Length > BatteryLimits.MaxIdLength
			|| reading.DeviceKey.Length > BatteryLimits.MaxNameLength)
		{
			return null;
		}

		var id = reading.DeviceKey.ToLowerInvariant();
		return new BatteryDevice
		{
			Id = id,
			Name = reading.DeviceKey,
			Kind = KindOf(id),
			LevelPercent = (int)Math.Round(Math.Clamp(reading.Percentage, 0, 100), MidpointRounding.AwayFromZero),
			ChargeState = reading.IsCharging switch
			{
				true => BatteryChargeState.Charging,
				false => BatteryChargeState.Discharging,
				null => BatteryChargeState.Unknown
			}
		};
	}

	private static string KindOf(string id)
	{
		foreach (var (keyword, kind) in _kindKeywords)
		{
			if (id.Contains(keyword, StringComparison.Ordinal))
			{
				return kind;
			}
		}

		return BatteryDeviceKinds.Other;
	}
}
