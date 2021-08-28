using Dalamud.Game.Gui;
using Dalamud.Logging;
using FFTriadBuddy;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace TriadBuddyPlugin
{
    public class UIReaderTriadCardList
    {
        [StructLayout(LayoutKind.Explicit, Size = 0x520)]               // it's around 0x550?
        private unsafe struct AddonTriadCardList
        {
            [FieldOffset(0x0)] public AtkUnitBase AtkUnitBase;
            [FieldOffset(0xe0)] public AtkCollisionNode* SelectedCardColisionNode;

            [FieldOffset(0x288)] public byte CardRarity;                // 1..5
            [FieldOffset(0x289)] public byte CardType;                  // 0: no type, 1: primal, 2: scion, 3: beastman, 4: garland
            [FieldOffset(0x28b)] public byte NumSideU;
            [FieldOffset(0x28c)] public byte NumSideD;
            [FieldOffset(0x28d)] public byte NumSideR;
            [FieldOffset(0x28e)] public byte NumSideL;
            [FieldOffset(0x290)] public int CardIconId;                 // texture id for button (82100+) or 0 when missing

            [FieldOffset(0x500)] public byte PageIndex;                 // ignores writes
            [FieldOffset(0x504)] public byte CardIndex;                 // can be written to, yay!
        }

        [StructLayout(LayoutKind.Explicit, Size = 0x110)]               // it's around 0x200?
        private unsafe struct AgentTriadCardList
        {
            [FieldOffset(0x100)] public int PageIndex;                  // can be written to, yay!
            [FieldOffset(0x108)] public int CardIndex;                  // ignores writes
            [FieldOffset(0x10c)] public byte FilterMode;                // 0 = all, 1 = only owned, 2 = only missing

            // 0x28 card data iterator start?
            // 0x30 card data iterator end
        }

        public enum Status
        {
            NoErrors,
            AddonNotFound,
            AddonNotVisible,
            NodesNotReady,
        }

        public UIStateTriadCardList cachedState = new();
        public Action<UIStateTriadCardList> OnUIStateChanged;
        public Action<bool> OnVisibilityChanged;

        public Status status = Status.AddonNotFound;
        public bool IsVisible => (status != Status.AddonNotFound) && (status != Status.AddonNotVisible);
        public bool HasErrors => false;

        private GameGui gameGui;
        private IntPtr cachedAddonPtr;
        private IntPtr cachedAddonAgentPtr;

        public UIReaderTriadCardList(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            IntPtr addonPtr = gameGui.GetAddonByName("GSInfoCardList", 1);
            if (cachedAddonPtr != addonPtr)
            {
                // reset cached pointers when addon address changes
                cachedAddonPtr = addonPtr;
                cachedAddonAgentPtr = gameGui.FindAgentInterface(addonPtr);

                cachedState.descNodeAddr = 0;
            }

            if (addonPtr == IntPtr.Zero || cachedAddonAgentPtr == IntPtr.Zero)
            {
                SetStatus(Status.AddonNotFound);
                return;
            }

            var addon = (AddonTriadCardList*)addonPtr;
            if (addon->AtkUnitBase.RootNode == null || !addon->AtkUnitBase.RootNode->IsVisible)
            {
                SetStatus(Status.AddonNotVisible);
                return;
            }

            if (cachedState.descNodeAddr == 0)
            {
                if (!FindTextNodeAddresses(addon))
                {
                    SetStatus(Status.NodesNotReady);
                    return;
                }
            }

            var descNode = (AtkResNode*)cachedState.descNodeAddr;
            (cachedState.screenPos, cachedState.screenSize) = GUINodeUtils.GetNodePosAndSize(addon->AtkUnitBase.RootNode);
            (cachedState.descriptionPos, cachedState.descriptionSize) = GUINodeUtils.GetNodePosAndSize(descNode);

            var addonAgent = (AgentTriadCardList*)cachedAddonAgentPtr;
            if (cachedState.pageIndex != addon->PageIndex ||
                cachedState.cardIndex != addon->CardIndex ||
                cachedState.filterMode != addonAgent->FilterMode ||
                cachedState.numU != addon->NumSideU)
            {
                cachedState.numU = addon->NumSideU;
                cachedState.numL = addon->NumSideL;
                cachedState.numD = addon->NumSideD;
                cachedState.numR = addon->NumSideR;
                cachedState.rarity = addon->CardRarity;
                cachedState.type = addon->CardType;
                cachedState.iconId = addon->CardIconId;
                cachedState.pageIndex = addon->PageIndex;
                cachedState.cardIndex = addon->CardIndex;
                cachedState.filterMode = addonAgent->FilterMode;

                OnUIStateChanged?.Invoke(cachedState);
            }

            SetStatus(Status.NoErrors);
        }

        public unsafe bool SetPageAndGridView(int pageIndex, int cellIndex)
        {
            // doesn't really belong to a ui "reader", but won't be making a class just for calling one function
            // (there is no UnsafeReaderTriadCards, it's a um.. just a trick of light)

            // basic sanity checks on values before writing them to memory
            // this will NOT be enough when filters are active!
            if (pageIndex < 0 || pageIndex >= GameCardDB.MaxGridPages || cellIndex < 0 || cellIndex >= GameCardDB.MaxGridCells)
            {
                return false;
            }

            // refresh cached pointers before using them
            IntPtr addonPtr = gameGui.GetAddonByName("GSInfoCardList", 1);
            cachedAddonAgentPtr = (addonPtr != IntPtr.Zero) ? gameGui.FindAgentInterface(addonPtr) : IntPtr.Zero;

            if (addonPtr != IntPtr.Zero && cachedAddonAgentPtr != IntPtr.Zero)
            {
                var addon = (AddonTriadCardList*)addonPtr;
                var addonAgent = (AgentTriadCardList*)cachedAddonAgentPtr;

                addonAgent->PageIndex = pageIndex;
                addon->CardIndex = (byte)cellIndex;
                return true;
            }

            return false;
        }

        private void SetStatus(Status newStatus)
        {
            if (status != newStatus)
            {
                bool wasVisible = IsVisible;
                status = newStatus;

                if (HasErrors)
                {
                    PluginLog.Error("CardList reader error: " + newStatus);
                }

                if (wasVisible != IsVisible)
                {
                    OnVisibilityChanged?.Invoke(IsVisible);
                }
            }
        }

        private unsafe bool FindTextNodeAddresses(AddonTriadCardList* addon)
        {
            // 9 child nodes (sibling scan)
            //     [1] aqcuire section, simple node, 5 children
            //         [2] text
            //     [2] description section, simple node, 4 children
            //         [0] text

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(addon->AtkUnitBase.RootNode);
            var nodeDescripion = GUINodeUtils.PickNode(nodeArrL0, 2, 9);
            cachedState.descNodeAddr = (ulong)GUINodeUtils.GetChildNode(nodeDescripion);

            return (cachedState.descNodeAddr == 0);
        }
    }

    public class UIStateTriadCardList
    {
        public Vector2 screenPos;
        public Vector2 screenSize;
        public Vector2 descriptionPos;
        public Vector2 descriptionSize;
        public ulong descNodeAddr;

        public byte numU;
        public byte numL;
        public byte numD;
        public byte numR;
        public byte rarity;
        public byte type;
        public int iconId;

        public byte pageIndex;
        public byte cardIndex;
        public byte filterMode;

        public TriadCard ToTriadCard(GameUIParser ctx)
        {
            return ctx.ParseCard(numU, numL, numD, numR, GameDataLoader.ConvertToTriadType(type), GameDataLoader.ConvertToTriadRarity(rarity), false);
        }
    }
}
