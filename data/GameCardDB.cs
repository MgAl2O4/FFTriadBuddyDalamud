using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFTriadBuddy;
using System.Collections.Generic;

namespace TriadBuddyPlugin
{
    public enum GameCardCollectionFilter
    {
        All,
        OnlyOwned,
        OnlyMissing,
    }

    public class GameCardInfo
    {
        public struct CollectionPos
        {
            public int PageIndex;
            public int CellIndex;
        }

        public int CardId;

        public int RewardNpcId = -1;
        public MapLinkPayload RewardNpcLocation;

        // call GameCardDB.Refresh() before reading fields below
        public bool IsOwned;
        public CollectionPos[] Collection = new CollectionPos[3];
    }

    // aguments TriadCardDB with stuff not related to game logic
    public class GameCardDB
    {
        private static GameCardDB instance = new();
        public static readonly int MaxGridPages = 15;
        public static readonly int MaxGridCells = 30;

        public UnsafeReaderTriadCards memReader;
        public Dictionary<int, GameCardInfo> mapCards = new();
        public List<int> ownedCardIds = new();

        public static GameCardDB Get() { return instance; }

        public GameCardInfo FindById(int cardId)
        {
            if (mapCards.TryGetValue(cardId, out var cardInfo))
            {
                return cardInfo;
            }

            return null;
        }

        public void Refresh()
        {
            int maxId = mapCards.Count;
            ownedCardIds.Clear();

            if (memReader == null || memReader.HasErrors || maxId <= 0)
            {
                return;
            }

            // consider switching to memory read for bulk checks? not that UI itself cares about it...
            // check IsTriadCardOwned() for details, uiState+0x15ce5 is a byte array of szie 0x29 used as a bitmask with cardId => buffer[id / 8] & (1 << (id % 8))

            for (int id = 1; id < maxId; id++)
            {
                bool isOwned = memReader.IsCardOwned(id);
                if (isOwned)
                {
                    ownedCardIds.Add(id);
                }
            }

            foreach (var kvp in mapCards)
            {
                kvp.Value.IsOwned = ownedCardIds.Contains(kvp.Value.CardId);
            }

            RebuildCollections();
        }

        private void RebuildCollections()
        {
            var sortedTriadCards = new List<TriadCard>();
            sortedTriadCards.AddRange(TriadCardDB.Get().cards);
            sortedTriadCards.RemoveAll(x => (x == null) || !x.IsValid());
            sortedTriadCards.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));

            var noCollectionData = new GameCardInfo.CollectionPos() { PageIndex = -1, CellIndex = -1 };

            for (int filterIdx = 0; filterIdx < 3; filterIdx++)
            {
                int groupIdx = 0;
                int pageIdx = 0;
                int cellIdx = 0;

                foreach (var cardOb in sortedTriadCards)
                {
                    bool isValid = mapCards.TryGetValue(cardOb.Id, out var cardInfoOb);
                    if (isValid)
                    {
                        bool isOwned = ownedCardIds.Contains(cardOb.Id);
                        bool isMatchingFilter =
                            ((GameCardCollectionFilter)filterIdx == GameCardCollectionFilter.All) ||
                            ((GameCardCollectionFilter)filterIdx == GameCardCollectionFilter.OnlyOwned && isOwned) ||
                            ((GameCardCollectionFilter)filterIdx == GameCardCollectionFilter.OnlyMissing && !isOwned);

                        if (isMatchingFilter)
                        {
                            if (groupIdx != cardOb.Group)
                            {
                                groupIdx = cardOb.Group;
                                pageIdx++;
                                cellIdx = 0;
                            }

                            if (cellIdx >= MaxGridCells)
                            {
                                cellIdx = 0;
                                pageIdx++;
                            }

                            cardInfoOb.Collection[filterIdx] = new GameCardInfo.CollectionPos() { PageIndex = pageIdx, CellIndex = cellIdx };
                            cellIdx++;
                        }
                        else
                        {
                            cardInfoOb.Collection[filterIdx] = new GameCardInfo.CollectionPos() { PageIndex = -1, CellIndex = -1 };
                        }
                    }
                }
            }
        }
    }
}
