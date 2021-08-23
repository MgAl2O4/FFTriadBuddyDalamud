using Dalamud.Game.Gui;
using Dalamud.Logging;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public partial class TriadGameUIReader
    {
        public enum Status
        {
            NoErrors,
            AddonNotFound,
            AddonNotVisible,
            FailedToReadMove,
            FailedToReadRules,
            FailedToReadRedPlayer,
            FailedToReadCards,
        }

        public TriadGameUIState currentState;
        public Status status;
        public bool HasErrors => status >= Status.FailedToReadMove;

        public event Action<TriadGameUIState> OnChanged;

        private GameGui gameGui;
        private IntPtr addonPtr;

        public TriadGameUIReader(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            addonPtr = gameGui.GetAddonByName("TripleTriad", 1);
            if (addonPtr == IntPtr.Zero)
            {
                SetStatus(Status.AddonNotFound);
                SetCurrentState(null);
                return;
            }

            var addon = (AddonTripleTriad*)addonPtr;

            bool isVisible = (addon->AtkUnitBase.RootNode != null) && addon->AtkUnitBase.RootNode->IsVisible;
            if (!isVisible)
            {
                SetStatus(Status.AddonNotVisible);
                SetCurrentState(null);
                return;
            }

            status = Status.NoErrors;
            var newState = new TriadGameUIState();

            (newState.rules, newState.redPlayerDesc) = GetUIDescriptions(addon);

            if (status == Status.NoErrors)
            {
                newState.move = addon->TurnState;
                if (newState.move > 2)
                {
                    SetStatus(Status.FailedToReadMove);
                }
            }

            if (status == Status.NoErrors)
            {
                newState.blueDeck[0] = GetCardData(addon->BlueDeck0);
                newState.blueDeck[1] = GetCardData(addon->BlueDeck1);
                newState.blueDeck[2] = GetCardData(addon->BlueDeck2);
                newState.blueDeck[3] = GetCardData(addon->BlueDeck3);
                newState.blueDeck[4] = GetCardData(addon->BlueDeck4);

                newState.redDeck[0] = GetCardData(addon->RedDeck0);
                newState.redDeck[1] = GetCardData(addon->RedDeck1);
                newState.redDeck[2] = GetCardData(addon->RedDeck2);
                newState.redDeck[3] = GetCardData(addon->RedDeck3);
                newState.redDeck[4] = GetCardData(addon->RedDeck4);

                newState.board[0] = GetCardData(addon->Board0);
                newState.board[1] = GetCardData(addon->Board1);
                newState.board[2] = GetCardData(addon->Board2);
                newState.board[3] = GetCardData(addon->Board3);
                newState.board[4] = GetCardData(addon->Board4);
                newState.board[5] = GetCardData(addon->Board5);
                newState.board[6] = GetCardData(addon->Board6);
                newState.board[7] = GetCardData(addon->Board7);
                newState.board[8] = GetCardData(addon->Board8);
            }

            SetCurrentState(status == Status.NoErrors ? newState : null);
        }

        private void SetStatus(Status newStatus)
        {
            if (status != newStatus)
            {
                status = newStatus;
                if (HasErrors)
                {
                    PluginLog.Error("ui reader error: " + newStatus);
                }
            }
        }

        private void SetCurrentState(TriadGameUIState newState)
        {
            bool isEmpty = newState == null;
            bool wasEmpty = currentState == null;

            if (isEmpty && wasEmpty)
            {
                return;
            }

            bool changed = (isEmpty != wasEmpty);
            if (!changed && !isEmpty && !wasEmpty)
            {
                changed = !currentState.Equals(newState);
            }

            if (changed)
            {
                currentState = newState;
                OnChanged?.Invoke(newState);
            }
        }

        private unsafe (List<string>, List<string>) GetUIDescriptions(AddonTripleTriad* addon)
        {
            var listRuleDesc = new List<string>();
            var listRedDesc = new List<string>();

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(addon->AtkUnitBase.RootNode);

            var nodeRule0 = GUINodeUtils.PickNode(nodeArrL0, 4, 12);
            var nodeArrRule = GUINodeUtils.GetImmediateChildNodes(nodeRule0);
            if (nodeArrRule != null && nodeArrRule.Length == 5)
            {
                for (int idx = 0; idx < 4; idx++)
                {
                    var text = GUINodeUtils.GetNodeText(nodeArrRule[4 - idx]);
                    if (!string.IsNullOrEmpty(text))
                    {
                        listRuleDesc.Add(text);
                    }
                }
            }
            else
            {
                SetStatus(Status.FailedToReadRules);
            }

            var nodeName0 = GUINodeUtils.PickNode(nodeArrL0, 6, 12);
            var nodeArrNameL1 = GUINodeUtils.GetImmediateChildNodes(nodeName0);
            var nodeNameL1 = GUINodeUtils.PickNode(nodeArrNameL1, 0, 5);
            var nodeArrNameL2 = GUINodeUtils.GetAllChildNodes(nodeNameL1);
            // there are multiple text boxes, for holding different combinations of name & titles?
            // idk, too lazy to investigate, grab everything inside
            int numParsed = 0;
            if (nodeArrNameL2 != null)
            {
                foreach (var testNode in nodeArrNameL2)
                {
                    var isVisible = (testNode != null) ? (testNode->Flags & 0x10) == 0x10 : false;
                    if (isVisible)
                    {
                        numParsed++;
                        var text = GUINodeUtils.GetNodeText(testNode);
                        if (!string.IsNullOrEmpty(text))
                        {
                            listRedDesc.Add(text);
                        }
                    }
                }
            }

            if (numParsed == 0)
            {
                SetStatus(Status.FailedToReadRedPlayer);
            }

            return (listRuleDesc, listRedDesc);
        }

        private unsafe (string, bool) GetCardTextureData(AddonTripleTriadCard addonCard)
        {
            // DragDrop Component
            // [1] Icon Component
            //     [0] Base Component <- locked out colors here
            //         [3] Image Node
            var nodeA = GUINodeUtils.PickChildNode(addonCard.CardDropControl, 1, 3);
            var nodeB = GUINodeUtils.PickChildNode(nodeA, 0, 2);
            var nodeC = GUINodeUtils.PickChildNode(nodeB, 3, 21);
            var texPath = GUINodeUtils.GetNodeTexturePath(nodeC);

            if (nodeC == null)
            {
                SetStatus(Status.FailedToReadCards);
            }

            bool isLocked = (nodeB != null) && (nodeB->MultiplyRed < 100);
            return (texPath, isLocked);
        }

        private unsafe TriadCardUIState GetCardData(AddonTripleTriadCard addonCard)
        {
            var resultOb = new TriadCardUIState();
            if (addonCard.HasCard)
            {
                resultOb.isPresent = true;
                resultOb.owner = addonCard.CardOwner;

                bool isKnown = (addonCard.NumSideU != 0);
                if (isKnown)
                {
                    resultOb.numU = addonCard.NumSideU;
                    resultOb.numL = addonCard.NumSideL;
                    resultOb.numD = addonCard.NumSideD;
                    resultOb.numR = addonCard.NumSideR;
                    resultOb.rarity = addonCard.CardRarity;
                    resultOb.type = addonCard.CardType;

                    (resultOb.texturePath, resultOb.isLocked) = GetCardTextureData(addonCard);
                }
            }

            return resultOb;
        }

        public unsafe (Vector2, Vector2) GetCardPosAndSize(AddonTripleTriadCard addonCard)
        {
            if (addonCard.CardDropControl != null && addonCard.CardDropControl->OwnerNode != null)
            {
                var resNode = &addonCard.CardDropControl->OwnerNode->AtkResNode;
                return GUINodeUtils.GetNodePosAndSize(resNode);
            }

            return (Vector2.Zero, Vector2.Zero);
        }

        public unsafe (Vector2, Vector2) GetBlueCardPosAndSize(int idx)
        {
            if (addonPtr != IntPtr.Zero)
            {
                var addon = (AddonTripleTriad*)addonPtr;
                switch (idx)
                {
                    case 0: return GetCardPosAndSize(addon->BlueDeck0);
                    case 1: return GetCardPosAndSize(addon->BlueDeck1);
                    case 2: return GetCardPosAndSize(addon->BlueDeck2);
                    case 3: return GetCardPosAndSize(addon->BlueDeck3);
                    case 4: return GetCardPosAndSize(addon->BlueDeck4);
                    default: break;
                }
            }

            return (Vector2.Zero, Vector2.Zero);
        }

        public unsafe (Vector2, Vector2) GetBoardCardPosAndSize(int idx)
        {
            if (addonPtr != IntPtr.Zero)
            {
                var addon = (AddonTripleTriad*)addonPtr;
                switch (idx)
                {
                    case 0: return GetCardPosAndSize(addon->Board0);
                    case 1: return GetCardPosAndSize(addon->Board1);
                    case 2: return GetCardPosAndSize(addon->Board2);
                    case 3: return GetCardPosAndSize(addon->Board3);
                    case 4: return GetCardPosAndSize(addon->Board4);
                    case 5: return GetCardPosAndSize(addon->Board5);
                    case 6: return GetCardPosAndSize(addon->Board6);
                    case 7: return GetCardPosAndSize(addon->Board7);
                    case 8: return GetCardPosAndSize(addon->Board8);
                    default: break;
                }
            }

            return (Vector2.Zero, Vector2.Zero);
        }
    }
}
