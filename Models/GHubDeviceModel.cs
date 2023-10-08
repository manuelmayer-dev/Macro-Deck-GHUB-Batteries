using Newtonsoft.Json;

namespace SuchByte.GHUBBatteries.Models;

public class GHubDeviceModel
{
    [JsonProperty(PropertyName = "name")]
    public string DeviceName { get; set; }

}