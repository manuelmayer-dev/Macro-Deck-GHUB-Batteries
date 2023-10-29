using System.Text.Json.Serialization;

namespace SuchByte.GHUBBatteries.DataTypes;

public class DeviceBatteryData
{
    [JsonIgnore]
    public string DeviceName { get; set; }
    
    [JsonPropertyName("isCharging")]
    public bool IsCharging { get; set; }

    [JsonPropertyName("millivolts")]
    public int Millivolts { get; set; }

    [JsonPropertyName("percentage")]
    public float Percentage { get; set; }
}