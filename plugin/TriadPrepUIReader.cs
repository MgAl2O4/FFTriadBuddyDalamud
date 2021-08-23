using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace TriadBuddyPlugin
{
    public class TriadPrepUIReader
    {
        public TriadPrepUIState cachedState = new TriadPrepUIState();
        public bool isActive;
        public bool IsDeckSelection => hasDeckSelection;

        public Action<TriadPrepUIState> OnChanged;

        private GameGui gameGui;
        private bool hasRequest;
        private bool hasDeckSelection;
        private IntPtr cachedDeckSelAddon;

        public TriadPrepUIReader(GameGui gameGui)
        {
            this.gameGui = gameGui;
        }

        public unsafe void Update()
        {
            bool newActive = false;

            IntPtr addonReqPtr = gameGui.GetAddonByName("TripleTriadRequest", 1);
            if (addonReqPtr != IntPtr.Zero)
            {
                var baseNode = (AtkUnitBase*)addonReqPtr;
                if (baseNode != null && baseNode->RootNode != null && baseNode->RootNode->IsVisible)
                {
                    if (!hasRequest)
                    {
                        UpdateRequest(baseNode);
                        hasRequest = true;
                    }
                    newActive = true;
                }
            }
            else
            {
                IntPtr addonDeckPtr = gameGui.GetAddonByName("TripleTriadSelDeck", 1);
                if (addonDeckPtr != IntPtr.Zero)
                {
                    // addon ptr changed? reset cached node ptrs
                    if (cachedDeckSelAddon != addonDeckPtr)
                    {
                        cachedDeckSelAddon = addonDeckPtr;
                        foreach (var deckOb in cachedState.decks)
                        {
                            deckOb.screenUpdateAddr = 0;
                        }
                    }

                    var baseNode = (AtkUnitBase*)addonDeckPtr;
                    if (baseNode != null && baseNode->RootNode != null && baseNode->RootNode->IsVisible)
                    {
                        if (!hasDeckSelection)
                        {
                            UpdateDeckSelect(baseNode);

                            hasDeckSelection = cachedState.decks.Count > 0;
                            if (hasDeckSelection)
                            {
                                OnChanged?.Invoke(cachedState);
                            }
                        }
                        else
                        {
                            foreach (var deckOb in cachedState.decks)
                            {
                                var updateNode = (AtkResNode*)deckOb.screenUpdateAddr;
                                if (updateNode != null)
                                {
                                    (deckOb.screenPos, deckOb.screenSize) = GUINodeUtils.GetNodePosAndSize(updateNode);
                                }
                            }
                        }

                        newActive = true;
                    }
                }
            }

            if (isActive != newActive)
            {
                isActive = newActive;
                hasRequest = false;
                hasDeckSelection = false;
            }
        }

        private unsafe void UpdateRequest(AtkUnitBase* baseNode)
        {
            // 13 child nodes (sibling scan, root node list huge)
            //     [6] match/tournament rules, simple node
            //         [0] comp node with 3 children: [2] = text
            //         [1] comp node with 3 children: [2] = text
            //     [7] region rules, simple node
            //         [0] comp node with 3 children: [2] = text
            //         [1] comp node with 3 children: [2] = text
            //     [8] npc, simple node
            //         [0] text

            var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
            var nodeRulesA = GUINodeUtils.PickNode(nodeArrL0, 6, 13);
            var nodeArrL1A = GUINodeUtils.GetImmediateChildNodes(nodeRulesA);
            var nodeL2A1 = GUINodeUtils.PickNode(nodeArrL1A, 0, 6);
            cachedState.rules[3] = GUINodeUtils.GetNodeText(GUINodeUtils.PickChildNode(nodeL2A1, 2, 3));
            var nodeL2A2 = GUINodeUtils.PickNode(nodeArrL1A, 1, 6);
            cachedState.rules[2] = GUINodeUtils.GetNodeText(GUINodeUtils.PickChildNode(nodeL2A2, 2, 3));

            var nodeRulesB = GUINodeUtils.PickNode(nodeArrL0, 7, 13);
            var nodeArrL1B = GUINodeUtils.GetImmediateChildNodes(nodeRulesB);
            var nodeL2B1 = GUINodeUtils.PickNode(nodeArrL1B, 0, 3);
            cachedState.rules[1] = GUINodeUtils.GetNodeText(GUINodeUtils.PickChildNode(nodeL2B1, 2, 3));
            var nodeL2B2 = GUINodeUtils.PickNode(nodeArrL1B, 1, 3);
            cachedState.rules[0] = GUINodeUtils.GetNodeText(GUINodeUtils.PickChildNode(nodeL2B2, 2, 3));

            var nodeNpc = GUINodeUtils.PickNode(nodeArrL0, 8, 13);
            cachedState.npc = GUINodeUtils.GetNodeText(GUINodeUtils.GetChildNode(nodeNpc));

            cachedState.decks.Clear();
        }

        private unsafe void UpdateDeckSelect(AtkUnitBase* baseNode)
        {
            // 5 child nodes (node list)
            //    [4] list 
            //        [x] comp nodes, each has 12 child nodes
            //            [3] simple node with 5 children, each is a card
            //                [x] comp node with 2 children
            //                    [1] comp node with 4 children
            //                        [0] card image
            //            [11] text, deck name

            cachedState.decks.Clear();

            var nodeA = (baseNode->UldManager.NodeListCount == 5) ? baseNode->UldManager.NodeList[4] : null;
            if (nodeA != null && (int)nodeA->Type > 1000)
            {
                var compNodeA = (AtkComponentNode*)nodeA;
                for (int idxA = 0; idxA < compNodeA->Component->UldManager.NodeListCount; idxA++)
                {
                    var nodeB = compNodeA->Component->UldManager.NodeList[idxA];
                    var nodeC1 = GUINodeUtils.PickChildNode(nodeB, 3, 12);

                    if (nodeC1 != null)
                    {
                        var deckOb = new TriadPrepDeckUIState();
                        int numValidCards = 0;

                        var nodeArrC1 = GUINodeUtils.GetImmediateChildNodes(nodeC1);
                        if (nodeArrC1 != null && nodeArrC1.Length == 5)
                        {
                            for (int idxC = 0; idxC < nodeArrC1.Length; idxC++)
                            {
                                var nodeD = GUINodeUtils.PickChildNode(nodeArrC1[idxC], 1, 2);
                                var nodeE = GUINodeUtils.PickChildNode(nodeD, 0, 4);
                                var texPath = GUINodeUtils.GetNodeTexturePath(nodeE);
                                if (string.IsNullOrEmpty(texPath))
                                {
                                    break;
                                }

                                deckOb.cardTexPaths[idxC] = texPath;
                                numValidCards++;
                            }
                        }

                        if (numValidCards == deckOb.cardTexPaths.Length)
                        {
                            (deckOb.screenPos, deckOb.screenSize) = GUINodeUtils.GetNodePosAndSize(nodeB);
                            deckOb.screenUpdateAddr = (ulong)nodeB;
                            cachedState.decks.Add(deckOb);
                        }
                    }
                }
            }
        }
    }

    public class TriadPrepDeckUIState
    {
        public string[] cardTexPaths = new string[5];
        public Vector2 screenPos;
        public Vector2 screenSize;
        public ulong screenUpdateAddr;
    }

    public class TriadPrepUIState
    {
        public string[] rules = new string[4];
        public string npc;

        public List<TriadPrepDeckUIState> decks = new List<TriadPrepDeckUIState>();
    }
}
