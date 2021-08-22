using Dalamud.Logging;
using FFTriadBuddy;
using System;
using System.Collections.Generic;

namespace TriadBuddyPlugin
{
    public class UIStateParseContext
    {
        public TriadCardDB cards = TriadCardDB.Get();
        public TriadNpcDB npcs = TriadNpcDB.Get();
        public TriadGameModifierDB mods = TriadGameModifierDB.Get();

        public bool hasFailedCard = false;
        public bool hasFailedModifier = false;
        public bool hasFailedNpc = false;
        public bool HasErrors => hasFailedCard || hasFailedModifier || hasFailedNpc;

        public void OnFailedCard(object cardOb)
        {
            PluginLog.Error($"failed to match card: {cardOb}");
            hasFailedCard = true;
        }

        public void OnFailedModifier(string modStr)
        {
            PluginLog.Error($"failed to match rule: {modStr}");
            hasFailedModifier = true;
        }

        public void OnFailedNpc(List<string> desc)
        {
            PluginLog.Error($"failed to match npc: {string.Join(", ", desc)}");
            hasFailedNpc = true;
        }
    }

    public class TriadCardUIState : IEquatable<TriadCardUIState>
    {
        public byte numU;
        public byte numL;
        public byte numD;
        public byte numR;
        public byte rarity;
        public byte type;
        public byte owner;
        public bool isPresent;
        public bool isLocked;
        public string texturePath;

        public bool IsHidden => isPresent && (numU == 0);

        public bool Equals(TriadCardUIState other)
        {
            return (isPresent == other.isPresent) &&
                (isLocked == other.isLocked) &&
                (owner == other.owner) &&
                (texturePath == other.texturePath);
        }

        public override string ToString()
        {
            if (!isPresent) return "(empty)";

            string desc = $"[{numU:X}-{numL:X}-{numD:X}-{numR:X}], tex:{texturePath}, owner:{owner}";
            if (isLocked)
            {
                desc += " (locked)";
            }

            return desc;
        }

        public TriadCard ToTriadCard(UIStateParseContext ctx)
        {
            TriadCard resultOb = null;
            if (isPresent)
            {
                if (!IsHidden)
                {
                    // there's hardly any point in doing side comparison since plugin can access card id directly, but i still like it :<
                    var matchOb = ctx.cards.Find(numU, numL, numD, numR);
                    if (matchOb != null)
                    {
                        if (matchOb.SameNumberId < 0)
                        {
                            resultOb = matchOb;
                        }
                        else
                        {
                            // ambiguous match, use texture for exact Id
                            resultOb = ctx.cards.FindByTexture(texturePath);
                        }
                    }

                    if (resultOb == null)
                    {
                        ctx.OnFailedCard(this);
                    }
                }
                else
                {
                    resultOb = ctx.cards.hiddenCard;
                }
            }

            return resultOb;
        }
    }

    public class TriadGameUIState : IEquatable<TriadGameUIState>
    {
        public List<string> rules;
        public List<string> redPlayerDesc;
        public TriadCardUIState[] blueDeck = new TriadCardUIState[5];
        public TriadCardUIState[] redDeck = new TriadCardUIState[5];
        public TriadCardUIState[] board = new TriadCardUIState[9];
        public byte move;

        public bool Equals(TriadGameUIState other)
        {
            if (move != other.move)
            {
                return false;
            }

            // not real list comparison, but will be enough here
            if (rules.Count != other.rules.Count || !rules.TrueForAll(x => other.rules.Contains(x)))
            {
                return false;
            }

            if (redPlayerDesc.Count != other.redPlayerDesc.Count || !redPlayerDesc.TrueForAll(x => other.redPlayerDesc.Contains(x)))
            {
                return false;
            }

            Func<TriadCardUIState, TriadCardUIState, bool> HasCardDiffs = (a, b) =>
            {
                if ((a == null) != (b == null))
                {
                    return true;
                }

                return (a != null && b != null) ? !a.Equals(b) : false;
            };

            for (int idx = 0; idx < board.Length; idx++)
            {
                if (HasCardDiffs(board[idx], other.board[idx]))
                {
                    return false;
                }
            }

            for (int idx = 0; idx < blueDeck.Length; idx++)
            {
                if (HasCardDiffs(blueDeck[idx], other.blueDeck[idx]) || HasCardDiffs(redDeck[idx], other.redDeck[idx]))
                {
                    return false;
                }
            }

            return true;
        }

        public TriadNpc ToTriadNpc(UIStateParseContext ctx)
        {
            TriadNpc resultOb = null;

            foreach (var name in redPlayerDesc)
            {
                // some names will be truncated in UI, e.g. 'Guhtwint of the Three...'
                // limit match to first 20 characters and hope that SE will keep it unique
                string matchPattern = (name.Length > 20) ? name.Substring(0, 20) : name;

                var matchOb = ctx.npcs.FindByNameStart(matchPattern);
                if (matchOb != null)
                {
                    if (resultOb == null || resultOb == matchOb)
                    {
                        resultOb = matchOb;
                    }
                    else
                    {
                        // um.. names matched two different npc, fail 
                        resultOb = null;
                        break;
                    }
                }
            }

            if (redPlayerDesc.Count > 0 && resultOb == null)
            {
                ctx.OnFailedNpc(redPlayerDesc);
            }

            return resultOb;
        }

        public List<TriadGameModifier> ToTriadModifier(UIStateParseContext ctx)
        {
            var list = new List<TriadGameModifier>();
            foreach (var rule in rules)
            {
                var matchOb = ctx.mods.mods.Find(x => x.GetLocalizedName().Equals(rule, StringComparison.OrdinalIgnoreCase));
                if (matchOb != null)
                {
                    list.Add(matchOb);
                }
                else
                {
                    ctx.OnFailedModifier(rule);
                }
            }

            return list;
        }

        public ScannerTriad.GameState ToTriadScreenState(UIStateParseContext ctx)
        {
            var screenOb = new ScannerTriad.GameState();
            screenOb.mods = ToTriadModifier(ctx);
            screenOb.turnState = (move == 0) ? ScannerTriad.ETurnState.Waiting : ScannerTriad.ETurnState.Active;

            for (int idx = 0; idx < board.Length; idx++)
            {
                screenOb.board[idx] = board[idx].ToTriadCard(ctx);
                screenOb.boardOwner[idx] =
                    (board[idx].owner == 1) ? ETriadCardOwner.Blue :
                    (board[idx].owner == 2) ? ETriadCardOwner.Red :
                     ETriadCardOwner.Unknown;
            }

            bool hasForcedMove = (move == 2);
            for (int idx = 0; idx < blueDeck.Length; idx++)
            {
                screenOb.blueDeck[idx] = blueDeck[idx].ToTriadCard(ctx);
                screenOb.redDeck[idx] = redDeck[idx].ToTriadCard(ctx);

                if (hasForcedMove && blueDeck[idx].isPresent && !blueDeck[idx].isLocked)
                {
                    screenOb.forcedBlueCard = screenOb.blueDeck[idx];
                }
            }

            return screenOb;
        }
    }
}
