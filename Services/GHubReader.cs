using Newtonsoft.Json.Linq;
using SuchByte.GHUBBatteries.Models;
using SuchByte.GHUBBatteries.Services;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Variables;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

namespace SuchByte.GHUBBatteries
{
    public class GHubReader
    {

        private static readonly string _gHubPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
        private static readonly string _gHubSettingsDatabasePath = Path.Combine(_gHubPath, "settings.db");

        private static readonly string _gHubSettingsDatabaseBatterySection = "percentage";

        private static int _refreshTimerIntervalSec = 10;

        private static Timer _refreshTimer;

        public static void Initilize()
        {
            _refreshTimer = new Timer()
            {
                Interval = _refreshTimerIntervalSec * 1000,
                Enabled = true
            };
            _refreshTimer.Elapsed += RefreshTimer_Elapsed;
            _refreshTimer.Start();
            Task.Run(() => RefreshStats());
        }

        private static void RefreshTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Task.Run(() => RefreshStats());
        }

        private static void RefreshStats()
        {
            try
            {
                if (!Directory.Exists(_gHubPath))
                {
                    MacroDeckLogger.Error(Main.Instance, $"G HUB folder not found at: {_gHubPath}");
                    _refreshTimer.Stop();
                    return;
                }

                var settings = GHubDatabaseReader.DatabaseToJObject(_gHubSettingsDatabasePath);
                if (settings == null)
                {
                    MacroDeckLogger.Error(Main.Instance, $"Error while reading G HUB database");
                    return;
                }

                var properties = settings.Properties().Where(p => p.Name.Contains("battery")).ToList();
                foreach (var property in properties)
                {
                    string[] splitName = property.Name.Split('/');
                    if (splitName.Length != 3 || splitName[2] != _gHubSettingsDatabaseBatterySection)
                    {
                        continue;
                    }

                    UpdateVariable(new GHubDeviceModel { DeviceName = splitName[1] }, property.Value.ToObject<GHubDeviceBatteryModel>());
                }
            }
            catch (Exception ex)
            {
                MacroDeckLogger.Error(Main.Instance, $"Error while parsing settings: {ex.Message + Environment.NewLine + ex.StackTrace}");
                _refreshTimer.Stop();
            }
        }

        private static void UpdateVariable(GHubDeviceModel gHubDevice, GHubDeviceBatteryModel gHubDeviceBattery)
        {
            MacroDeckLogger.Trace(Main.Instance, $"Updating variable for {gHubDevice.DeviceName}: {gHubDeviceBattery.BatteryPercentage}%");
            VariableManager.SetValue(gHubDevice.DeviceName + "_battery_level", Math.Round(gHubDeviceBattery.BatteryPercentage, 0), VariableType.Integer, Main.Instance);
            VariableManager.SetValue(gHubDevice.DeviceName + "_charging", gHubDeviceBattery.IsBatteryCharging, VariableType.Bool, Main.Instance);
        }

    }
}
