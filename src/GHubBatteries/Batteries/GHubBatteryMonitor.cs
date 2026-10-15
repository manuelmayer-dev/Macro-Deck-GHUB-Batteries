using GHubBatteries.GHub;
using MacroDeck.Sdk.Batteries;
using MacroDeck.Sdk.Logging;
using Serilog;

namespace GHubBatteries.Batteries;

/// <summary>
/// Polls G HUB's settings and publishes the battery devices that changed since the last publish. Devices
/// G HUB stops reporting, or all of them when G HUB disappears, stay published as not connected.
/// </summary>
internal sealed class GHubBatteryMonitor : IAsyncDisposable
{
	internal static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

	private readonly IGHubSettingsSource _source;
	private readonly TimeProvider _time;
	private readonly ILogger _logger;
	private readonly Dictionary<string, BatteryDevice> _published = new(StringComparer.Ordinal);

	private FailureEpisodeTracker _failures;
	private CancellationTokenSource? _stopping;
	private Task _loop = Task.CompletedTask;
	private volatile GHubAvailability _availability = GHubAvailability.Unknown;
	private bool _deviceLimitLogged;

	public GHubBatteryMonitor(IGHubSettingsSource source, TimeProvider time, ILogger logger)
	{
		_source = source;
		_time = time;
		_logger = logger.ForContext<GHubBatteryMonitor>();
		_failures = new FailureEpisodeTracker(time: time);
	}

	public GHubAvailability Availability => _availability;

	/// <summary>
	/// Starts polling from a clean state. The host withdraws every device before it initializes the
	/// provider again, so nothing from an earlier run may count as already published.
	/// </summary>
	public async Task StartAsync(IBatteryProviderContext context)
	{
		await StopAsync().ConfigureAwait(false);
		Reset();

		_stopping = new CancellationTokenSource();
		var stoppingToken = _stopping.Token;
		_loop = Task.Run(() => RunAsync(context, stoppingToken), CancellationToken.None);
	}

	public async Task StopAsync()
	{
		if (Interlocked.Exchange(ref _stopping, null) is not { } stopping)
		{
			return;
		}

		await stopping.CancelAsync().ConfigureAwait(false);
		await _loop.ConfigureAwait(false);
		stopping.Dispose();
	}

	public ValueTask DisposeAsync() => new(StopAsync());

	internal void Reset()
	{
		_published.Clear();
		_failures = new FailureEpisodeTracker(time: _time);
		_availability = GHubAvailability.Unknown;
		_deviceLimitLogged = false;
	}

	/// <summary>Reads G HUB once and publishes what changed. Never throws except for cancellation.</summary>
	internal async Task PollOnceAsync(IBatteryProviderContext context, CancellationToken cancellationToken)
	{
		try
		{
			var snapshot = await _source.ReadAsync(cancellationToken).ConfigureAwait(false);
			UpdateAvailability(snapshot.Availability);

			var devices = snapshot.Availability == GHubAvailability.Available
				? CurrentDevices(snapshot.Json)
				: [.. _published.Values.Select(device => device with { IsConnected = false })];

			var changed = devices
				.Where(device => !_published.TryGetValue(device.Id, out var previous) || previous != device)
				.ToList();

			if (changed.Count > 0)
			{
				await context.PublishAsync(changed, cancellationToken).ConfigureAwait(false);
				foreach (var device in changed)
				{
					_published[device.Id] = device;
				}
			}

			if (_failures.RecordSuccess() is { } recovered)
			{
				_logger.Information(
					"Reading G HUB batteries recovered after {Duration} and {Failures} failed attempts",
					recovered.Duration,
					recovered.Failures);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw;
		}
#pragma warning disable CA1031 // A failed poll must not end the loop; the next tick retries.
		catch (Exception exception)
#pragma warning restore CA1031
		{
			LogFailure(exception);
		}
	}

	private async Task RunAsync(IBatteryProviderContext context, CancellationToken cancellationToken)
	{
		using var timer = new PeriodicTimer(PollInterval, _time);
		try
		{
			do
			{
				await PollOnceAsync(context, cancellationToken).ConfigureAwait(false);
			}
			while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false));
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
		}
	}

	private List<BatteryDevice> CurrentDevices(byte[] json)
	{
		var current = new Dictionary<string, BatteryDevice>(StringComparer.Ordinal);
		foreach (var reading in GHubBatteryParser.Parse(json))
		{
			if (BatteryDeviceMapper.Map(reading) is { } device)
			{
				current.TryAdd(device.Id, device);
			}
		}

		// Devices already published come first: the host counts them against the limit even while
		// disconnected, so they must never be crowded out by new ones.
		var devices = _published.Keys
			.Select(id => current.TryGetValue(id, out var device) ? device : _published[id] with { IsConnected = false })
			.ToList();

		foreach (var device in current.Values.Where(device => !_published.ContainsKey(device.Id)).OrderBy(device => device.Id, StringComparer.Ordinal))
		{
			if (devices.Count >= BatteryLimits.MaxDevicesPerProvider)
			{
				if (!_deviceLimitLogged)
				{
					_deviceLimitLogged = true;
					_logger.Warning(
						"G HUB reports more battery devices than the {Limit} Macro Deck accepts; the rest are ignored",
						BatteryLimits.MaxDevicesPerProvider);
				}

				break;
			}

			devices.Add(device);
		}

		return devices;
	}

	private void UpdateAvailability(GHubAvailability availability)
	{
		if (_availability == availability)
		{
			return;
		}

		_availability = availability;
		switch (availability)
		{
			case GHubAvailability.NotFound:
				_logger.Warning("G HUB settings database not found. Is Logitech G HUB installed?");
				break;
			case GHubAvailability.UnsupportedPlatform:
				_logger.Warning("G HUB batteries are not supported on this platform");
				break;
			case GHubAvailability.Available:
				_logger.Information("Reading battery levels from G HUB");
				break;
		}
	}

	private void LogFailure(Exception exception)
	{
		var signal = _failures.RecordFailure(exception.Message);
		switch (signal.Kind)
		{
			case FailureEpisodeSignalKind.Onset:
				_logger.Warning(exception, "Reading G HUB batteries failed");
				break;
			case FailureEpisodeSignalKind.SummaryDue:
				_logger.Warning(
					"Reading G HUB batteries still fails after {Duration} and {Failures} attempts: {LastError}",
					signal.Duration,
					signal.ConsecutiveFailures,
					signal.LastError);
				break;
			default:
				_logger.Debug(exception, "Reading G HUB batteries failed again");
				break;
		}
	}
}
