using GHubBatteries;
using MacroDeck.Plugin.Hosting;
using MacroDeck.Plugin.Serilog;

// Identity, description and icon are not set here: they come from manifest.json at the content root.
var builder = MacroDeckPlugin.CreatePlugin(args)
	.UseMacroDeckLogging()
	.UseLocalization(Strings.LocalizationCatalog)
	.RegisterIntegration<GHubBatteriesIntegration>();

builder.Services.AddGHubBatteries();

await builder.Build().RunAsync();
