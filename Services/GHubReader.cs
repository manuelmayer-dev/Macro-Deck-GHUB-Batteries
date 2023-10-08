using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using SuchByte.GHUBBatteries.Models;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Variables;

namespace SuchByte.GHUBBatteries.Services;

public class GHubReader
{
    private static readonly string GHubPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
    private static readonly string GHubSettingsDatabasePath = Path.Combine(GHubPath, "settings.db");

    private const string GHubSettingsDatabaseBatterySection = "percentage";

    private const int RefreshTimerIntervalSec = 10;

    private static Timer _refreshTimer;

    public static void Initialize()
    {
        _refreshTimer = new Timer()
        {
            Interval = RefreshTimerIntervalSec * 1000,
            Enabled = true
        };
        _refreshTimer.Elapsed += RefreshTimer_Elapsed;
        _refreshTimer.Start();
        Task.Run(RefreshStats);
    }

    private static void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
    {
        Task.Run(RefreshStats);
    }

    private static void RefreshStats()
    {
        try
        {
            if (!Directory.Exists(GHubPath))
            {
                MacroDeckLogger.Error(Main.Instance, $"G HUB folder not found at: {GHubPath}");
                _refreshTimer.Stop();
                return;
            }

            var settings = GHubDatabaseReader.DatabaseToJObject(GHubSettingsDatabasePath);
            if (settings == null)
            {
                MacroDeckLogger.Error(Main.Instance, $"Error while reading G HUB database");
                return;
            }

            var properties = settings.Properties().Where(p => p.Name.Contains("battery")).ToList();
            foreach (var property in properties)
            {
                var splitName = property.Name.Split('/');
                if (splitName.Length != 3 || splitName[2] != GHubSettingsDatabaseBatterySection)
                {
                    continue;
                }

                UpdateVariable(new GHubDeviceModel { DeviceName = splitName[1] },
                    property.Value.ToObject<GHubDeviceBatteryModel>());
            }
        }
        catch (Exception ex)
        {
            MacroDeckLogger.Error(Main.Instance,
                $"Error while parsing settings: {ex.Message + Environment.NewLine + ex.StackTrace}");
            _refreshTimer.Stop();
        }
    }

    private static void UpdateVariable(GHubDeviceModel gHubDevice, GHubDeviceBatteryModel gHubDeviceBattery)
    {
        MacroDeckLogger.Trace(Main.Instance,
            $"Updating variable for {gHubDevice.DeviceName}: {gHubDeviceBattery.BatteryPercentage}%");
        VariableManager.SetValue(gHubDevice.DeviceName + "_battery_level",
            Math.Round(gHubDeviceBattery.BatteryPercentage, 0), VariableType.Integer, Main.Instance, null);
        VariableManager.SetValue(gHubDevice.DeviceName + "_charging", gHubDeviceBattery.IsBatteryCharging,
            VariableType.Bool, Main.Instance, null);
    }
}