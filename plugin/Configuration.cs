using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace TriadBuddyPlugin
{
    [Serializable]
    public class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 0;

        public bool ShowSolverHintsInGame { get; set; } = true;
        public bool CanUseProfileReader { get; set; } = true;

        [NonSerialized]
        private DalamudPluginInterface pluginInterface;

        public void Initialize(DalamudPluginInterface pluginInterface)
        {
            this.pluginInterface = pluginInterface;
        }

        public void Save()
        {
            pluginInterface?.SavePluginConfig(this);
        }
    }
}
