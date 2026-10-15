using GHubBatteries.Batteries;
using GHubBatteries.GHub;
using Microsoft.Extensions.DependencyInjection;

namespace GHubBatteries;

internal static class ServiceCollectionExtensions
{
	public static IServiceCollection AddGHubBatteries(this IServiceCollection services)
	{
		services.AddSingleton(CreateSettingsLocation());
		services.AddSingleton<IGHubSettingsSource, SqliteGHubSettingsSource>();
		services.AddSingleton<GHubBatteryMonitor>();
		return services;
	}

	private static IGHubSettingsLocation CreateSettingsLocation()
	{
		if (OperatingSystem.IsWindows())
		{
			return new WindowsGHubSettingsLocation();
		}

		if (OperatingSystem.IsMacOS())
		{
			return new MacGHubSettingsLocation();
		}

		return new UnsupportedGHubSettingsLocation();
	}
}
