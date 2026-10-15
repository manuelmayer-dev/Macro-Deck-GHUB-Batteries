using System.Text;
using System.Text.Json;
using GHubBatteries.GHub;
using NUnit.Framework;

namespace GHubBatteries.Tests;

[TestFixture]
public sealed class GHubBatteryParserTests
{
	private static IReadOnlyList<GHubBatteryReading> Parse(string json) => GHubBatteryParser.Parse(Encoding.UTF8.GetBytes(json));

	[Test]
	public void Reads_the_percentage_entry_in_the_shape_G_HUB_stores_it()
	{
		const string json = """
			{
			  "autostart": true,
			  "battery/proxwirelessheadset/percentage": {"millivolts":3672,"percentage":22,"time":"2026-06-27T21:36:05Z"},
			  "battery/proxwirelessheadset/warning": {"deviceId":"dev00000000","level":"LOW","percentage":30,"time":"1782589745"},
			  "devices/known": {"knownList":[{"modelId":"pro_71_gaming_wireless_headset","type":"HEADSET"}]}
			}
			""";

		var readings = Parse(json);

		Assert.That(readings, Is.EqualTo(new[] { new GHubBatteryReading("proxwirelessheadset", 22, null) }));
	}

	[TestCase(true, true)]
	[TestCase(false, false)]
	public void Reads_the_charging_flag_when_present(bool isCharging, bool expected)
	{
		var readings = Parse(GHubJson.Settings(GHubJson.Battery("mouse", 50, isCharging)));

		Assert.That(readings.Single().IsCharging, Is.EqualTo(expected));
	}

	[Test]
	public void Matches_battery_keys_case_insensitively_and_keeps_the_device_key_as_stored()
	{
		var readings = Parse("""{"Battery/G915/Percentage":{"percentage":81.5}}""");

		Assert.That(readings.Single(), Is.EqualTo(new GHubBatteryReading("G915", 81.5, null)));
	}

	[Test]
	public void Skips_malformed_entries_and_keeps_the_valid_ones()
	{
		const string json = """
			{
			  "battery/a/percentage": {"percentage":"high"},
			  "battery/b/percentage": 42,
			  "battery//percentage": {"percentage":10},
			  "battery/c/d/percentage": {"percentage":10},
			  "battery/e/percentage": {"isCharging":"yes","percentage":10}
			}
			""";

		Assert.That(Parse(json), Is.EqualTo(new[] { new GHubBatteryReading("e", 10, null) }));
	}

	[Test]
	public void Accepts_a_byte_order_mark()
	{
		var json = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(GHubJson.Settings(GHubJson.Battery("a", 1)))).ToArray();

		Assert.That(GHubBatteryParser.Parse(json), Has.Count.EqualTo(1));
	}

	[Test]
	public void An_empty_object_has_no_readings()
	{
		Assert.That(Parse("{}"), Is.Empty);
	}

	[TestCase("[]")]
	[TestCase("")]
	[TestCase("{\"battery/a/percentage\":")]
	public void A_document_that_is_not_a_json_object_throws(string json)
	{
		Assert.That(() => Parse(json), Throws.InstanceOf<JsonException>());
	}
}
