using Newtonsoft.Json;

namespace SuchByte.GHUBBatteries.Models;

public class GHubDeviceBatteryModel
{
    [JsonProperty(PropertyName = "isCharging")]
    public bool IsBatteryCharging { get; set; }

    [JsonProperty(PropertyName = "millivolts")]
    public int BatteryMillivolts { get; set; }

    [JsonProperty(PropertyName = "percentage")]
    public double BatteryPercentage { get; set; }
}