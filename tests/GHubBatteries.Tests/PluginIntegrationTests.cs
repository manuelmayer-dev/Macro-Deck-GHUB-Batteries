using GHubBatteries.GHub;
using MacroDeck.Plugin.Testing;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Batteries;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace GHubBatteries.Tests;

/// <summary>
/// The plugin as the host sees it, through <see cref="PluginTestHarness"/>, with G HUB replaced by a fake.
/// </summary>
[TestFixture]
public sealed class PluginIntegrationTests
{
	private static PluginTestHarness CreateHarness(FakeGHubSettingsSource source) =>
		PluginTestHarness.Create(builder =>
		{
			builder
				.UseLocalization(Strings.LocalizationCatalog)
				.RegisterIntegration<GHubBatteriesIntegration>();
			builder.Services.AddGHubBatteries();
			builder.Services.AddSingleton<IGHubSettingsSource>(source);
		});

	private static GHubBatteriesIntegration Integration(PluginTestHarness harness)
		=> harness.Services.GetServices<IPluginIntegration>().OfType<GHubBatteriesIntegration>().Single();

	private static Task ShutdownBatteriesAsync(PluginTestHarness harness)
		=> ((IBatteryProvider)Integration(harness)).ShutdownAsync();

	[Test]
	public async Task Initializing_publishes_the_G_HUB_devices()
	{
		var source = new FakeGHubSettingsSource();
		source.ReturnsJson(GHubJson.Settings(GHubJson.Battery("proxwirelessheadset", 22)));
		await using var harness = CreateHarness(source);

		await harness.InitializeIntegrationsAsync();
		try
		{
			Assert.That(() => harness.Context.BatteryProvider.Devices.Count, Is.EqualTo(1).After(5000, 10));
			var device = harness.Context.BatteryProvider.Devices["proxwirelessheadset"];
			Assert.That(device.LevelPercent, Is.EqualTo(22));
			Assert.That(device.Kind, Is.EqualTo(BatteryDeviceKinds.Headphones));
		}
		finally
		{
			// The harness does not shut battery providers down itself.
			await ShutdownBatteriesAsync(harness);
		}
	}

	[Test]
	public async Task Reports_an_issue_while_G_HUB_is_not_installed()
	{
		var source = new FakeGHubSettingsSource();
		source.Returns(GHubSettingsSnapshot.NotFound);
		await using var harness = CreateHarness(source);
		var issues = Integration(harness);

		await harness.InitializeIntegrationsAsync();
		try
		{
			Assert.That(
				async () => (await issues.GetIssuesAsync()).Select(issue => issue.Id),
				Is.EqualTo(new[] { "ghub-not-found" }).After(5000, 10));
		}
		finally
		{
			await ShutdownBatteriesAsync(harness);
		}
	}

	[Test]
	public async Task No_issue_while_G_HUB_is_available()
	{
		var source = new FakeGHubSettingsSource();
		await using var harness = CreateHarness(source);
		var integration = Integration(harness);

		var issues = await integration.GetIssuesAsync();

		Assert.That(issues, Is.Empty);
	}

	[Test]
	public async Task Resolving_an_issue_reports_that_the_user_has_to_act()
	{
		await using var harness = CreateHarness(new FakeGHubSettingsSource());
		var integration = Integration(harness);

		var resolution = await integration.ResolveIssueAsync("ghub-not-found");

		Assert.That(resolution.Success, Is.False);
	}
}

/// <summary>
/// The localization set is generated from <c>Localization/*.resx</c>, so these guard the wiring rather
/// than any wording: a missing catalog registration leaves every label showing its raw key.
/// </summary>
[TestFixture]
public sealed class LocalizationTests
{
	[Test]
	public void The_catalog_is_scoped_to_the_plugin_id()
	{
		Assert.That(Strings.LocalizationCatalog.Scope, Is.EqualTo("plugin:com.suchbyte.ghub-batteries"));
	}

	[Test]
	public void English_is_the_default_culture_and_German_is_shipped()
	{
		Assert.That(Strings.LocalizationCatalog.DefaultCulture, Is.EqualTo("en"));
		Assert.That(Strings.LocalizationCatalog.Cultures, Is.SupersetOf(new[] { "en", "de" }));
	}

	[Test]
	public void German_translates_every_key()
	{
		Assert.That(Strings.LocalizationCatalog.KeysOf("de"), Is.EquivalentTo(Strings.LocalizationCatalog.KeysOf("en")));
	}

	[Test]
	public void Every_key_resolves_to_text()
	{
		foreach (var culture in new[] { "en", "de" })
		{
			foreach (var key in Strings.LocalizationCatalog.KeysOf(culture))
			{
				Assert.That(Strings.LocalizationCatalog.TryGetTemplate(culture, key, out var text), Is.True);
				Assert.That(text, Is.Not.Empty);
			}
		}
	}
}
