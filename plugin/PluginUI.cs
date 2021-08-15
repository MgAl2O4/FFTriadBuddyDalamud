using Dalamud.Interface;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private GameUI gameUI;
        private Solver solver;
        private string pluginName;

        private bool isVisible = false;
        public bool IsVisible { get => isVisible; set { isVisible = value; } }

        private uint cachedCardColor;
        private Vector2 cachedCardPos;
        private Vector2 cachedCardSize;
        private Vector2 cachedBoardPos;
        private Vector2 cachedBoardSize;
        private bool hasCachedOverlay;

        public PluginUI(string pluginName, GameUI gameUI, Solver solver)
        {
            this.gameUI = gameUI;
            this.solver = solver;
            this.pluginName = pluginName;

            if (solver != null && gameUI != null)
            {
                solver.OnMoveChanged += Solver_OnMoveChanged;
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

                (cachedCardPos, cachedCardSize) = gameUI.GetBlueCardPosAndSize(solver.moveCardIdx);
                (cachedBoardPos, cachedBoardSize) = gameUI.GetBoardCardPosAndSize(solver.moveBoardIdx);
            }
        }

        public void Dispose()
        {
            // meh, keep it for now
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawOverlay();
            DrawStatusWindow();
        }

        public void DrawOverlay()
        {
            if (hasCachedOverlay)
            {
                var useCardPos = cachedCardPos + ImGuiHelpers.MainViewport.Pos;
                var useBoardPos = cachedBoardPos + ImGuiHelpers.MainViewport.Pos;

                ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(useCardPos, useCardPos + cachedCardSize, cachedCardColor, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
                ImGui.GetForegroundDrawList(ImGuiHelpers.MainViewport).AddRect(useBoardPos, useBoardPos + cachedBoardSize, 0xFFFFFF00, 5.0f, ImDrawFlags.RoundCornersAll, 5.0f);
            }
        }

        public void DrawStatusWindow()
        {
            if (!IsVisible | gameUI == null)
            {
                return;
            }

            ImGui.SetNextWindowSize(new Vector2(350, 120), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(350, 120), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin(pluginName, ref isVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                var colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
                var colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
                var colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);

                ImGui.Text("Status: ");
                ImGui.SameLine();
                var friendlyDesc =
                    (gameUI.status == GameUI.Status.NoErrors) ? "Active" :
                    (gameUI.status == GameUI.Status.AddonNotFound) ? "Minigame not active" :
                    (gameUI.status == GameUI.Status.AddonNotVisible) ? "Minigame not visible" :
                    gameUI.status.ToString();

                ImGui.TextColored(gameUI.HasErrors() ? colorErr : colorOk, friendlyDesc);

                ImGui.Text("Game data: ");
                ImGui.SameLine();
                int numCards = FFTriadBuddy.TriadCardDB.Get().cards.Count;
                int numNpcs = FFTriadBuddy.TriadNpcDB.Get().npcs.Count;
                if (numCards == 0 || numNpcs == 0)
                {
                    ImGui.TextColored(colorErr, "missing! solver disabled");
                }
                else
                {
                    ImGui.Text($"cards: {numCards}, NPCs: {numNpcs}");
                }

                ImGui.Separator();
                ImGui.Text("Solver.NPC:");
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, (solver.currentNpc != null) ? solver.currentNpc.Name.GetLocalized() : "--");

                ImGui.Text("Solver.Move:");
                ImGui.SameLine();
                if (solver.hasMove)
                {
                    var useColor =
                        (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueWins) ? colorOk :
                        (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueDraw) ? colorYellow :
                        colorErr;

                    string humanCard = (solver.moveCard != null) ? solver.moveCard.Name.GetLocalized() : "??";
                    int boardX = solver.moveBoardIdx % 3;
                    int boardY = solver.moveBoardIdx / 3;
                    string humanBoardX = boardX == 0 ? "left" : (boardX == 1) ? "center" : "right";
                    string humanBoardY = boardY == 0 ? "top" : (boardY == 1) ? "center" : "bottom";
                    string humanBoard = (solver.moveBoardIdx == 4) ? "center" : $"{humanBoardY}, {humanBoardX}";

                    // 1 based indexing for humans, disgusting
                    ImGui.TextColored(useColor, $"[{solver.moveCardIdx + 1}] {humanCard} => {humanBoard}");
                }
                else
                {
                    ImGui.TextColored(colorYellow, "--");
                }
            }
            ImGui.End();
        }
    }
}
