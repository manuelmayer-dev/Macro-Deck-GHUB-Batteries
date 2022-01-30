using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace SuchByte.GHUBBatteries.Models
{
    public class GHubDeviceModel
    {
        [JsonProperty(PropertyName = "name")]
        public string DeviceName { get; set; }

    }
}
