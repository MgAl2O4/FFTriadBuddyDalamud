using Dalamud;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
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

        private readonly Window statusWindow;
        private readonly CommandInfo statusCommand;

        private readonly UIReaderTriadGame uiReaderGame;
        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly UIReaderTriadCardList uiReaderCardList;
        private readonly UIReaderTriadDeckEdit uiReaderDeckEdit;
        private readonly Solver solver;
        private readonly GameDataLoader dataLoader;
        private readonly PluginOverlays overlays;
        private readonly Localization locManager;

        public static Localization CurrentLocManager;
        private string[] supportedLangCodes = { "en", "zh" };

        // fallback option in case profile reader breaks
        private bool canUseProfileReader = true;

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui, DataManager dataManager, SigScanner sigScanner)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.dataManager = dataManager;
            this.framework = framework;

            // prep utils
            locManager = new Localization("assets/loc", "", true);
            locManager.SetupWithLangCode(pluginInterface.UiLanguage);
            CurrentLocManager = locManager;

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            solver = new Solver();
            solver.profileGS = canUseProfileReader ? new UnsafeReaderProfileGS(gameGui) : null;

            // prep data scrapers
            uiReaderGame = new UIReaderTriadGame(gameGui);
            uiReaderGame.OnUIStateChanged += (state) => solver.UpdateGame(state);

            uiReaderPrep = new UIReaderTriadPrep(gameGui);
            uiReaderPrep.shouldScanDeckData = (solver.profileGS == null) || solver.profileGS.HasErrors;
            uiReaderPrep.OnUIStateChanged += (state) => solver.UpdateDecks(state);

            uiReaderCardList = new UIReaderTriadCardList(gameGui);
            uiReaderDeckEdit = new UIReaderTriadDeckEdit(gameGui);

            GameCardDB.Get().memReader = new UnsafeReaderTriadCards(sigScanner);

            // prep UI
            overlays = new PluginOverlays(solver, uiReaderGame, uiReaderPrep);
            statusWindow = new PluginWindowStatus(dataManager, solver, uiReaderGame, uiReaderPrep);
            windowSystem.AddWindow(statusWindow);

            var deckOptimizerWindow = new PluginWindowDeckOptimize(dataManager, solver, uiReaderDeckEdit);
            var deckEvalWindow = new PluginWindowDeckEval(solver, uiReaderPrep, deckOptimizerWindow);
            windowSystem.AddWindow(deckEvalWindow);
            windowSystem.AddWindow(deckOptimizerWindow);

            windowSystem.AddWindow(new PluginWindowCardInfo(uiReaderCardList, gameGui));
            windowSystem.AddWindow(new PluginWindowCardSearch(uiReaderCardList));

            // prep plugin hooks
            statusCommand = new(OnCommand);
            commandManager.AddHandler("/triadbuddy", statusCommand);

            pluginInterface.LanguageChanged += OnLanguageChanged;
            pluginInterface.UiBuilder.Draw += OnDraw;

            framework.Update += Framework_OnUpdateEvent;

            // keep at the end to update everything created here
            locManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
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
        }

        private void CacheLocalization()
        {
            statusCommand.HelpMessage = string.Format(Localization.Localize("Cmd_Status", "Show state of {0} plugin"), Name);
        }

        public void Dispose()
        {
            commandManager.RemoveHandler("/triadbuddy");
            windowSystem.RemoveAllWindows();
            framework.Update -= Framework_OnUpdateEvent;
            pluginInterface.Dispose();
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

        private void Framework_OnUpdateEvent(Framework framework)
        {
            try
            {
                if (dataLoader.IsDataReady)
                {
                    uiReaderGame.Update();
                    uiReaderPrep.Update();
                    uiReaderCardList.Update();
                    uiReaderDeckEdit.Update();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
