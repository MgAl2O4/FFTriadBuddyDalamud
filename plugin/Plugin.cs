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

        public readonly TriadGameUIReader uiReaderGame;
        public readonly TriadPrepUIReader uiReaderPrep;
        public readonly Solver solver;
        public readonly GameDataLoader dataLoader;

        // overlay: solver
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

            uiReaderGame = new TriadGameUIReader(gameGui);
            uiReaderGame.OnChanged += (state) => solver.UpdateGame(state);

            uiReaderPrep = new TriadPrepUIReader(gameGui);
            uiReaderPrep.OnChanged += (state) => solver.UpdateDecks(state);

            dataLoader = new GameDataLoader();
            dataLoader.StartAsyncWork(dataManager);

            pluginInterface.UiBuilder.Draw += OnDraw;
            commandManager.AddHandler("/triadbuddy", new(OnCommand) { HelpMessage = $"Show state of {Name} plugin." });

            windowStatus = new PluginStatusWindow() { solver = solver, uiReaderGame = uiReaderGame, uiReaderPrep = uiReaderPrep };
            windowSystem.AddWindow(windowStatus);

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

            if (hasCachedOverlay)
            {
                var useCardPos = cachedCardPos + ImGuiHelpers.MainViewport.Pos;
                var useBoardPos = cachedBoardPos + ImGuiHelpers.MainViewport.Pos;

                var drawList = ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport);
                drawList.AddRect(useCardPos, useCardPos + cachedCardSize, cachedCardColor, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
                drawList.AddRect(useBoardPos, useBoardPos + cachedBoardSize, 0xFFFFFF00, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
            }
            else if (uiReaderPrep.IsDeckSelection)
            {
                DrawDeckSolverOverlay();
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

                (cachedCardPos, cachedCardSize) = uiReaderGame.GetBlueCardPosAndSize(solver.moveCardIdx);
                (cachedBoardPos, cachedBoardSize) = uiReaderGame.GetBoardCardPosAndSize(solver.moveBoardIdx);
            }
        }

        private void Framework_OnUpdateEvent(Framework framework)
        {
            try
            {
                // TODO: async? run every X ms? - check low spec perf, seems to be negligible
                if (dataLoader.IsDataReady)
                {
                    uiReaderGame.Update();
                    uiReaderPrep.Update();
                }
            }
            catch (Exception ex)
            {
                PluginLog.Error(ex, "state update failed");
            }
        }

        private void DrawDeckSolverOverlay()
        {
            if (solver.preGameDeckChance.Count == uiReaderPrep.cachedState.decks.Count)
            {
                var drawList = ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport);
                const int padding = 5;

                const uint hintColorWin = 0xFF00FF00;
                const uint hintColorDraw = 0xFFFFFF00;
                const uint hintColorLose = 0xFFFF0000;
                var hintTextOffset = new Vector2(padding, padding);

                for (int idx = 0; idx < uiReaderPrep.cachedState.decks.Count; idx++)
                {
                    var deckState = uiReaderPrep.cachedState.decks[idx];
                    var deckChance = solver.preGameDeckChance[idx];

                    bool isSolverReady = deckChance.compScore > 0;
                    var hintText = !isSolverReady ? "..." : deckChance.winChance.ToString("P0");
                    uint hintColor = !isSolverReady ? 0xFFFFFFFF :
                        deckChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueWins ? hintColorWin :
                        deckChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueDraw ? hintColorDraw :
                        hintColorLose;

                    var hintTextSize = ImGui.CalcTextSize(hintText);
                    var hintRectSize = hintTextSize;
                    hintRectSize.X += padding * 2;
                    hintRectSize.Y += padding * 2;

                    var hintPos = deckState.screenPos + ImGuiHelpers.MainViewport.Pos;
                    hintPos.X += padding;
                    hintPos.Y += (deckState.screenSize.Y - hintTextSize.Y) / 2;

                    drawList.AddRectFilled(hintPos, hintPos + hintRectSize, 0x80000000, 5.0f, ImDrawFlags.RoundCornersAll);
                    drawList.AddText(hintPos + hintTextOffset, hintColor, hintText);
                }
            }
        }
    }
}
