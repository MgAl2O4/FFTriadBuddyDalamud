using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Numerics;

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

        public readonly TriadGameUIReader uiReader;
        public readonly Solver solver;
        public readonly GameDataLoader dataLoader;

        private uint cachedCardColor;
        private Vector2 cachedCardPos;
        private Vector2 cachedCardSize;
        private Vector2 cachedBoardPos;
        private Vector2 cachedBoardSize;
        private bool hasCachedOverlay;

        public Plugin(DalamudPluginInterface pluginInterface, Framework framework, CommandManager commandManager, GameGui gameGui, DataManager dataManager)
        {
            this.pluginInterface = pluginInterface;
            this.commandManager = commandManager;
            this.framework = framework;

            solver = new Solver();
            solver.OnMoveChanged += Solver_OnMoveChanged;

            uiReader = new TriadGameUIReader(gameGui);
            uiReader.OnChanged += (state) => solver.Update(state);

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            pluginInterface.UiBuilder.Draw += OnDraw;
            commandManager.AddHandler("/triadbuddy", new(OnCommand) { HelpMessage = $"Show state of {Name} plugin." });

            windowStatus = new PluginStatusWindow() { solver = solver, uiReader = uiReader };
            windowSystem.AddWindow(windowStatus);

            framework.OnUpdateEvent += Framework_OnUpdateEvent;
        }

        public void Dispose()
        {
            commandManager.RemoveHandler("/triadbuddy");
            windowSystem.RemoveAllWindows();
            framework.OnUpdateEvent -= Framework_OnUpdateEvent;
            pluginInterface.Dispose();
        }

        private void OnCommand(string command, string args)
        {
            windowStatus.IsOpen = true;
        }

        private void OnDraw()
        {
            windowSystem.Draw();

            if (hasCachedOverlay)
            {
                var useCardPos = cachedCardPos + ImGuiHelpers.MainViewport.Pos;
                var useBoardPos = cachedBoardPos + ImGuiHelpers.MainViewport.Pos;

                var drawList = ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport);
                drawList.AddRect(useCardPos, useCardPos + cachedCardSize, cachedCardColor, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
                drawList.AddRect(useBoardPos, useBoardPos + cachedBoardSize, 0xFFFFFF00, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
            }
        }

        private void Solver_OnMoveChanged(bool hasMove)
        {
            hasCachedOverlay = hasMove;
            if (hasMove)
            {
                cachedCardColor =
                    (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueWins) ? 0xFF00FF00 :
                    (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueDraw) ? 0xFF00D7FF :
                    0xFF0000FF;

                (cachedCardPos, cachedCardSize) = uiReader.GetBlueCardPosAndSize(solver.moveCardIdx);
                (cachedBoardPos, cachedBoardSize) = uiReader.GetBoardCardPosAndSize(solver.moveBoardIdx);
            }
        }

        private void Framework_OnUpdateEvent(Framework framework)
        {
            try
            {
                // TODO: async? run every X ms? - check low spec perf, seems to be negligible
                if (dataLoader.IsDataReady)
                {
                    uiReader.Update();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }
    }
}
