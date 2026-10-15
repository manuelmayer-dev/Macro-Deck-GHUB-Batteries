using GHubBatteries.Batteries;
using GHubBatteries.GHub;
using MacroDeck.Sdk;
using MacroDeck.Sdk.Actions;
using MacroDeck.Sdk.Batteries;
using MacroDeck.Sdk.Issues;

namespace GHubBatteries;

/// <summary>
/// Publishes the batteries Logitech G HUB knows about. Macro Deck creates the battery variables and widget
/// entries itself, so the integration has no actions or variables of its own.
/// </summary>
internal sealed class GHubBatteriesIntegration(GHubBatteryMonitor monitor)
	: IPluginIntegration, IBatteryProvider, IIntegrationIssueProvider
{
	private const string GHubNotFoundIssueId = "ghub-not-found";
	private const string UnsupportedPlatformIssueId = "unsupported-platform";

	public IReadOnlyList<IActionDefinition> Actions => [];

	public Task InitializeAsync(IIntegrationContext context) => Task.CompletedTask;

	public Task ShutdownAsync() => Task.CompletedTask;

	Task IBatteryProvider.InitializeAsync(IBatteryProviderContext context, CancellationToken cancellationToken)
		=> monitor.StartAsync(context);

	Task IBatteryProvider.ShutdownAsync(CancellationToken cancellationToken) => monitor.StopAsync();

	public Task<IReadOnlyList<IntegrationIssue>> GetIssuesAsync(CancellationToken cancellationToken = default)
	{
		IReadOnlyList<IntegrationIssue> issues = monitor.Availability switch
		{
			GHubAvailability.NotFound =>
			[
				new IntegrationIssue
				{
					Id = GHubNotFoundIssueId,
					Title = Strings.Issues.GHubNotFound.Title(),
					Description = Strings.Issues.GHubNotFound.Description(),
					Severity = IntegrationIssueSeverity.Warning
				}
			],
			GHubAvailability.UnsupportedPlatform =>
			[
				new IntegrationIssue
				{
					Id = UnsupportedPlatformIssueId,
					Title = Strings.Issues.UnsupportedPlatform.Title(),
					Description = Strings.Issues.UnsupportedPlatform.Description(),
					Severity = IntegrationIssueSeverity.Error
				}
			],
			_ => []
		};

		return Task.FromResult(issues);
	}

	// Both issues are informational: only installing G HUB or switching platforms resolves them.
	public Task<IssueResolution> ResolveIssueAsync(string issueId, CancellationToken cancellationToken = default)
		=> Task.FromResult(IssueResolution.Failed());
}
