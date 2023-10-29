using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text.Json;
using SuchByte.GHUBBatteries.DataTypes;
using SuchByte.MacroDeck.Logging;

namespace SuchByte.GHUBBatteries.Repository;

public static class GHubRepository
{
    private static readonly string GHubPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LGHUB");
    private static readonly string GHubSettingsDatabasePath = Path.Combine(GHubPath, "settings.db");
    
    private const string BatteryPercentageQuery =
        """
        SELECT json_each.key, json_each.value
        FROM data, json_each(file)
        WHERE LOWER(json_each.key) LIKE 'battery/%/percentage';
        """;
    
    public static async IAsyncEnumerable<DeviceBatteryData> GetBatteryData()
    {
        if (!File.Exists(GHubSettingsDatabasePath))
        {
            MacroDeckLogger.Error(Main.Instance, $"No database found at: {GHubSettingsDatabasePath}");
            yield break;
        }

        await using var connection = new SQLiteConnection($"Data Source={GHubSettingsDatabasePath}");
        connection.Open();
        
        await using (var command = new SQLiteCommand(BatteryPercentageQuery, connection))
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var key = reader.GetString(0);
                var dataJson = reader.GetString(1);
                var data = JsonSerializer.Deserialize<DeviceBatteryData>(dataJson);
                data.DeviceName = key.Split('/')[1];

                yield return data;
            }
        }
        
        connection.Close();
    }
}