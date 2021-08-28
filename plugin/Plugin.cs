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
        private readonly WindowSystem windowSystem = new("TriadBuddy");
        private readonly Window windowStatus;

        private readonly UIReaderTriadGame uiReaderGame;
        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly UIReaderTriadCardList uiReaderCardList;
        private readonly Solver solver;
        private readonly GameDataLoader dataLoader;
        private readonly PluginOverlays overlays;

        // fallback option in case profile reader breaks
        private bool canUseProfileReader = true;

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui, DataManager dataManager, SigScanner sigScanner)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.framework = framework;

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            GameCardDB.Get().memReader = new UnsafeReaderTriadCards(sigScanner);

            solver = new Solver();
            solver.profileGS = canUseProfileReader ? new UnsafeReaderProfileGS(gameGui) : null;

            uiReaderGame = new UIReaderTriadGame(gameGui);
            uiReaderGame.OnUIStateChanged += (state) => solver.UpdateGame(state);

            uiReaderPrep = new UIReaderTriadPrep(gameGui);
            uiReaderPrep.shouldScanDeckData = (solver.profileGS != null) && !solver.profileGS.HasErrors;
            uiReaderPrep.OnUIStateChanged += (state) => solver.UpdateDecks(state);

            uiReaderCardList = new UIReaderTriadCardList(gameGui);

            overlays = new PluginOverlays(solver, uiReaderGame, uiReaderPrep);
            windowStatus = new PluginWindowStatus(solver, uiReaderGame, uiReaderPrep);
            windowSystem.AddWindow(windowStatus);

            windowSystem.AddWindow(new PluginWindowDeckEval(solver, uiReaderPrep));
            windowSystem.AddWindow(new PluginWindowCardInfo(uiReaderCardList, gameGui));
            windowSystem.AddWindow(new PluginWindowCardSearch(uiReaderCardList));

            pluginInterface.UiBuilder.Draw += OnDraw;
            commandManager.AddHandler("/triadbuddy", new(OnCommand) { HelpMessage = $"Show state of {Name} plugin." });
            framework.Update += Framework_OnUpdateEvent;
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
            windowStatus.IsOpen = true;
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
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
