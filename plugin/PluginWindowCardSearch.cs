using Dalamud;
using Dalamud.Interface.Windowing;
using FFTriadBuddy;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public unsafe class PluginWindowCardSearch : Window, IDisposable
    {
        private readonly UIReaderTriadCardList uiReaderCardList;

        private List<Tuple<TriadCard, GameCardInfo>> listCards = new();
        private int selectedCardIdx;
        private int filterMode = -1;
        private ImGuiTextFilterPtr searchFilter;
        private bool showNpcMatchesOnly = false;
        private bool showNotOwnedOnly = false;

        private string locNpcOnly;
        private string locNotOwnedOnly;
        private string locFilterActive;

        public PluginWindowCardSearch(UIReaderTriadCardList uiReaderCardList) : base("Card Search")
        {
            this.uiReaderCardList = uiReaderCardList;

            var searchFilterPtr = ImGuiNative.ImGuiTextFilter_ImGuiTextFilter(null);
            searchFilter = new ImGuiTextFilterPtr(searchFilterPtr);

            uiReaderCardList.OnVisibilityChanged += (_) => UpdateWindowData();
            uiReaderCardList.OnUIStateChanged += OnUIStateChanged;
            UpdateWindowData();

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;

            Size = new Vector2(250, ImGui.GetTextLineHeightWithSpacing() * 15.5f);

            Flags = ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoMove |
                //ImGuiWindowFlags.NoMouseInputs |
                ImGuiWindowFlags.NoDocking |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav;

            Plugin.CurrentLocManager.LocalizationChanged += (_) => CacheLocalization();
            CacheLocalization();
        }

        public void Dispose()
        {
            ImGuiNative.ImGuiTextFilter_destroy(searchFilter.NativePtr);
        }

        private void CacheLocalization()
        {
            locNpcOnly = Localization.Localize("CS_NpcOnly", "NPC matches only");
            locNotOwnedOnly = Localization.Localize("CS_NotOwnedOnly", "Not owned only");
            locFilterActive = Localization.Localize("CS_FilterActive", "(Collection filtering is active)");
        }

        private void UpdateWindowData()
        {
            bool wasOpen = IsOpen;
            IsOpen = uiReaderCardList.IsVisible;

            if (IsOpen && !wasOpen)
            {
                GameCardDB.Get().Refresh();
                filterMode = -1;
                searchFilter.Clear();

                UpdateWindowBounds();
                OnUIStateChanged(uiReaderCardList.cachedState);
            }
        }

        public void OnUIStateChanged(UIStateTriadCardList uiState)
        {
            if (filterMode != uiState.filterMode)
            {
                filterMode = uiState.filterMode;
                listCards.Clear();

                var cardDB = TriadCardDB.Get();
                var cardInfoDB = GameCardDB.Get();

                bool includeOwned = filterMode != 2;
                bool includeMissing = filterMode != 1;

                foreach (var card in cardDB.cards)
                {
                    if (card != null && card.IsValid())
                    {
                        var cardInfo = cardInfoDB.FindById(card.Id);
                        if (cardInfo != null)
                        {
                            if ((includeOwned && cardInfo.IsOwned) || (includeMissing && !cardInfo.IsOwned))
                            {
                                listCards.Add(new Tuple<TriadCard, GameCardInfo>(card, cardInfo));
                            }
                        }
                    }
                }

                if (listCards.Count > 1)
                {
                    listCards.Sort((a, b) => a.Item1.Name.GetLocalized().CompareTo(b.Item1.Name.GetLocalized()));
                }

                selectedCardIdx = -1;
            }
        }

        public void UpdateWindowBounds()
        {
            Position = new Vector2(uiReaderCardList.cachedState.screenPos.X + uiReaderCardList.cachedState.screenSize.X + 10, uiReaderCardList.cachedState.screenPos.Y);
        }

        public override void Draw()
        {
            // position & size will lag 1 tick behind, no hooks in Window class to do dynamic stuff before drawing?
            // doesn't really matter, it's docked under what's supposed to be simple UI screen where all interaction comes down to clicking a button
            UpdateWindowBounds();

            bool showOwnedCheckbox = filterMode == 0;

            searchFilter.Draw("", Size.Value.X - 20);

            ImGui.BeginListBox("##cards", new Vector2(Size.Value.X - 20, ImGui.GetTextLineHeightWithSpacing() * 10));
            for (int idx = 0; idx < listCards.Count; idx++)
            {
                var (cardOb, cardInfo) = listCards[idx];
                if ((showNpcMatchesOnly && cardInfo.RewardNpcId < 0) ||
                    (showOwnedCheckbox && showNotOwnedOnly && cardInfo.IsOwned))
                {
                    continue;
                }

                var itemDesc = cardOb.Name.GetLocalized();
                if (searchFilter.PassFilter(itemDesc))
                {
                    bool isSelected = selectedCardIdx == idx;
                    if (ImGui.Selectable($"{(int)cardOb.Rarity + 1}★   {itemDesc}", isSelected))
                    {
                        selectedCardIdx = idx;
                        OnSelectionChanged();
                    }

                    if (isSelected)
                    {
                        ImGui.SetItemDefaultFocus();
                    }
                }
            }
            ImGui.EndListBox();

            ImGui.NewLine();
            ImGui.Checkbox(locNpcOnly, ref showNpcMatchesOnly);

            if (showOwnedCheckbox)
            {
                ImGui.Checkbox(locNotOwnedOnly, ref showNotOwnedOnly);
            }
            else
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1), locFilterActive);
            }
        }

        private void OnSelectionChanged()
        {
            var (cardOb, cardInfo) = (selectedCardIdx >= 0) && (selectedCardIdx < listCards.Count) ? listCards[selectedCardIdx] : null;
            if (cardOb != null && cardInfo != null)
            {
                var filterEnum = (filterMode == 1) ? GameCardCollectionFilter.OnlyOwned :
                    (filterMode == 2) ? GameCardCollectionFilter.OnlyMissing :
                    GameCardCollectionFilter.All;

                var collectionPos = cardInfo.Collection[(int)filterEnum];

                //PluginLog.Log($"Card selection! {cardOb.Name.GetLocalized()}, filter:{filterEnum} ({filterMode}) => page:{collectionPos.PageIndex}, cell:{collectionPos.CellIndex}");
                uiReaderCardList.SetPageAndGridView(collectionPos.PageIndex, collectionPos.CellIndex);
            }
        }
    }
}
