using GHubBatteries.Batteries;
using GHubBatteries.GHub;
using MacroDeck.Sdk.Batteries;
using NUnit.Framework;

namespace GHubBatteries.Tests;

[TestFixture]
public sealed class BatteryDeviceMapperTests
{
	private static BatteryDevice Map(string key = "device", double percentage = 50, bool? isCharging = null)
		=> BatteryDeviceMapper.Map(new GHubBatteryReading(key, percentage, isCharging))!;

	[Test]
	public void Uses_the_lowercased_key_as_id_and_the_key_as_name()
	{
		var device = Map("ProXWirelessHeadset");

		Assert.That(device.Id, Is.EqualTo("proxwirelessheadset"));
		Assert.That(device.Name, Is.EqualTo("ProXWirelessHeadset"));
		Assert.That(device.IsConnected, Is.True);
	}

	[TestCase("proxwirelessheadset", BatteryDeviceKinds.Headphones)]
	[TestCase("g_headphones", BatteryDeviceKinds.Headphones)]
	[TestCase("prowirelessmouse", BatteryDeviceKinds.Mouse)]
	[TestCase("g915keyboard", BatteryDeviceKinds.Keyboard)]
	[TestCase("speaker_x", BatteryDeviceKinds.Speaker)]
	[TestCase("g502", BatteryDeviceKinds.Other)]
	public void Derives_the_kind_from_the_key(string key, string kind)
	{
		Assert.That(Map(key).Kind, Is.EqualTo(kind));
	}

	[TestCase(21.5, 22)]
	[TestCase(21.4, 21)]
	[TestCase(140, 100)]
	[TestCase(-3, 0)]
	public void Rounds_and_clamps_the_level(double percentage, int level)
	{
		Assert.That(Map(percentage: percentage).LevelPercent, Is.EqualTo(level));
	}

	[TestCase(true, BatteryChargeState.Charging)]
	[TestCase(false, BatteryChargeState.Discharging)]
	[TestCase(null, BatteryChargeState.Unknown)]
	public void Maps_the_charging_flag(bool? isCharging, BatteryChargeState state)
	{
		Assert.That(Map(isCharging: isCharging).ChargeState, Is.EqualTo(state));
	}

	[Test]
	public void A_key_longer_than_the_id_limit_is_not_published()
	{
		var reading = new GHubBatteryReading(new string('a', BatteryLimits.MaxIdLength + 1), 50, null);

		Assert.That(BatteryDeviceMapper.Map(reading), Is.Null);
	}
}
