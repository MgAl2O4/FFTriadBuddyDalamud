using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginStatusWindow : Window, IDisposable
    {
        public TriadGameUIReader uiReaderGame;
        public TriadPrepUIReader uiReaderPrep;
        public Solver solver;

        public PluginStatusWindow() : base("Triad Buddy")
        {
            IsOpen = false;

            Size = new Vector2(350, 120);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public void Dispose()
        {
            // meh
        }

        public override void Draw()
        {
            var colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
            var colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
            var colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);

            ImGui.Text("Status: ");
            ImGui.SameLine();
            var statusDesc =
                uiReaderGame.HasErrors ? uiReaderGame.status.ToString() :
                solver.HasErrors ? solver.status.ToString() :
                (uiReaderGame.status == TriadGameUIReader.Status.AddonNotFound) ? "Minigame not active" :
                (uiReaderGame.status == TriadGameUIReader.Status.AddonNotVisible) ? "Minigame not visible" :
                "Active";

            ImGui.TextColored(uiReaderGame.HasErrors || solver.HasErrors ? colorErr : colorOk, statusDesc);

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

            // context sensitive part
            if (uiReaderPrep.isActive)
            {
                var npcDesc = "--";
                var rulesDesc = "--";

                if (uiReaderPrep.IsDeckSelection)
                {
                    if (solver.preGameNpc != null) { npcDesc = solver.preGameNpc.Name.GetLocalized(); }

                    if (solver.preGameMods.Count > 0)
                    {
                        rulesDesc = "";
                        foreach (var ruleOb in solver.preGameMods)
                        {
                            if (rulesDesc.Length > 0) { rulesDesc += ", "; }
                            rulesDesc += ruleOb.GetLocalizedName();
                        }
                    }
                }
                else
                {
                    npcDesc = uiReaderPrep.cachedState.npc;
                    rulesDesc = string.Join(", ", uiReaderPrep.cachedState.rules);
                }

                ImGui.Separator();
                ImGui.Text("Prep.NPC:");
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, npcDesc);

                ImGui.Text("Prep.Rules:");
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, rulesDesc);
            }
            else
            {
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
        }
    }
}
