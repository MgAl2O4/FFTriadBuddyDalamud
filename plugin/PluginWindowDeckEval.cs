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

        private readonly UIReaderTriadPrep uiReaderPrep;
        private readonly Solver solver;
        private readonly PluginWindowDeckOptimize optimizerWindow;

        private string locEvaluating;
        private string locWinChance;
        private string locCantFind;
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
            Position = uiReaderPrep.cachedState.screenPos + new Vector2(0, uiReaderPrep.cachedState.screenSize.Y);
            Size = new Vector2(uiReaderPrep.cachedState.screenSize.X, 50);
        }

        public override void Draw()
        {
            if (solver.preGameDecks.Count > 0)
            {
                Vector4 hintColor = colorTxt;
                string hintText = "";

                if (solver.preGameProgress < 1.0f)
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

                var textSize = ImGui.CalcTextSize(hintText);
                ImGui.SetCursorPos(new Vector2((Size.Value.X - textSize.X) * 0.5f, (Size.Value.Y - textSize.Y) * 0.5f));
                ImGui.TextColored(hintColor, hintText);

                if (optimizerWindow.CanRunOptimizer())
                {
                    ImGui.SetCursorPos(new Vector2(Size.Value.X - 50, (Size.Value.Y - textSize.Y) * 0.5f - ImGui.GetStyle().FramePadding.Y));
                    if (ImGuiComponents.IconButton(FontAwesomeIcon.Search))
                    {
                        optimizerWindow.SetupAndOpen(solver.preGameNpc, solver.preGameMods);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(locOptimize);
                    }
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
