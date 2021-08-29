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

        private TriadDeckOptimizer deckOptimizer = new();
        private List<TriadGameModifier> regionMods = new();
        private TriadNpc npc;
        private string regionModsDesc;

        private float optimizerProgress;
        private float optimizerElapsedTime;
        private string optimizerTimeRemainingDesc;
        private bool isOptimizerRunning;
        private int[] pendingCardIds;
        private int[] shownCardIds;
        private string[] shownCardTooltips = new string[5];
        private float pendingCardsUpdateTimeRemaining;
        private float optimizerStatsTimeRemaining;

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
        private string locTimeRemaining;
        private string locTooltipPage;
        private string locOptimizeStart;
        private string locOptimizeAbort;

        public PluginWindowDeckOptimize(DataManager dataManager) : base("Deck Optimizer")
        {
            this.dataManager = dataManager;

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

        private void DeckOptimizer_OnFoundDeck(TriadDeck deck)
        {
            if (deck != null && deck.knownCards != null && deck.knownCards.Count == 5)
            {
                // buffer card changes to catch multiple fast swaps and reduce number of loaded images

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
            locNpc = Localization.Localize("DO_Npc", "Npc:");
            locRegionRules = Localization.Localize("DO_RegionRules", "Region rules:");
            locWinChance = Localization.Localize("DO_WinChance", "Win chance:");
            locTimeRemaining = Localization.Localize("DO_TimeRemaining", "Time remaining:");
            locTooltipPage = Localization.Localize("DO_CardTooltip", "Collection page: {0}");
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

            IsOpen = true;
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
                        ImGui.SetTooltip(shownCardTooltips[idx]);
                    }
                }
            }

            // stat block
            ImGui.SetCursorPos(new Vector2(currentPos.X, currentPos.Y + cardImagePos[3].Y + ImGui.GetTextLineHeight()));
            ImGui.Text(locWinChance);
            ImGui.TextColored(colorResultData, "100%%");    // TODO

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

                    for (int idx = 0; idx < shownCardIds.Length; idx++)
                    {
                        var cardOb = TriadCardDB.Get().FindById(shownCardIds[idx]);
                        if (cardOb != null)
                        {
                            shownCardTooltips[idx] = $"{(int)cardOb.Rarity + 1}★  {cardOb.Name.GetLocalized()}";

                            var cardInfo = GameCardDB.Get().FindById(shownCardIds[idx]);
                            if (cardInfo != null)
                            {
                                int pageIdx = cardInfo.Collection[(int)GameCardCollectionFilter.All].PageIndex;
                                shownCardTooltips[idx] += "\n\n";
                                shownCardTooltips[idx] += string.Format(locTooltipPage, pageIdx + 1);
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

            await deckOptimizer.Process(npc, regionMods.ToArray(), lockedCards);

            isOptimizerRunning = false;
            optimizerTimeRemainingDesc = "--";
        }

        private void AbortOptimizer()
        {
            deckOptimizer.AbortProcess();
        }

        public override void OnClose()
        {
            if (isOptimizerRunning)
            {
                deckOptimizer.AbortProcess();
            }

            // free cached card images on window close
            foreach (var kvp in mapCardImages)
            {
                kvp.Value.Dispose();
            }
            mapCardImages.Clear();
        }
    }
}
