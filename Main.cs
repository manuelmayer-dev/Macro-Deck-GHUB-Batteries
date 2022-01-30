using SuchByte.MacroDeck.Plugins;
using System;
using System.Drawing;

namespace SuchByte.GHUBBatteries
{
    public class Main : MacroDeckPlugin
    {
        internal static MacroDeckPlugin Instance { get; set; }

        public override string Description => "This plugin creates variables of the battery state of your Logitech devices";

        public override Image Icon => Properties.Resources.G_HUB_Batteries_Plugin;

        public Main() {
            Instance = this;
        }

        public override void Enable()
        {
            this.Actions = new System.Collections.Generic.List<PluginAction>(); // Just create a empty list because otherwise it causes errors in Macro Deck
            GHubReader.Initilize();
        }

    }
}
