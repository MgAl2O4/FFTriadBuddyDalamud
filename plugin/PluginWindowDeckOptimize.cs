using Dalamud;
using Dalamud.Data;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFTriadBuddy;
using ImGuiNET;
using ImGuiScene;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginWindowDeckOptimize : Window, IDisposable
    {
        private readonly Vector4 colorSetupData = new Vector4(0.9f, 0.9f, 0.0f, 1);
        private readonly Vector4 colorResultData = new Vector4(0.0f, 0.9f, 0.9f, 1);

        private DataManager dataManager;
        private Solver solver;
        private UIReaderTriadDeckEdit uiReaderDeckEdit;

        private TriadDeckOptimizer deckOptimizer = new();
        private List<TriadGameModifier> regionMods = new();
        private TriadNpc npc;
        private string regionModsDesc;

        private float optimizerProgress;
        private float optimizerElapsedTime;
        private float optimizerWinChance;
        private float pendingWinChance;
        private string optimizerTimeRemainingDesc;
        private bool isOptimizerRunning;
        private int[] pendingCardIds;
        private int[] shownCardIds;
        private string[] shownCardTooltipsDeckEdit = new string[5];
        private string[] shownCardTooltipsCollection = new string[5];
        private float pendingCardsUpdateTimeRemaining;
        private float optimizerStatsTimeRemaining;

        private bool hasDeckSolverResult;
        private TriadGameResultChance deckWinChance;
        private TriadDeck cachedSolverDeck;

        private Dictionary<int, TextureWrap> mapCardImages = new();
        private TextureWrap cardBackgroundImage;

        private Vector2 cardBackgroundUV0 = new(0.0f, 0.0f);
        private Vector2 cardBackgroundUV1 = new(1.0f, 1.0f);
        private Vector2 cardImageSize = new(104, 128);
        private Vector2[] cardImagePos = new Vector2[5];
        private Vector2 cardImageBox = new(0.0f, 0.0f);

        private string locNpc;
        private string locRegionRules;
        private string locWinChance;
        private string locDeckScore;
        private string locTimeRemaining;
        private string locCollectionPage;
        private string locDeckEditPage;
        private string locOptimizeStart;
        private string locOptimizeAbort;

        public PluginWindowDeckOptimize(DataManager dataManager, Solver solver, UIReaderTriadDeckEdit uiReaderDeckEdit) : base("Deck Optimizer")
        {
            this.dataManager = dataManager;
            this.solver = solver;
            this.uiReaderDeckEdit = uiReaderDeckEdit;

            deckOptimizer.OnFoundDeck += DeckOptimizer_OnFoundDeck;

            cardBackgroundImage = dataManager.GetImGuiTexture("ui/uld/CardTripleTriad.tex");
            cardBackgroundUV1.Y = (cardBackgroundImage != null) ? (cardImageSize.Y / cardBackgroundImage.Height) : 1.0f;

            cardImagePos[0] = new Vector2(0, 0);
            cardImagePos[1] = new Vector2(cardImageSize.X + 5, 0);
            cardImagePos[2] = new Vector2((cardImageSize.X + 5) * 2, 0);
            cardImagePos[3] = new Vector2((cardImageSize.X + 5) / 2, cardImageSize.Y + 5);
            cardImagePos[4] = new Vector2((cardImageSize.X + 5) / 2 + cardImageSize.X + 5, cardImageSize.Y + 5);
            cardImageBox.X = cardImagePos[2].X + cardImageSize.X;
            cardImageBox.Y = cardImagePos[4].Y + cardImageSize.Y;

            SizeCondition = ImGuiCond.Appearing;
            Size = new Vector2(cardImageBox.X + 150, cardImageBox.Y + (ImGui.GetTextLineHeight() * 7.5f));

            Flags = ImGuiWindowFlags.NoResize;

            Plugin.CurrentLocManager.LocalizationChanged += (langCode) => CacheLocalization();
            CacheLocalization();
        }

        private void DeckOptimizer_OnFoundDeck(TriadDeck deck, float estWinChance)
        {
            if (deck != null && deck.knownCards != null && deck.knownCards.Count == 5)
            {
                // buffer card changes to catch multiple fast swaps and reduce number of loaded images
                pendingWinChance = estWinChance;
                cachedSolverDeck = deck;

                pendingCardIds = new int[5];
                for (int idx = 0; idx < pendingCardIds.Length; idx++)
                {
                    pendingCardIds[idx] = deck.knownCards[idx].Id;
                }
            }
        }

        public void Dispose()
        {
            cardBackgroundImage.Dispose();
            foreach (var kvp in mapCardImages)
            {
                kvp.Value.Dispose();
            }
        }

        private void CacheLocalization()
        {
            WindowName = Localization.Localize("DO_Title", "Deck Optimizer");
            locNpc = Localization.Localize("DO_Npc", "Npc:");
            locRegionRules = Localization.Localize("DO_RegionRules", "Region rules:");
            locWinChance = Localization.Localize("DO_WinChance", "Win chance:");
            locDeckScore = Localization.Localize("DO_DeckScore", "Deck score:");
            locTimeRemaining = Localization.Localize("DO_TimeRemaining", "Time remaining:");
            locCollectionPage = Localization.Localize("DO_CollectionPage", "Collection page: {0}");
            locDeckEditPage = Localization.Localize("DO_DeckBuilderPage", "Deck builder page: {0}");
            locOptimizeStart = Localization.Localize("DO_Start", "Optimize deck");
            locOptimizeAbort = Localization.Localize("DO_Abort", "Abort");
        }

        public bool CanRunOptimizer()
        {
            // needs access to list of currently owned cards, provided by UnsafeReaderTriadCard class
            // any errors there (outdated signatures) will disable deck optimizer
            return PlayerSettingsDB.Get().ownedCards.Count > 0;
        }

        public void SetupAndOpen(TriadNpc npc, List<TriadGameModifier> gameRules)
        {
            if (cardBackgroundImage == null)
            {
                return;
            }

            string prevModDesc = regionModsDesc;
            var prevNpc = this.npc;

            regionMods.Clear();
            regionModsDesc = null;

            this.npc = npc;
            if (npc != null)
            {
                bool[] removedNpcMod = { false, false };
                foreach (var mod in gameRules)
                {
                    if (mod != null)
                    {
                        int npcModIdx = npc.Rules.FindIndex(x => x.GetLocalizationId() == mod.GetLocalizationId());
                        if (npcModIdx != -1 && !removedNpcMod[npcModIdx])
                        {
                            removedNpcMod[npcModIdx] = true;
                            continue;
                        }

                        regionMods.Add((TriadGameModifier)Activator.CreateInstance(mod.GetType()));

                        if (regionModsDesc != null) { regionModsDesc += ", "; }
                        regionModsDesc += mod.GetLocalizedName();
                    }
                }
            }

            bool setupChanged = (npc != prevNpc) || (regionModsDesc != prevModDesc);
            if (setupChanged)
            {
                optimizerWinChance = -1;
                hasDeckSolverResult = false;
                shownCardIds = null;
            }

            // opening on already running optimizer? sounds bad, make sure it's aborted
            if (isOptimizerRunning)
            {
                AbortOptimizer();
            }

            IsOpen = true;
            uiReaderDeckEdit?.OnDeckOptimizerVisible(IsOpen);
        }

        private TextureWrap GetCardTexture(int cardId)
        {
            if (mapCardImages.TryGetValue(cardId, out var texWrap))
            {
                return texWrap;
            }

            uint iconId = TriadCardDB.GetCardTextureId(cardId);
            var newTexWrap = dataManager.GetImGuiTextureIcon(iconId);
            mapCardImages.Add(cardId, newTexWrap);

            return newTexWrap;
        }

        public override void Draw()
        {
            if (cardBackgroundImage == null)
            {
                return;
            }

            UpdateTick();

            // header
            ImGui.Text(locNpc);
            ImGui.SameLine();
            ImGui.TextColored(colorSetupData, npc != null ? npc.Name.GetLocalized() : "??");

            ImGui.Text(locRegionRules);
            ImGui.SameLine();
            ImGui.TextColored(colorSetupData, !string.IsNullOrEmpty(regionModsDesc) ? regionModsDesc : "--");

            // deck cards and their layouts
            var textSizeWinChance = ImGui.CalcTextSize(locWinChance);
            var textSizeTimeRemaining = ImGui.CalcTextSize(locTimeRemaining);
            var textWidthMax = Math.Max(textSizeTimeRemaining.X, textSizeWinChance.X) + 10;
            var imageBoxIndentX = cardImagePos[3].X;
            var imageBoxOffsetX = Math.Max(0, textWidthMax - imageBoxIndentX);

            var currentPos = ImGui.GetCursorPos();
            var centerOffset = new Vector2((ImGui.GetWindowContentRegionWidth() - cardImageBox.X) / 2, 10);

            for (int idx = 0; idx < cardImagePos.Length; idx++)
            {
                var drawPos = currentPos + cardImagePos[idx] + centerOffset;
                drawPos.X += Math.Max(0, imageBoxOffsetX - centerOffset.X);

                ImGui.SetCursorPos(drawPos);
                ImGui.Image(cardBackgroundImage.ImGuiHandle, cardImageSize, cardBackgroundUV0, cardBackgroundUV1);

                var cardImage = (shownCardIds != null) ? GetCardTexture(shownCardIds[idx]) : null;
                if (cardImage != null)
                {
                    ImGui.SetCursorPos(drawPos);
                    ImGui.Image(cardImage.ImGuiHandle, cardImageSize);

                    if (ImGui.IsItemHovered())
                    {
                        bool isDeckEditorOpen = uiReaderDeckEdit?.IsVisible ?? false;
                        ImGui.SetTooltip(isDeckEditorOpen ? shownCardTooltipsDeckEdit[idx] : shownCardTooltipsCollection[idx]);
                    }
                }
            }

            // stat block
            ImGui.SetCursorPos(new Vector2(currentPos.X, currentPos.Y + cardImagePos[3].Y + ImGui.GetTextLineHeight()));
            if (isOptimizerRunning || hasDeckSolverResult || optimizerWinChance >= 0.0f)
            {
                if (hasDeckSolverResult)
                {
                    ImGui.Text(locWinChance);
                    ImGui.TextColored(colorResultData, deckWinChance.winChance.ToString("P0").Replace("%", "%%"));
                }
                else
                {
                    PluginLog.Log($"chance:{optimizerWinChance}");

                    // "win chance" - it's a score assigned by deck optimizer, that can be approximated as win chance %
                    ImGui.Text(locDeckScore);
                    ImGui.TextColored(colorResultData, optimizerWinChance < 0 ? "--" : (optimizerWinChance * 1000).ToString("0"));
                }
            }

            if (isOptimizerRunning)
            {
                ImGui.NewLine();
                ImGui.Text(locTimeRemaining);
                ImGui.TextColored(colorResultData, optimizerTimeRemainingDesc);
            }

            // footer
            ImGui.SetCursorPos(new Vector2(currentPos.X, currentPos.Y + cardImageBox.Y + 20));
            if (!isOptimizerRunning)
            {
                if (ImGui.Button(locOptimizeStart, new Vector2(-1, 0)))
                {
                    StartOptimizer();
                }
            }
            else
            {
                ImGui.SetNextItemWidth(ImGui.GetWindowContentRegionWidth() * 0.75f);
                ImGui.ProgressBar(optimizerProgress, Vector2.Zero);
                ImGui.SameLine();

                if (ImGui.Button(locOptimizeAbort, new Vector2(-1, 0)))
                {
                    AbortOptimizer();
                }
            }
        }

        private void UpdateTick()
        {
            // found deck buffering, 0.5s cooldown for changes
            if (pendingCardIds != null)
            {
                if (pendingCardsUpdateTimeRemaining <= 0.0f)
                {
                    pendingCardsUpdateTimeRemaining = 0.5f;

                    shownCardIds = pendingCardIds;
                    pendingCardIds = null;

                    optimizerWinChance = pendingWinChance;
                    pendingWinChance = -1;

                    uiReaderDeckEdit?.SetHighlightedCards(shownCardIds);

                    for (int idx = 0; idx < shownCardIds.Length; idx++)
                    {
                        var cardOb = TriadCardDB.Get().FindById(shownCardIds[idx]);
                        if (cardOb != null)
                        {
                            var tooltip = $"{(int)cardOb.Rarity + 1}★  {cardOb.Name.GetLocalized()}";
                            shownCardTooltipsCollection[idx] = tooltip;
                            shownCardTooltipsDeckEdit[idx] = tooltip;

                            var cardInfo = GameCardDB.Get().FindById(shownCardIds[idx]);
                            if (cardInfo != null)
                            {
                                int editPageIdx = cardInfo.Collection[(int)GameCardCollectionFilter.OnlyOwned].PageIndex;
                                shownCardTooltipsDeckEdit[idx] += "\n\n";
                                shownCardTooltipsDeckEdit[idx] += string.Format(locDeckEditPage, editPageIdx + 1);

                                int collectionPageIdx = cardInfo.Collection[(int)GameCardCollectionFilter.All].PageIndex;
                                shownCardTooltipsCollection[idx] += "\n\n";
                                shownCardTooltipsCollection[idx] += string.Format(locCollectionPage, collectionPageIdx + 1);
                            }
                        }
                    }
                }
                else
                {
                    pendingCardsUpdateTimeRemaining -= ImGui.GetIO().DeltaTime;
                }
            }

            // stat update tick, refresh every 0.25s
            if (isOptimizerRunning)
            {
                optimizerElapsedTime += ImGui.GetIO().DeltaTime;
                if (optimizerStatsTimeRemaining <= 0.0f)
                {
                    optimizerStatsTimeRemaining = 0.25f;
                    optimizerProgress = deckOptimizer.GetProgress() / 100.0f;

                    int secondsRemaining = deckOptimizer.GetSecondsRemaining((int)(optimizerElapsedTime * 1000));
                    optimizerElapsedTime = 0;

                    var tspan = TimeSpan.FromSeconds(secondsRemaining);
                    if (tspan.Hours > 0 || tspan.Minutes > 55)
                    {
                        optimizerTimeRemainingDesc = string.Format("{0:D2}h:{1:D2}m:{2:D2}s", tspan.Hours, tspan.Minutes, tspan.Seconds);
                    }
                    else if (tspan.Minutes > 0 || tspan.Seconds > 55)
                    {
                        optimizerTimeRemainingDesc = string.Format("{0:D2}m:{1:D2}s", tspan.Minutes, tspan.Seconds);
                    }
                    else
                    {
                        optimizerTimeRemainingDesc = string.Format("{0:D2}s", tspan.Seconds);
                    }
                }
                else
                {
                    optimizerStatsTimeRemaining -= ImGui.GetIO().DeltaTime;
                }
            }
        }

        private async void StartOptimizer()
        {
            if (!CanRunOptimizer())
            {
                PluginLog.Error("Failed to start deck optimizer");
                return;
            }

            // TODO: do i want to add UI selectors for locked cards? probably not.
            var lockedCards = new List<TriadCard>();
            for (int idx = 0; idx < 5; idx++)
            {
                lockedCards.Add(null);
            }

            deckOptimizer.Initialize(npc, regionMods.ToArray(), lockedCards);

            optimizerStatsTimeRemaining = 0;
            pendingCardsUpdateTimeRemaining = 0;
            optimizerProgress = 0;
            optimizerTimeRemainingDesc = "--";
            isOptimizerRunning = true;
            hasDeckSolverResult = false;

            await deckOptimizer.Process(npc, regionMods.ToArray(), lockedCards);

            isOptimizerRunning = false;
            optimizerTimeRemainingDesc = "--";

            RunDeckSolver();
        }

        private void AbortOptimizer()
        {
            deckOptimizer.AbortProcess();
        }

        private void RunDeckSolver()
        {
            hasDeckSolverResult = false;

            if (solver != null && cachedSolverDeck != null)
            {
                solver.SolveOptimizedDeck(cachedSolverDeck, npc, regionMods, (chance) =>
                {
                    // this is invoked from worker thread!
                    hasDeckSolverResult = true;
                    deckWinChance = chance;
                });
            }
        }

        public override void OnClose()
        {
            if (isOptimizerRunning)
            {
                deckOptimizer.AbortProcess();
            }

            uiReaderDeckEdit?.OnDeckOptimizerVisible(false);

            // free cached card images on window close
            foreach (var kvp in mapCardImages)
            {
                kvp.Value.Dispose();
            }
            mapCardImages.Clear();
        }
    }
}
