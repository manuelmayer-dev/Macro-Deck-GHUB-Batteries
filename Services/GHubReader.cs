using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SuchByte.GHUBBatteries.DataTypes;
using SuchByte.GHUBBatteries.Repository;
using SuchByte.MacroDeck.Logging;
using SuchByte.MacroDeck.Variables;

namespace SuchByte.GHUBBatteries.Services;

public class GHubReader
{
    private const int UpdateInterval = 5;

    public static void Initialize()
    {
        Task.Run(async () => await DoWork());
    }

    private static async Task DoWork()
    {
        while (true)
        {
            await UpdateBatteryInformation();
            await Task.Delay(TimeSpan.FromSeconds(UpdateInterval));
        }
    }

    private static async Task UpdateBatteryInformation()
    {
        List<DeviceBatteryData> data;

        try
        {
            data = await GHubRepository.GetBatteryData().ToListAsync();
        }
        catch (Exception ex)
        {
            MacroDeckLogger.Error(Main.Instance, $"Error while reading G HUB database\n{ex.Message}");
            return;
        }

        foreach (var deviceBatteryData in data)
        {
            UpdateVariable(deviceBatteryData);
        }
    }

    private static void UpdateVariable(DeviceBatteryData deviceBatteryData)
    {
        var deviceName = deviceBatteryData.DeviceName;
        var batteryPercentage = Math.Round(deviceBatteryData.Percentage, 0);
        var charging = deviceBatteryData.IsCharging;
        var millivolts = deviceBatteryData.Millivolts;
        VariableManager.SetValue($"{deviceName}_battery_level", batteryPercentage, VariableType.Integer, Main.Instance,
            null);
        VariableManager.SetValue($"{deviceName}_charging", charging, VariableType.Bool, Main.Instance, null);
        VariableManager.SetValue($"{deviceName}_battery_millivolts", millivolts, VariableType.Integer, Main.Instance,
            null);
    }
}