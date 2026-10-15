using GHubBatteries.GHub;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GHubBatteries.Tests;

[TestFixture]
public sealed class GHubSettingsLocationTests
{
	[Test]
	public void Mac_location_is_under_the_users_application_support()
	{
		var path = new MacGHubSettingsLocation().GetSettingsDatabasePath();

		Assert.That(path, Does.StartWith(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)));
		Assert.That(path.Replace('\\', '/'), Does.EndWith("/Library/Application Support/LGHUB/settings.db"));
	}

	[Test]
	public void Unsupported_location_has_no_path()
	{
		Assert.That(new UnsupportedGHubSettingsLocation().GetSettingsDatabasePath(), Is.Null);
	}

	[Test]
	public void The_location_matches_the_current_platform()
	{
		using var provider = new ServiceCollection().AddGHubBatteries().BuildServiceProvider();

		var expected = OperatingSystem.IsWindows() ? typeof(WindowsGHubSettingsLocation)
			: OperatingSystem.IsMacOS() ? typeof(MacGHubSettingsLocation)
			: typeof(UnsupportedGHubSettingsLocation);
		Assert.That(provider.GetRequiredService<IGHubSettingsLocation>(), Is.TypeOf(expected));
	}
}
