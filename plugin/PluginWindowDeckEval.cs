using Dalamud;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFTriadBuddy;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginWindowDeckEval : Window, IDisposable
    {
        private readonly Vector4 colorWin = new Vector4(0.2f, 0.9f, 0.2f, 1);
        private readonly Vector4 colorDraw = new Vector4(0.9f, 0.9f, 0.2f, 1);
        private readonly Vector4 colorLose = new Vector4(0.9f, 0.2f, 0.2f, 1);
        private readonly Vector4 colorTxt = new Vector4(1, 1, 1, 1);
        private readonly Vector4 colorGray = new Vector4(0.6f, 0.6f, 0.6f, 1);

        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly Solver solver;
        private readonly PluginWindowDeckOptimize optimizerWindow;

        private string locEvaluating;
        private string locWinChance;
        private string locCantFind;
        private string locNoProfileDecks;
        private string locOptimize;

        public PluginWindowDeckEval(Solver solver, UIReaderTriadPrep uiReaderPrep, PluginWindowDeckOptimize optimizerWindow) : base("Deck Eval")
        {
            this.solver = solver;
            this.uiReaderPrep = uiReaderPrep;
            this.optimizerWindow = optimizerWindow;

            uiReaderPrep.OnMatchRequestChanged += OnMatchRequestChanged;
            OnMatchRequestChanged(uiReaderPrep.HasMatchRequestUI);

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;
            RespectCloseHotkey = false;

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                // ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        public void Dispose()
        {
            // meh
        }

        private void CacheLocalization()
        {
            locEvaluating = Localization.Localize("DE_Evaluating", "Evaluating decks...");
            locWinChance = Localization.Localize("DE_WinChance", "win {0:P0}");
            locCantFind = Localization.Localize("DE_Failed", "Err.. Can't find best deck :<");
            locNoProfileDecks = Localization.Localize("DE_NoProfileDecks", "Err.. No decks to evaluate");
            locOptimize = Localization.Localize("DE_Optimize", "Optimize deck");
        }

        private void OnMatchRequestChanged(bool active)
        {
            bool canAccessProfileDecks = (solver.profileGS != null) && !solver.profileGS.HasErrors;
            IsOpen = active && canAccessProfileDecks;

            if (active)
            {
                GameCardDB.Get().Refresh();
            }
        }

        public override void PreDraw()
        {
            var btnSize = ImGuiHelpers.GetButtonSize("-");

            Position = uiReaderPrep.cachedState.screenPos + new Vector2(0, uiReaderPrep.cachedState.screenSize.Y);
            Size = new Vector2(uiReaderPrep.cachedState.screenSize.X, btnSize.Y + (2 * 10)) / ImGuiHelpers.GlobalScale;
        }

        public override void Draw()
        {
            Vector4 hintColor = colorTxt;
            string hintText = "";

            if (solver.preGameDecks.Count == 0)
            {
                // no profile decks created vs profile reader failed
                hintColor = solver.HasAllProfileDecksEmpty ? colorTxt : colorDraw;
                hintText = locNoProfileDecks;
            }
            else if (solver.preGameProgress < 1.0f)
            {
                hintColor = colorTxt;
                hintText = string.Format("{0} {1:P0}", locEvaluating, solver.preGameProgress).Replace("%", "%%");
            }
            else
            {
                if (solver.preGameDecks.TryGetValue(solver.preGameBestId, out var bestDeckData))
                {
                    hintColor = GetChanceColor(bestDeckData.chance);
                    hintText = $"{bestDeckData.name} -- ";
                    hintText += string.Format(locWinChance, bestDeckData.chance.winChance).Replace("%", "%%");
                }
                else
                {
                    hintColor = colorLose;
                    hintText = locCantFind;
                }
            }

            var btnSize = ImGuiHelpers.GetButtonSize("-");
            var textSize = ImGui.CalcTextSize(hintText);
            var windowMin = ImGui.GetWindowContentRegionMin();
            var windowMax = ImGui.GetWindowContentRegionMax();
            var windowSize = windowMax - windowMin;
            var hintPosY = windowMin.Y + ((windowSize.Y - textSize.Y) * 0.5f);
            var hintPosX = windowMin.X + ((windowSize.X - textSize.X) * 0.5f);

            var btnStartX = windowSize.X - btnSize.X - (10 * ImGuiHelpers.GlobalScale);
            var btnStartY = windowMin.Y + ((windowSize.Y - textSize.Y) * 0.5f) - ImGui.GetStyle().FramePadding.Y;
            var optimizeSize = ImGui.CalcTextSize(locOptimize);
            var optimizeStartX = btnStartX - optimizeSize.X - (5 * ImGuiHelpers.GlobalScale);

            if (optimizerWindow.CanRunOptimizer())
            {
                hintPosX = Math.Max(windowMin.X + (10 * ImGuiHelpers.GlobalScale), Math.Min(hintPosX, optimizeStartX - (20 * ImGuiHelpers.GlobalScale) - textSize.X));
            }

            ImGui.SetCursorPos(new Vector2(hintPosX, hintPosY));
            ImGui.TextColored(hintColor, hintText);

            if (optimizerWindow.CanRunOptimizer())
            {
                ImGui.SetCursorPos(new Vector2(optimizeStartX, hintPosY));
                ImGui.TextColored(colorGray, locOptimize);

                ImGui.SetCursorPos(new Vector2(btnStartX, btnStartY));
                if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                {
                    optimizerWindow.SetupAndOpen(solver.preGameNpc, solver.preGameMods);
                }
            }
        }

        public Vector4 GetChanceColor(TriadGameResultChance chance)
        {
            return (chance.expectedResult == ETriadGameState.BlueWins) ? colorWin :
                (chance.expectedResult == ETriadGameState.BlueDraw) ? colorDraw :
                colorLose;
        }
    }
}
