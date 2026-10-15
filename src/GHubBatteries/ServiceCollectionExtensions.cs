using GHubBatteries.Batteries;
using GHubBatteries.GHub;
using Microsoft.Extensions.DependencyInjection;

namespace GHubBatteries;

internal static class ServiceCollectionExtensions
{
	public static IServiceCollection AddGHubBatteries(this IServiceCollection services)
	{
		services.AddSingleton<IGHubSettingsLocation>(_ => OperatingSystem.IsWindows()
			? new WindowsGHubSettingsLocation()
			: new UnsupportedGHubSettingsLocation());
		services.AddSingleton<IGHubSettingsSource, SqliteGHubSettingsSource>();
		services.AddSingleton<GHubBatteryMonitor>();
		return services;
	}
}
