# GHub Batteries

A Macro Deck 3 plugin that shows the battery level of your wireless Logitech devices (headsets, mice,
keyboards) as Logitech G HUB reports them.

GHub Batteries is a community plugin. It is not affiliated with, endorsed by or maintained by Logitech.

## Features

- Every wireless device G HUB reports a battery level for appears in Macro Deck's battery catalog: in
  the built-in Battery widget, in the battery variables and for every other plugin that reads batteries.
- Level in percent and, where the device reports it, whether it is charging.
- Devices G HUB stops reporting, or all devices while G HUB is not available, stay listed as not
  connected with their last level instead of disappearing.
- A clear notice in the integration's detail view when G HUB is not installed.

## Requirements

- Macro Deck 3 on **Windows** (x64) or **macOS** (Apple silicon).
- [Logitech G HUB](https://www.logitechg.com/innovation/g-hub.html) installed and running. The plugin
  reads G HUB's local settings database, which G HUB only updates while it runs; it does not need G HUB
  to be signed in.

## Usage

Install the plugin from the Macro Deck Store. There is nothing to configure: within a few seconds your
devices show up in the Battery widget's device list.

### Variables

Macro Deck creates the variables for each device itself and lists them under this plugin:

| Variable | Meaning |
| --- | --- |
| `battery_<name>_<hash>_level` | Battery level in percent |
| `battery_<name>_<hash>_charging` | Whether the device is charging |
| `battery_<name>_<hash>_connected` | Whether the device is currently reported by G HUB |
| `battery_<name>_<hash>_state` | Charge state, `disconnected` while not connected |
| `battery_<name>_<hash>_time_remaining_minutes`, `_time_to_full_minutes`, `_health` | Not reported by G HUB |

`<name>` is the device name as G HUB stores it, for example `proxwirelessheadset`. See
[Battery variables](https://docs.macro-deck.app/guide/concepts/#battery-variables) for details.

### Upgrading from the Macro Deck 2 plugin

The Macro Deck 2 plugin (`SuchByte.GHUBBatteries`) created `<device>_battery_level`, `<device>_charging`
and `<device>_battery_millivolts`. Those names do not exist any more: bind buttons and widgets to the
`battery_<name>_<hash>_*` variables above, or use the Battery widget. The millivolt value is no longer
provided.

## How it works and what it accesses

Every five seconds the plugin reads G HUB's settings database in read-only mode, extracts the
`battery/<device>/percentage` entries and publishes only the devices whose values changed. The database
is at:

- Windows: `%LOCALAPPDATA%\LGHUB\settings.db`
- macOS: `~/Library/Application Support/LGHUB/settings.db`

- **No network access.** Nothing leaves your computer; the data goes only to Macro Deck.
- **Read-only.** The plugin opens the database read-only and never changes G HUB's settings.
- **What other plugins see.** Macro Deck shares battery devices with every plugin. The plugin publishes
  only the device key, level and charge state; no serial numbers or account data.

## Limitations

- Device names are G HUB's internal keys (for example `proxwirelessheadset`), because the battery entry
  does not carry a display name.
- G HUB can keep the last battery entry after a device is switched off, so such a device may keep
  showing its last level as connected until G HUB updates or removes the entry.
- While G HUB is closed, or while its database cannot be read, the last known values stay shown as
  connected; they are not live until G HUB runs again.
- G HUB only stores a charging flag while a device charges, so a device without one shows as
  discharging. An unreadable flag shows an unknown charge state.
- The device kind (headphones, mouse, keyboard, speaker) is guessed from the key and falls back to
  `other`.

## Development

Requirements: .NET 10 SDK and the Macro Deck plugin CLI of the same version as the SDK
(`MacroDeckSdkVersion` in [Directory.Packages.props](Directory.Packages.props)).

```bash
dotnet tool install --global MacroDeck.Plugin.Cli --version 3.0.0-beta.17
```

```bash
dotnet build -c Release -warnaserror
```

```bash
dotnet test -c Release
```

```bash
macrodeck-plugin test --project src/GHubBatteries
```

```bash
macrodeck-plugin build --source src/GHubBatteries --output ./artifacts
```

To run the plugin against your local Macro Deck, start Macro Deck and use the **Macro Deck - Real Host**
launch profile, or:

```bash
macrodeck-plugin run --project src/GHubBatteries
```

Project layout:

```
src/GHubBatteries/
  GHubBatteriesIntegration.cs   battery provider and issue reporting
  Batteries/                    polling, change detection and mapping to Macro Deck battery devices
  GHub/                         G HUB settings location per OS, database access and JSON parsing
tests/GHubBatteries.Tests/      unit tests and plugin harness tests
```

Releases are published through the Macro Deck Creator Portal by publishing a GitHub release; see
[.github/workflows/release.yaml](.github/workflows/release.yaml).

## License

MIT, see [LICENSE](LICENSE). Third-party components and their licenses are listed in
[THIRD-PARTY-NOTICES](THIRD-PARTY-NOTICES), generated from the published dependency graph.

Logitech, Logitech G and G HUB are trademarks of Logitech.
