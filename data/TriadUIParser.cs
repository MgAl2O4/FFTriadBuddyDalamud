using Dalamud.Logging;
using FFTriadBuddy;
using System;

namespace TriadBuddyPlugin
{
    public class TriadUIParser
    {
        public TriadCardDB cards = TriadCardDB.Get();
        public TriadNpcDB npcs = TriadNpcDB.Get();
        public TriadGameModifierDB mods = TriadGameModifierDB.Get();

        public bool hasFailedCard = false;
        public bool hasFailedModifier = false;
        public bool hasFailedNpc = false;
        public bool HasErrors => hasFailedCard || hasFailedModifier || hasFailedNpc;

        public TriadCard ParseCard(int numU, int numL, int numD, int numR, string texPath, bool markFailed = true)
        {
            // there's hardly any point in doing side comparison since plugin can access card id directly, but i still like it :<
            var matchOb = cards.Find(numU, numL, numD, numR);
            if (matchOb != null)
            {
                if (matchOb.SameNumberId >= 0)
                {
                    // ambiguous match, use texture for exact Id
                    matchOb = ParseCard(texPath, false);
                }
            }

            if (matchOb == null && markFailed)
            {
                OnFailedCard($"[{numU}-{numL}-{numD}-{numR}], tex:{texPath}");
            }

            return matchOb;
        }

        public TriadCard ParseCard(string texPath, bool markFailed = true)
        {
            var matchOb = cards.FindByTexture(texPath);
            if (matchOb == null && markFailed)
            {
                OnFailedCard(texPath);
            }

            return matchOb;
        }

        public void OnFailedCard(string desc)
        {
            PluginLog.Error($"failed to match card: {desc}");
            hasFailedCard = true;
        }

        public TriadGameModifier ParseModifier(string desc, bool markFailed = true)
        {
            var matchOb = mods.mods.Find(x => x.GetLocalizedName().Equals(desc, StringComparison.OrdinalIgnoreCase));
            if (matchOb == null && markFailed)
            {
                OnFailedModifier(desc);
            }

            return matchOb;
        }

        public void OnFailedModifier(string desc)
        {
            PluginLog.Error($"failed to match rule: {desc}");
            hasFailedModifier = true;
        }

        public TriadNpc ParseNpcNameStart(string desc, bool markFailed = true)
        {
            // some names will be truncated in UI, e.g. 'Guhtwint of the Three...'
            // limit match to first 20 characters and hope that SE will keep it unique
            string matchPattern = (desc.Length > 20) ? desc.Substring(0, 20) : desc;

            var matchOb = npcs.FindByNameStart(matchPattern);
            if (matchOb == null && markFailed)
            {
                OnFailedNpc(desc);
            }

            return matchOb;
        }

        public TriadNpc ParseNpc(string desc, bool markFailed = true)
        {
            var matchOb = npcs.Find(desc);
            if (matchOb == null && markFailed)
            {
                OnFailedNpc(desc);
            }

            return matchOb;
        }

        public void OnFailedNpc(string desc)
        {
            PluginLog.Error($"failed to match npc: {string.Join(", ", desc)}");
            hasFailedNpc = true;
        }
    }
}
