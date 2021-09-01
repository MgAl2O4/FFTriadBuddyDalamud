using Dalamud;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginWindowStatus : Window, IDisposable
    {
        private readonly UIReaderTriadGame uiReaderGame;
        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly Solver solver;

        private string locStatus;
        private string locStatusNotActive;
        private string locStatusPvPMatch;
        private string locStatusActive;
        private string locGameData;
        private string locGameDataError;
        private string locGameDataLog;
        private string locPrepNpc;
        private string locPrepRule;
        private string locGameNpc;
        private string locGameMove;
        private string locGameMoveDisabled;
        private string locBoardX0;
        private string locBoardX1;
        private string locBoardX2;
        private string locBoardY0;
        private string locBoardY1;
        private string locBoardY2;
        private string locBoardCenter;

        public PluginWindowStatus(Solver solver, UIReaderTriadGame uiReaderGame, UIReaderTriadPrep uiReaderPrep) : base("Triad Buddy")
        {
            this.solver = solver;
            this.uiReaderGame = uiReaderGame;
            this.uiReaderPrep = uiReaderPrep;

            IsOpen = false;

            Size = new Vector2(350, 120);
            SizeCondition = ImGuiCond.FirstUseEver;

            Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        public void Dispose()
        {
            // meh
        }

        private void CacheLocalization()
        {
            locStatus = Localization.Localize("ST_Status", "Status:");
            locStatusNotActive = Localization.Localize("ST_StatusNotActive", "Minigame not active");
            locStatusPvPMatch = Localization.Localize("ST_StatusPvP", "PvP match");
            locStatusActive = Localization.Localize("ST_StatusActive", "Active");
            locGameData = Localization.Localize("ST_GameData", "Game data:");
            locGameDataError = Localization.Localize("ST_GameDataError", "missing! solver disabled");
            locGameDataLog = Localization.Localize("ST_GameDataLog", "cards: {0}, npcs: {1}");
            locPrepNpc = Localization.Localize("ST_PrepNpc", "Prep.NPC:");
            locPrepRule = Localization.Localize("ST_PrepRules", "Prep.Rules:");
            locGameNpc = Localization.Localize("ST_GameNpc", "Solver.NPC:");
            locGameMove = Localization.Localize("ST_GameMove", "Solver.Move:");
            locGameMoveDisabled = Localization.Localize("ST_GameMoveDisabled", "disabled");
            locBoardX0 = Localization.Localize("ST_BoardXLeft", "left");
            locBoardX1 = Localization.Localize("ST_BoardXCenter", "center");
            locBoardX2 = Localization.Localize("ST_BoardXRight", "right");
            locBoardY0 = Localization.Localize("ST_BoardYTop", "top");
            locBoardY1 = Localization.Localize("ST_BoardYCenter", "center");
            locBoardY2 = Localization.Localize("ST_BoardYBottom", "bottom");
            locBoardCenter = Localization.Localize("ST_BoardXYCenter", "center");
        }

        public override void Draw()
        {
            var colorErr = new Vector4(0.9f, 0.2f, 0.2f, 1);
            var colorOk = new Vector4(0.2f, 0.9f, 0.2f, 1);
            var colorYellow = new Vector4(0.9f, 0.9f, 0.2f, 1);

            ImGui.Text(locStatus);
            ImGui.SameLine();

            bool isPvPMatch = (uiReaderGame.status == UIReaderTriadGame.Status.PvPMatch) || (solver.status == Solver.Status.FailedToParseNpc);
            var statusDesc =
                isPvPMatch ? locStatusPvPMatch :
                uiReaderGame.HasErrors ? uiReaderGame.status.ToString() :
                solver.HasErrors ? solver.status.ToString() :
                !uiReaderGame.IsVisible ? locStatusNotActive :
                locStatusActive;

            var statusColor =
                isPvPMatch ? colorYellow :
                uiReaderGame.HasErrors || solver.HasErrors ? colorErr :
                colorOk;

            ImGui.TextColored(statusColor, statusDesc);

            ImGui.Text(locGameData);
            ImGui.SameLine();
            int numCards = FFTriadBuddy.TriadCardDB.Get().cards.Count;
            int numNpcs = FFTriadBuddy.TriadNpcDB.Get().npcs.Count;
            bool isGameDataMissing = numCards == 0 || numNpcs == 0;
            if (isGameDataMissing)
            {
                ImGui.TextColored(colorErr, locGameDataError);
            }
            else
            {
                ImGui.Text(string.Format(locGameDataLog, numCards, numNpcs));
            }

            // context sensitive part
            if (uiReaderPrep.HasDeckSelectionUI || uiReaderPrep.HasMatchRequestUI)
            {
                var rulesDesc = "--";

                var npcDesc = (solver.preGameNpc != null) ? solver.preGameNpc.Name.GetLocalized() : uiReaderPrep.cachedState.npc;
                if (solver.preGameMods.Count > 0)
                {
                    rulesDesc = "";
                    foreach (var ruleOb in solver.preGameMods)
                    {
                        if (rulesDesc.Length > 0) { rulesDesc += ", "; }
                        rulesDesc += ruleOb.GetLocalizedName();
                    }
                }
                else
                {
                    rulesDesc = string.Join(", ", uiReaderPrep.cachedState.rules);
                }

                ImGui.Separator();
                ImGui.Text(locPrepNpc);
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, npcDesc);

                ImGui.Text(locPrepRule);
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, rulesDesc);
            }
            else
            {
                ImGui.Separator();
                ImGui.Text(locGameNpc);
                ImGui.SameLine();
                ImGui.TextColored(colorYellow, (solver.currentNpc != null) ? solver.currentNpc.Name.GetLocalized() : "--");

                ImGui.Text(locGameMove);
                ImGui.SameLine();

                if (isPvPMatch || isGameDataMissing)
                {
                    ImGui.TextColored(colorYellow, locGameMoveDisabled);
                }
                else if (solver.hasMove)
                {
                    var useColor =
                        (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueWins) ? colorOk :
                        (solver.moveWinChance.expectedResult == FFTriadBuddy.ETriadGameState.BlueDraw) ? colorYellow :
                        colorErr;

                    string humanCard = (solver.moveCard != null) ? solver.moveCard.Name.GetLocalized() : "??";
                    int boardX = solver.moveBoardIdx % 3;
                    int boardY = solver.moveBoardIdx / 3;
                    string humanBoardX = boardX == 0 ? locBoardX0 : (boardX == 1) ? locBoardX1 : locBoardX2;
                    string humanBoardY = boardY == 0 ? locBoardY0 : (boardY == 1) ? locBoardY1 : locBoardY2;
                    string humanBoard = (solver.moveBoardIdx == 4) ? locBoardCenter : $"{humanBoardY}, {humanBoardX}";

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
