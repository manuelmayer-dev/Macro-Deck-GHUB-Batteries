using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using SuchByte.MacroDeck.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SuchByte.GHUBBatteries.Services
{
    public static class GHubDatabaseReader
    {
        public static JObject DatabaseToJObject(string fileName)
        {
            if (!File.Exists(fileName))
            {
                MacroDeckLogger.Error(Main.Instance, $"Database file not found at: {fileName}");
                return null;
            }
            try
            {
                using (var connection = new SqliteConnection($"Data Source={fileName}"))
                {
                    connection.Open();
                    string sql = "SELECT FILE FROM DATA ORDER BY _id DESC";
                    using (SqliteCommand command = new SqliteCommand(sql, connection))
                    using (SqliteDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            return JObject.Parse(reader.GetString(0));
                        }
                    }
                    connection.Close();
                }
            }
            catch (Exception ex)
            {
                MacroDeckLogger.Error(Main.Instance, $"Error while reading database: {ex.Message + Environment.NewLine + ex.StackTrace}");
            }
            return null;
        }
    }
}
