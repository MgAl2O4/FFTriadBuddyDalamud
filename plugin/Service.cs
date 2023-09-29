using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace TriadBuddyPlugin
{
    internal class Service
    {
        public static Plugin plugin;

        public static Configuration pluginConfig;

        [PluginService]
        public static DalamudPluginInterface pluginInterface { get; private set; } = null!;

        [PluginService]
        public static IDataManager dataManager { get; private set; } = null!;

        [PluginService]
        public static ICommandManager commandManager { get; private set; } = null!;

        [PluginService]
        public static ISigScanner sigScanner { get; private set; } = null!;

        [PluginService]
        public static IFramework framework { get; private set; } = null!;

        [PluginService]
        public static IGameGui gameGui { get; private set; } = null!;

        [PluginService]
        public static ITextureProvider textureProvider { get; private set; } = null!;

        [PluginService]
        public static IPluginLog logger { get; private set; } = null!;
    }
}
