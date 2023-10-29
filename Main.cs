using SuchByte.MacroDeck.Plugins;
using SuchByte.GHUBBatteries.Services;

namespace SuchByte.GHUBBatteries;

public class Main : MacroDeckPlugin
{
    internal static MacroDeckPlugin Instance { get; set; }

    public Main() {
        Instance = this;
    }

    public override void Enable()
    {
        GHubReader.Initialize();
    }

}