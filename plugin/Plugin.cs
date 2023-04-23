using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using MgAl2O4.Utils;
using System;

namespace TriadBuddyPlugin
{
    public class Plugin : IDalamudPlugin
    {
        public string Name => "Triad Buddy";

        private readonly DalamudPluginInterface pluginInterface;
        private readonly CommandManager commandManager;
        private readonly Framework framework;
        private readonly DataManager dataManager;
        private readonly WindowSystem windowSystem = new("TriadBuddy");

        private readonly PluginWindowStatus statusWindow;
        private readonly CommandInfo statusCommand;

        private readonly UIReaderTriadGame uiReaderGame;
        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly UIReaderTriadCardList uiReaderCardList;
        private readonly UIReaderTriadDeckEdit uiReaderDeckEdit;
        private readonly StatTracker statTracker;
        private readonly GameDataLoader dataLoader;
        private readonly UIReaderScheduler uiReaderScheduler;
        private readonly PluginOverlays overlays;
        private readonly Localization locManager;

        public static Localization CurrentLocManager;
        private string[] supportedLangCodes = { "de", "en", "es", "fr", "ja", "ko", "zh" };

        private Configuration configuration { get; init; }

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui, DataManager dataManager, SigScanner sigScanner)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.dataManager = dataManager;
            this.framework = framework;

            configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            configuration.Initialize(pluginInterface);

            // prep utils
            var myAssemblyName = GetType().Assembly.GetName().Name;
            locManager = new Localization($"{myAssemblyName}.assets.loc.", "", true);            // res stream format: TriadBuddy.assets.loc.en.json
            locManager.SetupWithLangCode(pluginInterface.UiLanguage);
            CurrentLocManager = locManager;

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            SolverUtils.CreateSolvers();
            SolverUtils.solverPreGameDecks.profileGS = configuration.CanUseProfileReader ? new UnsafeReaderProfileGS(gameGui) : null;

            statTracker = new StatTracker(configuration);

            // prep data scrapers
            uiReaderGame = new UIReaderTriadGame(gameGui);
            uiReaderGame.OnUIStateChanged += (state) => SolverUtils.solverGame.UpdateGame(state);

            uiReaderPrep = new UIReaderTriadPrep(gameGui);
            uiReaderPrep.shouldScanDeckData = (SolverUtils.solverPreGameDecks.profileGS == null) || SolverUtils.solverPreGameDecks.profileGS.HasErrors;
            uiReaderPrep.OnUIStateChanged += (state) => SolverUtils.solverPreGameDecks.UpdateDecks(state);

            uiReaderCardList = new UIReaderTriadCardList(gameGui);
            uiReaderDeckEdit = new UIReaderTriadDeckEdit(gameGui);

            var uiReaderMatchResults = new UIReaderTriadResults(gameGui);
            uiReaderMatchResults.OnUpdated += (state) => statTracker.OnMatchFinished(SolverUtils.solverGame, state);

            uiReaderScheduler = new UIReaderScheduler(gameGui);
            uiReaderScheduler.AddObservedAddon(uiReaderGame);
            uiReaderScheduler.AddObservedAddon(uiReaderPrep.uiReaderMatchRequest);
            uiReaderScheduler.AddObservedAddon(uiReaderPrep.uiReaderDeckSelect);
            uiReaderScheduler.AddObservedAddon(uiReaderCardList);
            uiReaderScheduler.AddObservedAddon(uiReaderDeckEdit);
            uiReaderScheduler.AddObservedAddon(uiReaderMatchResults);

            var memReaderTriadFunc = new UnsafeReaderTriadCards(sigScanner);
            GameCardDB.Get().memReader = memReaderTriadFunc;
            GameNpcDB.Get().memReader = memReaderTriadFunc;

            uiReaderDeckEdit.unsafeDeck = new UnsafeReaderTriadDeck(sigScanner);

            // prep UI
            overlays = new PluginOverlays(uiReaderGame, uiReaderPrep, configuration);
            statusWindow = new PluginWindowStatus(dataManager, uiReaderGame, uiReaderPrep, configuration);
            windowSystem.AddWindow(statusWindow);

            var npcStatsWindow = new PluginWindowNpcStats(statTracker);
            var deckOptimizerWindow = new PluginWindowDeckOptimize(dataManager, SolverUtils.solverDeckOptimize, uiReaderDeckEdit, configuration);
            var deckEvalWindow = new PluginWindowDeckEval(SolverUtils.solverPreGameDecks, uiReaderPrep, deckOptimizerWindow, npcStatsWindow);
            deckOptimizerWindow.OnConfigRequested += () => OnOpenConfig();
            windowSystem.AddWindow(deckEvalWindow);
            windowSystem.AddWindow(deckOptimizerWindow);
            windowSystem.AddWindow(npcStatsWindow);

            windowSystem.AddWindow(new PluginWindowCardInfo(uiReaderCardList, gameGui));
            windowSystem.AddWindow(new PluginWindowCardSearch(uiReaderCardList, gameGui, configuration, npcStatsWindow));
            windowSystem.AddWindow(new PluginWindowDeckSearch(uiReaderDeckEdit, gameGui, configuration));

            // prep plugin hooks
            statusCommand = new(OnCommand) { HelpMessage = string.Format(Localization.Localize("Cmd_Status", "Show state of {0} plugin"), Name) };
            commandManager.AddHandler("/triadbuddy", statusCommand);

            pluginInterface.LanguageChanged += OnLanguageChanged;
            pluginInterface.UiBuilder.Draw += OnDraw;
            pluginInterface.UiBuilder.OpenConfigUi += OnOpenConfig;

            framework.Update += Framework_OnUpdateEvent;
        }

        private void OnLanguageChanged(string langCode)
        {
            // check if resource is available, will cause exception if trying to load empty json
            if (Array.Find(supportedLangCodes, x => x == langCode) != null)
            {
                locManager.SetupWithLangCode(langCode);
            }
            else
            {
                locManager.SetupWithFallbacks();
            }

            statusCommand.HelpMessage = string.Format(Localization.Localize("Cmd_Status", "Show state of {0} plugin"), Name);
        }

        public void Dispose()
        {
            commandManager.RemoveHandler("/triadbuddy");
            windowSystem.RemoveAllWindows();
            framework.Update -= Framework_OnUpdateEvent;
        }

        private void OnCommand(string command, string args)
        {
            statusWindow.IsOpen = true;
        }

        private void OnDraw()
        {
            windowSystem.Draw();
            overlays.OnDraw();
        }

        private void OnOpenConfig()
        {
            statusWindow.showConfigs = true;
            statusWindow.IsOpen = true;
        }

        private void Framework_OnUpdateEvent(Framework framework)
        {
            try
            {
                if (dataLoader.IsDataReady)
                {
                    float deltaSeconds = (float)framework.UpdateDelta.TotalSeconds;
                    uiReaderScheduler.Update(deltaSeconds);
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
