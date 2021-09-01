using Dalamud.Game.Gui;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;
using System;
using System.Collections.Generic;

namespace TriadBuddyPlugin
{
    public class UIReaderTriadDeckEdit
    {
        public bool IsVisible { get; private set; }

        private GameGui gameGui;

        private float blinkAlpha;
        private bool isOptimizerActive;

        private List<string> highlightTexPaths = new();

        public UIReaderTriadDeckEdit(GameGui gameGui)
        {
            this.gameGui = gameGui;
            blinkAlpha = 0.0f;
        }

        public unsafe void Update()
        {
            bool newVisible = false;

            IntPtr addonPtr = gameGui.GetAddonByName("GSInfoEditDeck", 1);
            if (addonPtr != IntPtr.Zero)
            {
                var baseNode = (AtkUnitBase*)addonPtr;
                if (baseNode != null && baseNode->RootNode != null && baseNode->RootNode->IsVisible)
                {
                    blinkAlpha = (blinkAlpha + ImGui.GetIO().DeltaTime) % 1.0f;
                    newVisible = true;

                    // root, 14 children (sibling scan)
                    //     [9] res node, card icon grid + fancy stuff
                    //         [0] res node, just card icon grid
                    //             [x] DragDrop components for each card, 3 children on node list
                    //                 [2] icon component, 7 children on node list
                    //                     [0] image node

                    var nodeArrL0 = GUINodeUtils.GetImmediateChildNodes(baseNode->RootNode);
                    var nodeA = GUINodeUtils.PickNode(nodeArrL0, 9, 14);
                    var nodeB = GUINodeUtils.GetChildNode(nodeA);
                    var nodeArrCards = GUINodeUtils.GetImmediateChildNodes(nodeB);
                    if (nodeArrCards != null)
                    {
                        foreach (var nodeD in nodeArrCards)
                        {
                            var nodeE = GUINodeUtils.PickChildNode(nodeD, 2, 3);
                            var nodeImage = GUINodeUtils.PickChildNode(nodeE, 0, 7);

                            if (nodeImage != null)
                            {
                                if (!isOptimizerActive)
                                {
                                    // no optimizer: reset highlights
                                    nodeImage->MultiplyBlue = 100;
                                    nodeImage->MultiplyRed = 100;
                                    nodeImage->MultiplyGreen = 100;
                                }
                                else
                                {
                                    var texPath = GUINodeUtils.GetNodeTexturePath(nodeImage);
                                    bool shouldHighlight = IsCardTexPathMatching(texPath);

                                    // lerp color:
                                    //   t0 .. t0.5 = 0 -> 100%
                                    //   t0.5 .. t1 -> hold 100%
                                    float colorAlpha = (blinkAlpha < 0.5f) ? (blinkAlpha * 2.0f) : 1.0f;
                                    byte colorV = (byte)(shouldHighlight ? (50 + 50 * colorAlpha) : 25);

                                    nodeImage->MultiplyBlue = colorV;
                                    nodeImage->MultiplyRed = colorV;
                                    nodeImage->MultiplyGreen = colorV;
                                }
                            }
                        }
                    }
                }
            }

            if (IsVisible != newVisible)
            {
                IsVisible = newVisible;
            }
        }

        public void OnDeckOptimizerVisible(bool isVisible)
        {
            isOptimizerActive = isVisible;
            highlightTexPaths.Clear();
        }

        public void SetHighlightedCards(int[] cardIds)
        {
            highlightTexPaths.Clear();
            if (cardIds != null)
            {
                foreach (int cardId in cardIds)
                {
                    var pathPattern = string.Format("{0}.tex", FFTriadBuddy.TriadCardDB.GetCardIconTextureId(cardId));
                    highlightTexPaths.Add(pathPattern);
                }
            }
        }

        private bool IsCardTexPathMatching(string texPath)
        {
            foreach (var pathPattern in highlightTexPaths)
            {
                if (texPath.EndsWith(pathPattern))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
