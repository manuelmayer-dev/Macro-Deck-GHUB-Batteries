using System.Collections.Concurrent;
using GHubBatteries.Batteries;
using GHubBatteries.GHub;
using MacroDeck.Plugin.Testing;
using MacroDeck.Plugin.Testing.Fakes;
using MacroDeck.Sdk.Batteries;
using NUnit.Framework;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace GHubBatteries.Tests;

[TestFixture]
public sealed class GHubBatteryMonitorTests
{
	private FakeGHubSettingsSource _source = null!;
	private FakeBatteryProviderContext _batteries = null!;
	private GHubBatteryMonitor _monitor = null!;

	[SetUp]
	public void SetUp()
	{
		_source = new FakeGHubSettingsSource();
		_batteries = new FakeBatteryProviderContext();
		_monitor = new GHubBatteryMonitor(_source, new ManualTimeProvider(), Serilog.Core.Logger.None);
	}

	[TearDown]
	public async Task TearDown() => await _monitor.DisposeAsync();

	private Task PollAsync() => _monitor.PollOnceAsync(_batteries, CancellationToken.None);

	[Test]
	public async Task The_first_poll_publishes_every_device_in_one_call()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22), GHubJson.Battery("mouse", 80, true)));

		await PollAsync();

		Assert.That(_batteries.Publishes, Has.Count.EqualTo(1));
		Assert.That(_batteries.Devices.Keys, Is.EquivalentTo(new[] { "headset", "mouse" }));
		Assert.That(_batteries.Devices["mouse"].ChargeState, Is.EqualTo(BatteryChargeState.Charging));
		Assert.That(_monitor.Availability, Is.EqualTo(GHubAvailability.Available));
	}

	[Test]
	public async Task Unchanged_devices_are_not_published_again()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));

		await PollAsync();
		await PollAsync();

		Assert.That(_batteries.Publishes, Has.Count.EqualTo(1));
	}

	[Test]
	public async Task Only_the_device_that_changed_is_published()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22), GHubJson.Battery("mouse", 80)));
		await PollAsync();

		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 21), GHubJson.Battery("mouse", 80)));
		await PollAsync();

		Assert.That(_batteries.Publishes[^1].Select(device => device.Id), Is.EqualTo(new[] { "headset" }));
		Assert.That(_batteries.Devices["headset"].LevelPercent, Is.EqualTo(21));
	}

	[Test]
	public async Task A_device_G_HUB_no_longer_reports_stays_published_as_not_connected()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22), GHubJson.Battery("mouse", 80)));
		await PollAsync();

		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("mouse", 80)));
		await PollAsync();

		Assert.That(_batteries.Devices["headset"].IsConnected, Is.False);
		Assert.That(_batteries.Devices["headset"].LevelPercent, Is.EqualTo(22));
		Assert.That(_batteries.RemovedIds, Is.Empty);
	}

	[Test]
	public async Task Keys_that_differ_only_in_case_become_one_device()
	{
		_source.ReturnsJson("""{"battery/Mouse/percentage":{"percentage":10},"battery/mouse/percentage":{"percentage":20}}""");

		await PollAsync();

		Assert.That(_batteries.Devices.Keys, Is.EqualTo(new[] { "mouse" }));
		Assert.That(_batteries.Devices["mouse"].LevelPercent, Is.EqualTo(10));
	}

	[Test]
	public async Task Missing_G_HUB_reports_the_issue_state_and_publishes_nothing()
	{
		_source.Returns(GHubSettingsSnapshot.NotFound);

		await PollAsync();

		Assert.That(_monitor.Availability, Is.EqualTo(GHubAvailability.NotFound));
		Assert.That(_batteries.Publishes, Is.Empty);
	}

	[Test]
	public async Task Devices_become_not_connected_when_G_HUB_disappears_and_return_when_it_is_back()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));
		await PollAsync();

		_source.Returns(GHubSettingsSnapshot.NotFound);
		await PollAsync();
		Assert.That(_batteries.Devices["headset"].IsConnected, Is.False);

		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));
		await PollAsync();
		Assert.That(_batteries.Devices["headset"].IsConnected, Is.True);
		Assert.That(_monitor.Availability, Is.EqualTo(GHubAvailability.Available));
	}

	[Test]
	public async Task A_failed_read_keeps_the_published_devices_and_is_retried()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));
		await PollAsync();

		_source.Fails(new IOException("database is locked"));
		Assert.That(PollAsync, Throws.Nothing);
		Assert.That(_batteries.Devices["headset"].IsConnected, Is.True);

		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 20)));
		await PollAsync();
		Assert.That(_batteries.Devices["headset"].LevelPercent, Is.EqualTo(20));
	}

	[Test]
	public async Task A_failed_publish_is_retried_on_the_next_poll()
	{
		var failingOnce = new FailingOnceBatteryContext(_batteries);
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));

		await _monitor.PollOnceAsync(failingOnce, CancellationToken.None);
		await _monitor.PollOnceAsync(failingOnce, CancellationToken.None);

		Assert.That(_batteries.Devices.Keys, Is.EqualTo(new[] { "headset" }));
	}

	[Test]
	public async Task Never_publishes_more_devices_than_the_host_accepts_and_keeps_known_devices()
	{
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("known", 50)));
		await PollAsync();

		var many = Enumerable.Range(0, BatteryLimits.MaxDevicesPerProvider + 5).Select(i => GHubJson.Battery($"d{i:D3}", 50));
		_source.ReturnsJson(GHubJson.Settings([.. many]));
		await PollAsync();

		Assert.That(_batteries.Devices, Has.Count.EqualTo(BatteryLimits.MaxDevicesPerProvider));
		Assert.That(_batteries.Devices.ContainsKey("known"), Is.True);
		Assert.That(_batteries.Devices["known"].IsConnected, Is.False);
	}

	[Test]
	public async Task The_database_appearing_clears_the_not_found_state_even_when_it_cannot_be_parsed()
	{
		_source.Returns(GHubSettingsSnapshot.NotFound);
		await PollAsync();

		_source.ReturnsJson("not json");
		await PollAsync();

		Assert.That(_monitor.Availability, Is.EqualTo(GHubAvailability.Available));
	}

	[Test]
	public async Task Repeated_problems_are_logged_once_and_not_on_every_poll()
	{
		var sink = new CollectingSink();
		using var logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
		await using var monitor = new GHubBatteryMonitor(_source, new ManualTimeProvider(), logger);

		_source.Returns(GHubSettingsSnapshot.NotFound);
		for (var i = 0; i < 5; i++)
		{
			await monitor.PollOnceAsync(_batteries, CancellationToken.None);
		}

		_source.Fails(new IOException("database is locked"));
		for (var i = 0; i < 5; i++)
		{
			await monitor.PollOnceAsync(_batteries, CancellationToken.None);
		}

		Assert.That(sink.Events.Count(e => e.Level == LogEventLevel.Warning), Is.EqualTo(2));
	}

	[Test]
	public async Task Starting_again_publishes_every_device_again_with_a_single_running_loop()
	{
		var clock = new ManualTimeProvider();
		await using var monitor = new GHubBatteryMonitor(_source, clock, Serilog.Core.Logger.None);
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));

		await monitor.StartAsync(_batteries);
		Assert.That(() => _batteries.Publishes.Count, Is.EqualTo(1).After(5000, 10));

		await monitor.StartAsync(_batteries);
		Assert.That(() => _batteries.Publishes.Count, Is.EqualTo(2).After(5000, 10));

		await monitor.StopAsync();
		var reads = _source.Reads;
		clock.Advance(GHubBatteryMonitor.PollInterval * 3);
		Assert.That(_source.Reads, Is.EqualTo(reads));
	}

	[Test]
	public async Task Polls_immediately_and_then_on_every_interval_until_stopped()
	{
		var clock = new ManualTimeProvider();
		await using var monitor = new GHubBatteryMonitor(_source, clock, Serilog.Core.Logger.None);
		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 22)));

		await monitor.StartAsync(_batteries);
		Assert.That(() => _batteries.Devices.Count, Is.EqualTo(1).After(5000, 10));

		_source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("headset", 21)));
		Assert.That(
			() =>
			{
				clock.Advance(GHubBatteryMonitor.PollInterval);
				return _batteries.Devices["headset"].LevelPercent;
			},
			Is.EqualTo(21).After(5000, 10));

		await monitor.StopAsync();
		var reads = _source.Reads;
		clock.Advance(GHubBatteryMonitor.PollInterval * 3);
		Assert.That(_source.Reads, Is.EqualTo(reads));
	}

	private sealed class CollectingSink : ILogEventSink
	{
		private readonly ConcurrentQueue<LogEvent> _events = new();

		public IEnumerable<LogEvent> Events => _events;

		public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
	}

	private sealed class FailingOnceBatteryContext(IBatteryProviderContext inner) : IBatteryProviderContext
	{
		private bool _failed;

		public Task PublishAsync(BatteryDevice device, CancellationToken cancellationToken = default)
			=> PublishAsync([device], cancellationToken);

		public Task PublishAsync(IReadOnlyList<BatteryDevice> devices, CancellationToken cancellationToken = default)
		{
			if (_failed)
			{
				return inner.PublishAsync(devices, cancellationToken);
			}

			_failed = true;
			return Task.FromException(new InvalidOperationException("RATE_LIMITED"));
		}

		public Task RemoveAsync(string deviceId, CancellationToken cancellationToken = default)
			=> inner.RemoveAsync(deviceId, cancellationToken);
	}
}
