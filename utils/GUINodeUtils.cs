﻿using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;

namespace TriadBuddyPlugin
{
    // Dalamud.Interface.UIDebug is amazing

    public class GUINodeUtils
    {
        public static unsafe AtkResNode* PickChildNode(AtkResNode* maybeCompNode, int childIdx, int expectedNumChildren)
        {
            if (maybeCompNode != null && (int)maybeCompNode->Type >= 1000)
            {
                var compNode = (AtkComponentNode*)maybeCompNode;
                if (compNode->Component->UldManager.NodeListCount == expectedNumChildren && childIdx < expectedNumChildren)
                {
                    return compNode->Component->UldManager.NodeList[childIdx];
                }
            }

            return null;
        }

        public static unsafe AtkResNode* PickChildNode(AtkComponentBase* compPtr, int childIdx, int expectedNumChildren)
        {
            if (compPtr != null && compPtr->UldManager.NodeListCount == expectedNumChildren && childIdx < expectedNumChildren)
            {
                return compPtr->UldManager.NodeList[childIdx];
            }

            return null;
        }

        public static unsafe AtkResNode*[] GetImmediateChildNodes(AtkResNode* node)
        {
            var listAddr = new List<ulong>();
            if (node != null && node->ChildNode != null)
            {
                listAddr.Add((ulong)node->ChildNode);

                node = node->ChildNode;
                while (node->PrevSiblingNode != null)
                {
                    listAddr.Add((ulong)node->PrevSiblingNode);
                    node = node->PrevSiblingNode;
                }
            }

            return ConvertToNodeArr(listAddr);
        }

        public static unsafe AtkResNode*[] GetAllChildNodes(AtkResNode* node)
        {
            if (node != null)
            {
                var list = new List<ulong>();
                RecursiveAppendChildNodes(node, list);

                return ConvertToNodeArr(list);
            }

            return null;
        }

        private static unsafe void RecursiveAppendChildNodes(AtkResNode* node, List<ulong> listAddr)
        {
            if (node != null)
            {
                listAddr.Add((ulong)node);

                // step inside
                if (node->ChildNode != null)
                {
                    RecursiveAppendChildNodes(node->ChildNode, listAddr);

                    AtkResNode* linkNode = node->ChildNode;
                    while (linkNode->PrevSiblingNode != null)
                    {
                        RecursiveAppendChildNodes(linkNode->PrevSiblingNode, listAddr);
                        linkNode = linkNode->PrevSiblingNode;
                    }

                    // no need to check next siblings here?
                }
            }
        }

        private static unsafe AtkResNode*[] ConvertToNodeArr(List<ulong> listAddr)
        {
            if (listAddr.Count > 0)
            {
                var typedArr = new AtkResNode*[listAddr.Count];
                for (int idx = 0; idx < listAddr.Count; idx++)
                {
                    typedArr[idx] = (AtkResNode*)listAddr[idx];
                }

                return typedArr;
            }

            return null;
        }

        public static unsafe AtkResNode* PickNode(AtkResNode*[] nodes, int nodeIdx, int expectedNumNodes)
        {
            if (nodes != null && nodes.Length == expectedNumNodes && nodeIdx < expectedNumNodes)
            {
                return nodes[nodeIdx];
            }

            return null;
        }

        public static unsafe AtkResNode* GetChildNode(AtkResNode* node)
        {
            return node != null ? node->ChildNode : null;
        }

        public static unsafe string GetNodeTexturePath(AtkResNode* maybeImageNode)
        {
            if (maybeImageNode != null && maybeImageNode->Type == NodeType.Image)
            {
                var imageNode = (AtkImageNode*)maybeImageNode;
                if (imageNode->PartsList != null && imageNode->PartId <= imageNode->PartsList->PartCount)
                {
                    var textureInfo = imageNode->PartsList->Parts[imageNode->PartId].UldAsset;
                    var texType = textureInfo->AtkTexture.TextureType;
                    if (texType == TextureType.Resource)
                    {
                        var texFileNamePtr = textureInfo->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
                        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr));

                        return texString;
                    }
                }
            }

            return null;
        }

        public static unsafe string GetNodeText(AtkResNode* maybeTextNode)
        {
            if (maybeTextNode != null && maybeTextNode->Type == NodeType.Text)
            {
                var textNode = (AtkTextNode*)maybeTextNode;
                var text = MarshalNativeUtf8ToManagedString(new IntPtr(textNode->NodeText.StringPtr));
                return text;
            }

            return null;
        }

        // https://stackoverflow.com/a/58358514
        private static unsafe string MarshalNativeUtf8ToManagedString(IntPtr ptr) {
            var len = 0;
            var pStringUtf8 = (byte*)ptr;
            while (pStringUtf8[len] != 0)
            {
                len++;
            }

            return Encoding.UTF8.GetString(pStringUtf8, len);
        }

        public static unsafe Vector2 GetNodePosition(AtkResNode* node)
        {
            var pos = new Vector2(node->X, node->Y);
            var par = node->ParentNode;
            while (par != null)
            {
                pos *= new Vector2(par->ScaleX, par->ScaleY);
                pos += new Vector2(par->X, par->Y);
                par = par->ParentNode;
            }

            return pos;
        }

        public static unsafe Vector2 GetNodeScale(AtkResNode* node)
        {
            if (node == null) return new Vector2(1, 1);
            var scale = new Vector2(node->ScaleX, node->ScaleY);
            while (node->ParentNode != null)
            {
                node = node->ParentNode;
                scale *= new Vector2(node->ScaleX, node->ScaleY);
            }

            return scale;
        }
    }
}
