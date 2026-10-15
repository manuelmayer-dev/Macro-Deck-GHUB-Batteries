# Agent guidance

This file must be kept up to date. When a rule here stops matching reality, or a new rule emerges from
work in this repository, update this file as part of that change rather than leaving it to drift.

This repository is the **GHub Batteries** plugin for Macro Deck 3: an out-of-process plugin that reads the
battery levels Logitech G HUB stores in its local settings database and publishes them through the SDK's
`IBatteryProvider`. Macro Deck owns the battery variables and the Battery widget; the plugin has no actions,
variables or widgets of its own.

The Macro Deck 2 version of this plugin lives in the git history before the Macro Deck 3 port. Do not
reintroduce its files or its `SuchByte.MacroDeck` reference.

Worked examples of every capability live in the
[sample plugins repository](https://github.com/Macro-Deck-App/Macro-Deck-Sample-Plugins).

[README.md](README.md) is the human-facing guide: what the plugin does, how to build, run and pack it.
This file is the rule set for writing the plugin. Read it before changing code.

## Orientation

```
src/GHubBatteries/
  Program.cs                     builder chain, plus AddGHubBatteries() on builder.Services
  ServiceCollectionExtensions.cs DI registrations, the G HUB location is chosen per OS here
  GHubBatteriesIntegration.cs    IPluginIntegration + IBatteryProvider + IIntegrationIssueProvider, thin glue
  Batteries/GHubBatteryMonitor.cs   poll loop, diff against what was published, connected state, limits
  Batteries/BatteryDeviceMapper.cs  G HUB reading -> BatteryDevice (pure)
  GHub/IGHubSettingsLocation.cs  settings.db path per OS; add a macOS implementation here
  GHub/SqliteGHubSettingsSource.cs  read-only SQLite access to settings.db
  GHub/GHubBatteryParser.cs      battery/<device>/percentage entries from the settings JSON (pure)
  manifest.json, macrodeck-build.json   identity and the win-x64 publish target
  Localization/Strings.resx, Strings.de.resx
  Assets/icon.png
tests/GHubBatteries.Tests/       NUnit tests: parser, mapper, monitor, SQLite source, plugin harness
```

## G HUB specifics

- G HUB keeps one row in table `data` of `%LOCALAPPDATA%\LGHUB\settings.db` (WAL mode); column `file` is a
  UTF-8 JSON object. Battery entries are `"battery/<device>/percentage": {"percentage": 22, "millivolts":
  3672, "time": "...", "isCharging": true}`; `isCharging` is missing for some devices. Ignore
  `battery/<device>/warning` and every other key.
- Open the database read-only, unpooled and with a short timeout. Never write to it.
- Device ids are the lowercased G HUB key. They end up in users' widgets and variable names, so never
  change how they are derived.
- Platforms: only win-x64 is declared. Adding macOS means a new `IGHubSettingsLocation`, an `osx-*`
  entrypoint and build target, the CI matrix and the README, in one change, tested on a Mac.

## Store gate

A plugin is published through the Macro Deck Store, and the Creator Portal refuses a build that breaks
its policy. The policy changes over time, so never rely on a copy, on memory or on an earlier fetch in
the same session. **Fetch every document below again before you start a change and again before you call
it done.** If one cannot be fetched, say so and stop rather than assuming it still says what it said last
time.

| What | Where |
| --- | --- |
| Creator Guidelines (Markdown) | <https://api.macro-deck.app/api/v1/public/creator-guidelines> |
| Blocked packages (JSON) | <https://api.macro-deck.app/api/v1/public/dependency-policy/blocked-packages> |
| Minimum SDK version and allowed Macro Deck packages (JSON) | <https://api.macro-deck.app/api/v1/public/dependency-policy/sdk> |

A change is not done until all six hold:

1. **The plugin follows the Creator Guidelines.** Read the whole document, not just the part that seems
   relevant, and check the change against every rule in it. Where a rule here and the guidelines
   disagree, the guidelines win. Point out the conflict so this file can be corrected.
2. **No blocked package is used.** Check every entry in `blockedPackages` against the full dependency
   graph (`dotnet list package --include-transitive`), not only the packages referenced directly. An
   entry matches by `packagePattern`. A `versionPattern` of `null` blocks every version. Otherwise only
   the matching versions are blocked. `reason` says why. Replace the package or restructure the code
   that needs it. Never work around a block by vendoring, renaming or loading the package some other
   way.
3. **The Macro Deck SDK is at least `minimumSdkVersion`.** `MacroDeckSdkVersion` in
   `Directory.Packages.props` is pinned. Bump it together with `cli-version` in
   `.github/workflows/release.yaml` and the CLI install in `.github/workflows/ci.yaml`. A release must never
   be built against an SDK older than the minimum.
4. **Only allowed Macro Deck packages are used.** Every package in the graph whose id starts with
   `MacroDeck.` must match an entry in `allowedMacroDeckPackages`. Anything else under that prefix is
   refused on upload, including packages from another Macro Deck repository that were never published
   for plugins.
5. **The manifest names its author, licence and repository.** `publisher.name` in `manifest.json` must
   be the owner the plugin is published under in the Creator Portal: the Organization's name, or for a
   personal Project the creator's username (compared ignoring case). The Store always shows that owner,
   and an upload whose manifest names anyone else is refused. `license` must be set, at most 64
   characters, as an SPDX identifier such as `MIT`; the Store shows it as the plugin's licence.
   `repository` must be the GitHub repository the plugin is built and released from, written
   `https://github.com/<owner>/<name>`; an upload from any other repository is refused. The template's
   `Example Publisher` and `https://github.com/example/my-plugin` are placeholders and never pass.
6. **The plugin passes the conformance suite on every platform it declares.** The Store refuses a build
   that does not come with a conformance report for each platform in `entrypoints`, or whose report shows
   a failed Required check, never reached the plugin (`MDC0201`, the handshake, did not pass), or is for
   another plugin id or version. The official publishing workflow runs the suite on a stub host per
   platform and sends the reports, so never turn its `run-stub-host` off. Before calling a change done,
   run `macrodeck-plugin test` (see "Verifying a change") and fix every Required failure; a failed
   Recommended check is allowed but worth fixing.

Report the result of this gate with every change: which version of the guidelines was checked (the
`X-Creator-Guidelines-Version` response header), and whether each of the six points holds.

## The rules that make a plugin clean

### Identity

- A plugin's identity is `manifest.json` and nothing else. `IPluginIntegration` carries no `Id`, `Name`,
  `Version` or `IsInitialized`, and it never implements `IIntegrationIconProvider` - both of those are
  the in-process `IIntegration`'s surface. The builder's old `WithId`/`WithName`/`WithVersion`/
  `WithDescription`/`WithIcon` are gone; do not reintroduce them.
- Ids you write in source are **local ids** - `^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$`, max 64 chars, never
  containing `::`. The host derives the qualified `integrationId::localId` form from your authenticated
  registration. Never build or submit a qualified id yourself.
- **Action ids must be unique across the whole plugin**, not just per integration. In process the key is
  (integration id, action id); over the wire the owner is the plugin. A collision fails `Build()`.
- Event definition ids and variable `DefinitionId`s are persisted in user data. Treat them as a public
  API: renaming one breaks every widget already bound to it.

### The builder and the process

- Keep `Program.cs` short: `CreatePlugin(args)` → `.UseMacroDeckLogging()` →
  `.UseLocalization(Strings.LocalizationCatalog)` → `.RegisterIntegration<T>()`, then
  `builder.Services.AddGHubBatteries()`, `Build()` and `RunAsync()`. It is a real `WebApplication` builder, so
  `IHttpClientFactory`, options binding, hosted services and DI are all available and preferred over
  hand-rolled equivalents. Extra registrations go on `builder.Services` before `Build()`.
- `RegisterIntegration<T>()` is the one door: it also registers the capability handler for every SDK
  interface the integration implements, so you never write a handler for a built-in capability kind. A
  genuinely new kind uses `ICapabilityHandler` + `RegisterCapabilityHandler<T>()`.
- `services.AddMacroDeckIntegration<T>()` is internal to the hosting package now and cannot be called
  from a plugin project. Registering the integration with `services.AddSingleton<T>()` is an analyzer
  warning, not a workaround.
- `Build()` constructs every integration and handler as part of validation, so a constructor must be
  side-effect-free and cheap.
- **Never set your own listener URL.** No `UseUrls`, no `Configuration["urls"]`, no `ASPNETCORE_URLS` in
  `launchSettings.json` or `appsettings.json`. The supervisor binds a port and starts probing
  `GET /_macrodeck/health` before your process starts; overriding it makes the health check fail silently
  and permanently, with nothing surfaced to explain why.
- `/_macrodeck/*` is reserved. Mapping a route under it fails `Build()`.
- The only writable location to rely on is `MACRO_DECK_PLUGIN_DATA_DIRECTORY`. Anything written next to
  the executable lives in an immutable version directory and disappears on the next update or rollback.
- `Build()` reports **every** local problem at once in one `PluginConfigurationException`. Read the whole
  message before fixing anything; do not iterate one error per run.

### Lifecycle

`InitializeAsync` does not run at process start. It is gated on the connection being established, and it
runs again after a non-resume reconnect and whenever the host reports a configuration change. Make it
idempotent and safe to run repeatedly against an already-initialized process.

### Actions

- `ActionResult` must be truthful. `Success()` claims the operation completed. Could not reach the
  provider, refused a permission, handed an unusable value → `Failed(code, message)` with the closest
  `ActionErrorCodes` value. A press that did nothing must never report success.
- A legitimate no-op *is* success: an optional parameter left blank, a repeat count of zero, a setting
  already in the requested state.
- `Accepted` is only for work the provider took but cannot confirm. Where the API *can* confirm, poll
  until it does rather than returning `Accepted`.
- Throwing works (the flow engine records a failure) but the caller only ever sees a generic code - an
  exception message is never sent to a client. Prefer an explicit `Failed`.
- A synchronous executor returns the cached `ActionResult.SucceededTask` rather than allocating.
- **Forward `context.CancellationToken`** into everything you await. Dropping it is MDP3001.
- Parameter visibility (`OnlyWhen`) is presentation only. The host still sends hidden parameters, so
  validate the combination in the executor - never infer anything from a field being hidden.

### Async and concurrency

- No `.Result`, `.Wait()`, `.GetAwaiter().GetResult()` or `Thread.Sleep` anywhere in a type implementing
  `ICapabilityHandler`, `IActionExecutor` or `IConfigFlow` - the rule is whole-type, not just the
  interface methods, because the dispatcher has 32 concurrent invocation slots and a block anywhere
  reachable starves the rest (MDP3002).
- No `async void` on an SDK contract type; an exception there kills the process instead of failing one
  call. The `(object? sender, EventArgs e)` handler shape is the one exception (MDP3003).
- Invocations dispatch **concurrently**, each in its own DI scope. Instance state touched from more than
  one invocation needs its own synchronization - the same discipline as any concurrently invoked ASP.NET
  Core endpoint.
- `ICapabilityInvocationContext` only resolves inside an invocation scope. A singleton must not depend on
  it (MDP4001).

### Fire-and-forget contracts

`IEventPublisher.Publish`, `IUserNotifier.Notify`/`Dismiss` and `IPluginCatalogNotifier.CatalogChanged`
never throw and are safe to call with no live session. Mirror that in your own wrappers: they are called
from websocket callbacks and poll loops where a throw would take the integration's own work down.

The round-trip host callbacks - `context.Variables`, `context.Config`, `context.UserVariables`,
`context.Deck`'s mutating members, `context.Scripts.RunAsync`, `context.Widgets.ApplyAsync` - are the
opposite: real network calls that can throw `HostInvocationException` on rate limiting, timeout or no
connection. Handle them like any networked call. `Deck.GetFolders()`, `Scripts.GetScripts()` and
`Widgets.GetWidgets()` read a host-pushed cache instead and return empty in the short window before the
first push.

### Catalogues go stale - say so

Every synchronous catalog-shaped member (`GetInstances`, `EventDefinitions`, `DeclaredVariables`,
`GetProfiles`, …) is served to the host from a cached `describe`, not a live call. When something outside
a host-initiated invocation changes what a later `describe` would answer - a config value
`InitializeAsync` just read, a device that appeared or vanished - inject `IPluginCatalogNotifier` and call
`CatalogChanged(kind, reason)`. Skipping it leaves the UI disagreeing with the plugin until the next
reconnect. The host describes capabilities concurrently with `InitializeAsync`, so a first describe can
capture a default before the config read finishes.

### Configuration and secrets

- A config flow produces the entries; `InitializeAsync` reads them back through
  `IIntegrationContext.Config`. Keep that division - the flow validates and persists, the integration
  consumes.
- Persist credentials as `ConfigFlowValue.Secret` so they land in the host's encrypted secret store.
  Never write a token to a plain string field, a log line, or a file of your own. Rotating credentials go
  back through `SetSecretAsync`.
- Never run your own OAuth redirect server. Return `ConfigFlowResult.External(url, resumeStepId)` and let
  the host own the redirect and the callback correlation.
- An integration that provides a config flow starts **disabled** until the user completes it. Everything
  else starts enabled.

### Localization

- **No user-facing literal.** Every string a user reads is a key in `Localization/Strings.resx`, reached
  through the generated `Strings` class: action names and descriptions, parameter labels, descriptions
  and placeholders, `ActionStateDefinition` labels, config flow step titles and field labels, event and
  variable metadata, issue text, and the message on `ActionResult.Failed`/`Accepted`. A plain `string`
  converts to `LocalizedText` too, so nothing stops you - which is exactly why this is a rule.
- `ConfigFlowResult.Complete(title, …)` is the one deliberate exception: it takes a plain `string`
  because the host stores it as the configured entry's name and the user renames it from there. Write it
  in the default language and leave it.
- Log messages and exception messages are diagnostics, not UI. They stay English literals; never
  localize a log template.
- Check `MacroDeckStrings` before adding a key - `Common.*`, `Validation.*`, `Connection.*`,
  `Settings.*` are already translated everywhere Macro Deck ships. A duplicated `Save` is one more string
  every translator keeps in sync for text the reader already sees. Compose instead:
  `MacroDeckStrings.Validation.Required(Strings.Some.Field.Label())`.
- **Keys are dotted, not underscored.** A dotted key becomes a nested class, so
  `Issues.GHubNotFound.Title` is `Strings.Issues.GHubNotFound.Title()`. Name a key after
  where it is used, not after the English wording. A key that is also a group other keys nest under is
  MDLOC008.
- Placeholders are named (`{host}`) and become method parameters, so a missing one is a compile error.
  Positional `{0}` gives no such safety. A placeholder is a `string` unless a bracketed prefix on the
  entry's `<comment>` narrows it to `int`, `long`, `double` or `bool`.
- A count-dependent sentence is one key: `[plural]` on **every** form, keys suffixed `.One` and `.Other`,
  `Other` required. The rule is `count == 1` for every language and deliberately not CLDR, so phrase
  `Other` to stay grammatical where that rule does not hold.
- A translation is `Localization/Strings.<culture>.resx` with a well-formed BCP-47 name (`de`, `pt-BR`,
  `zh-Hant-TW`, never a truncated `zh`, never an underscore). It needs only the keys it translates; the
  chain falls back requested culture → neutral → catalog default → `en`. Nothing is registered per
  language.
- `Strings.resx` itself is required even for a single-language plugin: it is what every translation is
  checked against and the last resource the chain tries.
- The manifest's `languages` array is derived by `macrodeck-plugin build` and `pack` from this folder.
  Hand-maintaining it is the same mistake as hand-maintaining `files[]`.
- The generator's own diagnostics are MDLOC001-MDLOC008 (key only in a translation, placeholder
  mismatch, duplicate key, bad parameter type, malformed culture suffix, removed `MacroDeckStrings` key,
  broken plural family, key/group collision). Fix them; do not suppress them.
- Dropping `UseLocalization(Strings.LocalizationCatalog)` from `Program.cs` does not fail the build. It
  fails at runtime, quietly, with every label rendering as `[[plugin:<id>:Key]]`.
- The full reference is <https://docs.macro-deck.app/sdk/localization/>.

### Logging

- Log through **Serilog**, not `Microsoft.Extensions.Logging`. Inject Serilog's `ILogger` (and
  `.ForContext<T>()`), or use `IntegrationLog`. `UseMacroDeckLogging()` forwards everything to the host's
  log viewer.
- The host stamps integration identity from the authenticated session - a plugin cannot set its own
  attribution, so do not try.
- Logging is rate-limited and a flood is dropped. For a poll loop that fails repeatedly, use
  `FailureEpisodeTracker` rather than a line per tick or a silent Debug-only degrade.
- Structured properties reach the live viewer but are not persisted to the log file - put anything that
  must survive into the message template.

### Comments and style

- `Directory.Build.props` sets `Nullable`, `ImplicitUsings`, `latest-recommended` analysis,
  `EnforceCodeStyleInBuild` and `CS8602` as an error. Build warning-free; do not relax these to make a
  build pass.
- C# in `src/` is tab-indented. Match the surrounding file rather than reformatting it.
- Suppress a diagnostic with the narrowest scope that fits and **always with a reason** on the
  `#pragma` or the `NoWarn` entry.
- Comments explain non-obvious constraints - a race, a protocol rule, why a shape was chosen - not what
  the code already says.
- Write comments in English, regardless of the language used in chat or commit discussion.
- No decorative comment formatting - no ASCII dividers, banners, box-drawing, or emoji. A comment is a
  plain sentence, not a header.
- Keep comments as short as the constraint allows, and prefer no comment at all. Only write one for
  genuinely non-obvious or complex logic (a race, a workaround, a protocol quirk) - never to restate what
  a well-named method or property already says.
- Method and property names must be self-explanatory. If a name needs a comment to be understood, rename
  it instead of documenting it.
- No em dashes in code, comments, commit messages or documentation - use a period, comma or parenthesis
  instead.

### Code quality

- Follow DRY: extract shared logic instead of duplicating it across actions, providers or config flow
  steps. Do not extract on the first occurrence of similar code; do so once a real duplication pattern
  emerges.
- Keep a clean architecture: respect the separation between the integration, actions, config flow and
  provider-specific code. Don't reach across those boundaries or leak provider-specific types into
  shared SDK-facing surfaces.
- Consider performance: avoid unnecessary allocations, synchronous blocking, or repeated expensive work
  in hot paths (invocation handlers, poll loops).
- Dispose everything that owns unmanaged or long-lived resources (`HttpClient` handlers, subscriptions,
  timers, cancellation token registrations, websocket connections). Prefer `IAsyncDisposable`/`IDisposable`
  and DI-managed lifetimes over manual lifecycle management. Watch for event-handler subscriptions that
  outlive their subscriber - a common source of memory leaks in long-running plugin processes.

## Verifying a change

Run the ones that apply, in this order, before calling a change done:

```bash
dotnet build
```

```bash
dotnet test
```

For an interactive verification, start the installed Macro Deck desktop app and debug the plugin with
the **Macro Deck - Real Host** `.NET` launch profile. Keep the profile secret-free; supply a first-run
enrollment token only through the project's local .NET User Secrets and remove it after registration.
Do not add a second run configuration or a CLI/executable startup path.

```bash
macrodeck-plugin test --project src/GHubBatteries --report markdown --output conformance.md
```

The conformance suite drives a real session: capability contracts, invocation and cancellation semantics,
reconnect and resume, the reserved endpoints, logging limits. Exit `0` conformant, `1` the plugin is
wrong, `2` usage error, `3` input unreadable, `4` cancelled - `1` and `3` are deliberately distinct. Run
it after any change to capability shape, cancellation handling or the manifest, and treat a Required
check going from pass to fail as a blocking regression. Most checks `SKIP` until the plugin declares
capabilities.

The CLI version must equal `MacroDeckSdkVersion`.

## Packing a plugin release

```bash
macrodeck-plugin build --source src/GHubBatteries --output ./artifacts
macrodeck-plugin inspect --artifact ./artifacts/<id>-<version>.macroDeckPlugin
```

`build` reads `macrodeck-build.json`, publishes each runtime identifier the manifest declares into its
`runtimes/<rid>/` slot and packs the result. `--rid <rid>` builds one platform, for a CI matrix job.

A `dotnet build -c Release` output is *not* packable: the manifest points at `runtimes/<rid>/`, which
only `build` assembles, so `validate`/`pack` against `bin/Release/net10.0` fails on a missing entrypoint.
Adding a platform means adding it to `entrypoints` **and** `macrodeck-build.json`.

Plugins are **framework-dependent** by default: each entrypoint names `runtimes/<rid>/<Name>.dll` with
`"runtime": { "kind": "FrameworkDependent", "dotnetVersion": "10.0" }`, and each target publishes with
`--self-contained false -p:UseAppHost=false`. Macro Deck ships a .NET 10 runtime (ASP.NET Core
included) with the host and runs the plugin on it, which is also why it appears as `dotnet` in process
lists. Go self-contained only for a runtime Macro Deck does not ship: remove the `runtime` block, point
`executable` at the apphost (no `.dll`; `.exe` on Windows) and publish with `--self-contained true`.
Keep the manifest and `macrodeck-build.json` on the same pairing; mixing them fails validation.

Packing validates first, recomputes every `files[]` digest from disk and fills in `languages` from
`Localization/`, discarding whatever the source manifest declared - so never hand-maintain either.
Signing happens *after* packing, against the packed manifest; sign earlier and the digest will not
match.

## Workflow

- Work on a branch, not directly on `main`. Use `feature/`, `fix/`, `refactor/`, `chore/`, `docs/` or
  `ci/` with a short kebab-case description, and an issue number where one exists.
- A release is a published GitHub release; `.github/workflows/release.yaml` hands it to the Creator Portal.
  The tag is the version.
- Keep changes focused; no unrelated reformatting.
- Do not push or open a pull request unless asked.
- Do not add AI attribution or co-author trailers.
- Update `README.md` when the build, run, packaging or capability story changes. Update this file when a
  rule here stops being true.

## Third-party notices

`THIRD-PARTY-NOTICES` lists what the published package ships. Regenerate it whenever a dependency changes:
run `macrodeck-plugin build`, then the plugin creator's `inventory-dependencies.py` on
`src/GHubBatteries/bin/publish/win-x64` with `--notices`, and keep the hand-written "Bundled native
libraries" section for SQLite.
