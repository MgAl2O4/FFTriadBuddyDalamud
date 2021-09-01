using Dalamud;
using Dalamud.Game.Gui;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFTriadBuddy;
using ImGuiNET;
using System;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class PluginWindowCardInfo : Window, IDisposable
    {
        private readonly UIReaderTriadCardList uiReaderCardList;
        private readonly GameGui gameGui;

        private TriadCard selectedCard;
        private GameCardInfo selectedCardInfo;

        private string locNpcReward;
        private string locShowOnMap;
        private string locNoAvail;

        public PluginWindowCardInfo(UIReaderTriadCardList uiReaderCardList, GameGui gameGui) : base("Card Info")
        {
            this.uiReaderCardList = uiReaderCardList;
            this.gameGui = gameGui;

            uiReaderCardList.OnVisibilityChanged += (_) => UpdateWindowData();
            uiReaderCardList.OnUIStateChanged += (_) => UpdateWindowData();
            UpdateWindowData();

            // doesn't matter will be updated on next draw
            PositionCondition = ImGuiCond.None;
            SizeCondition = ImGuiCond.None;

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
            // meh
        }

        private void CacheLocalization()
        {
            locNpcReward = Localization.Localize("CI_NpcReward", "NPC reward:");
            locShowOnMap = Localization.Localize("CI_ShowMap", "Show on map");
            locNoAvail = Localization.Localize("CI_NotAvail", "Not available");
        }

        private void UpdateWindowData()
        {
            bool canShow = (uiReaderCardList != null) && uiReaderCardList.IsVisible && (uiReaderCardList.cachedState?.iconId == 0);
            if (canShow)
            {
                var parseCtx = new GameUIParser();
                selectedCard = uiReaderCardList.cachedState.ToTriadCard(parseCtx);

                if (selectedCard != null)
                {
                    canShow = true;
                    selectedCardInfo = GameCardDB.Get().FindById(selectedCard.Id);
                }
            }

            IsOpen = canShow;
        }

        public override void PreDraw()
        {
            Position = uiReaderCardList.cachedState.descriptionPos;
            Size = uiReaderCardList.cachedState.descriptionSize;
        }

        public override void Draw()
        {
            if (selectedCard != null)
            {
                var colorName = new Vector4(0.9f, 0.9f, 0.2f, 1);
                var colorGray = new Vector4(0.6f, 0.6f, 0.6f, 1);

                ImGui.TextColored(colorName, selectedCard.Name.GetLocalized());

                ImGui.Text($"{(int)selectedCard.Rarity + 1}★");
                ImGui.SameLine();
                ImGui.Text($"{selectedCard.Sides[(int)ETriadGameSide.Up]:X}-{selectedCard.Sides[(int)ETriadGameSide.Left]:X}-{selectedCard.Sides[(int)ETriadGameSide.Down]:X}-{selectedCard.Sides[(int)ETriadGameSide.Right]:X}");
                if (selectedCard.Type != ETriadCardType.None)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(colorGray, LocalizationDB.Get().LocCardTypes[(int)selectedCard.Type].Text);
                }

                ImGui.NewLine();
                ImGui.Text(locNpcReward);

                TriadNpc rewardNpc = (selectedCardInfo == null) ? null :
                    (selectedCardInfo.RewardNpcId < 0 || selectedCardInfo.RewardNpcId >= TriadNpcDB.Get().npcs.Count) ? null :
                    TriadNpcDB.Get().npcs[selectedCardInfo.RewardNpcId];

                if (selectedCardInfo != null && rewardNpc != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(colorName, rewardNpc.Name.GetLocalized());

                    //ImGui.NewLine();
                    var cursorY = ImGui.GetCursorPosY();
                    ImGui.SetCursorPosY(cursorY - ImGui.GetStyle().FramePadding.Y);
                    if (ImGuiComponents.IconButton(Dalamud.Interface.FontAwesomeIcon.Map))
                    {
                        gameGui.OpenMapWithMapLink(selectedCardInfo.RewardNpcLocation);
                    }
                    if (ImGui.IsItemHovered())
                    {
                        ImGui.SetTooltip(locShowOnMap);
                    }

                    ImGui.SetCursorPosY(cursorY);
                    ImGui.SameLine();
                    ImGui.Text($"{selectedCardInfo.RewardNpcLocation.PlaceName} {selectedCardInfo.RewardNpcLocation.CoordinateString}");
                }
                else
                {
                    ImGui.TextColored(colorGray, locNoAvail);
                }
            }
        }
    }
}
