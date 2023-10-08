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
        Actions = new System.Collections.Generic.List<PluginAction>(); // Just create a empty list because otherwise it causes errors in Macro Deck
        GHubReader.Initialize();
    }

}