using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Drawing;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TriadBuddyPlugin
{
    // It is good to have this be disposable in general, in case you ever need it
    // to do any cleanup
    class PluginUI : IDisposable
    {
        private GameUI gameUI;
        private Solver solver;
        private string pluginName;

        private bool isDebugVisible = true;

        private bool isVisible = false;
        public bool IsVisible { get => isVisible; set { isVisible = value; } }


        public PluginUI(string pluginName, GameUI gameUI, Solver solver)
        {
            this.gameUI = gameUI;
            this.solver = solver;
            this.pluginName = pluginName;
        }

        public void Dispose()
        {
        }

        public void Draw()
        {
            // This is our only draw handler attached to UIBuilder, so it needs to be
            // able to draw any windows we might have open.
            // Each method checks its own visibility/state to ensure it only draws when
            // it actually makes sense.
            // There are other ways to do this, but it is generally best to keep the number of
            // draw delegates as low as possible.

            DrawStatusWindow();
#if DEBUG
            DrawDebugWindow();
#endif // DEBUG
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

#if DEBUG
        public void DrawDebugWindow()
        {
            ImGui.SetNextWindowSize(new Vector2(375, 100), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSizeConstraints(new Vector2(375, 100), new Vector2(float.MaxValue, float.MaxValue));
            if (ImGui.Begin("Debug me", ref isDebugVisible, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
            {
                if (gameUI.currentState != null)
                {
                    ImGui.Text($"Move: {gameUI.currentState.move}");
                    ImGui.Text($"Rules: {string.Join(", ", gameUI.currentState.rules)}, red:{string.Join(",", gameUI.currentState.redPlayerDesc)}");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.blueDeck, "Blue");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.redDeck, "Red");

                    ImGui.Separator();
                    DrawCardArray(gameUI.currentState.board, "Board");
                }
                else
                {
                    ImGui.TextColored(new Vector4(0.9f, 0.2f, 0.2f, 1), "Game is not active");
                }

                if (ImGui.Button("Memory snapshot"))
                {
                    DebugSnapshot();
                }
            }
            ImGui.End();
        }

        private string GetCardDesc(GameUI.State.Card card)
        {
            if (card.numU == 0)
                return "hidden";

            string lockDesc = card.isLocked ? " [LOCKED] " : "";
            return $"[{card.numU:X}-{card.numL:X}-{card.numD:X}-{card.numR:X}]{lockDesc}, o:{card.owner}, t:{card.type}, r:{card.rarity}, tex:{card.texturePath}";
        }

        private void DrawCardArray(GameUI.State.Card[] cards, string prefix)
        {
            for (int idx = 0; idx < cards.Length; idx++)
            {
                if (cards[idx].isPresent)
                {
                    ImGui.Text($"{prefix}[{idx}]: {GetCardDesc(cards[idx])}");
                }
            }
        }

        private void DebugSnapshot()
        {
            try
            {
                string fname = string.Format(@"D:\temp\snap\{0:00}{1:00}{2:00}", DateTime.Now.Hour, DateTime.Now.Minute, DateTime.Now.Second);
                string fnameLog = fname + ".log";
                string fnameJpg = fname + ".jpg";

                if (File.Exists(fnameLog)) { File.Delete(fnameLog); }
                if (File.Exists(fnameJpg)) { File.Delete(fnameJpg); }

                if (gameUI.addonPtr != IntPtr.Zero)
                {
                    using (var file = File.Create(fnameLog))
                    {
                        int size = Marshal.SizeOf(typeof(AddonTripleTriad));
                        byte[] memoryBlock = new byte[size];
                        Marshal.Copy(gameUI.addonPtr, memoryBlock, 0, memoryBlock.Length);
                        file.Write(memoryBlock, 0, memoryBlock.Length);
                        file.Close();
                    }

                    PluginLog.Log("saved: {0}", fname);
                }

                var screen1Size = new Size(2560, 1440);
                var screen2Size = new Size(1920, 1080);
                var bitmap = new Bitmap(screen2Size.Width, screen2Size.Height);

                using (var g = Graphics.FromImage(bitmap))
                {
                    g.CopyFromScreen(screen1Size.Width, screen1Size.Height - screen2Size.Height, 0, 0, screen2Size);
                }

                var resizedBitmap = new Bitmap(bitmap, new Size(screen2Size.Width / 2, screen2Size.Height / 2));
                resizedBitmap.Save(fnameJpg);

                bitmap.Dispose();
                resizedBitmap.Dispose();
            }
            catch (Exception ex)
            {
                PluginLog.LogError(ex, "oops!");
            }
        }
#endif // DEBUG
    }
}
