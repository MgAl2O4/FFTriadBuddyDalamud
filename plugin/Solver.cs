using FFTriadBuddy;
using MgAl2O4.Utils;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TriadBuddyPlugin
{
    public class Solver
    {
        public enum Status
        {
            NoErrors,
            FailedToParseCards,
            FailedToParseRules,
            FailedToParseNpc,
        }

        public UnsafeReaderProfileGS profileGS;

        // game
        private TriadGameScreenMemory screenMemory = new();
        public TriadNpc currentNpc;
        public TriadCard moveCard => screenMemory.deckBlue?.GetCard(moveCardIdx);
        public int moveCardIdx;
        public int moveBoardIdx;
        public TriadGameResultChance moveWinChance;
        public bool hasMove;

        // deck selection
        public class DeckData
        {
            public string name;
            public int id;
            public TriadDeck solverDeck;
            public TriadGameResultChance chance;
        }

        public TriadNpc preGameNpc;
        public List<TriadGameModifier> preGameMods = new();
        public Dictionary<int, DeckData> preGameDecks = new();
        public float preGameProgress => (preGameDecks.Count > 0) ? (1.0f * preGameSolved / preGameDecks.Count) : 0.0f;
        public int preGameBestId = -1;
        private int preGameId = 0;
        private int preGameSolved = 0;
        private object preGameLock = new();

        public Status status;
        public bool HasErrors => status != Status.NoErrors;

        public event Action<bool> OnMoveChanged;

        public Solver()
        {
            TriadGameSession.StaticInitialize();
        }

        public void UpdateGame(UIStateTriadGame stateOb)
        {
            status = Status.NoErrors;

            ScannerTriad.GameState screenOb = null;
            if (stateOb != null)
            {
                var parseCtx = new GameUIParser();
                screenOb = stateOb.ToTriadScreenState(parseCtx);
                currentNpc = stateOb.ToTriadNpc(parseCtx);

                if (parseCtx.HasErrors)
                {
                    currentNpc = null;
                    status =
                        parseCtx.hasFailedCard ? Status.FailedToParseCards :
                        parseCtx.hasFailedModifier ? Status.FailedToParseRules :
                        parseCtx.hasFailedNpc ? Status.FailedToParseNpc :
                        Status.NoErrors;
                }
            }
            else
            {
                // not really an error state, ui reader will push null state when game is finished
                currentNpc = null;
            }

            if (currentNpc != null && screenOb.turnState == ScannerTriad.ETurnState.Active && !stateOb.isPvP)
            {
                var updateFlags = screenMemory.OnNewScan(screenOb, currentNpc);
                if (updateFlags != TriadGameScreenMemory.EUpdateFlags.None)
                {
                    screenMemory.gameSession.SolverFindBestMove(screenMemory.gameState, out int solverBoardPos, out TriadCard solverTriadCard, out moveWinChance);
                    hasMove = true;
                    moveCardIdx = screenMemory.deckBlue.GetCardIndex(solverTriadCard);
                    moveBoardIdx = (moveCardIdx < 0) ? -1 : solverBoardPos;

                    Logger.WriteLine("  suggested move: [{0}] {1} {2} (expected: {3})",
                        moveBoardIdx, ETriadCardOwner.Blue,
                        solverTriadCard != null ? solverTriadCard.Name.GetCodeName() : "??",
                        moveWinChance.expectedResult);

                    OnMoveChanged?.Invoke(hasMove);
                }
            }
            else if (hasMove)
            {
                hasMove = false;
                OnMoveChanged?.Invoke(hasMove);
            }
        }

        private class DeckSolverContext
        {
            public TriadGameSession session;
            public TriadGameData gameData;
            public int deckId;
            public int passId;
        }

        public void UpdateDecks(UIStateTriadPrep state)
        {
            // don't report status here, just log stuff out
            var parseCtx = new GameUIParser();
            preGameNpc = parseCtx.ParseNpc(state.npc);
            preGameMods.Clear();
            foreach (var rule in state.rules)
            {
                var ruleOb = parseCtx.ParseModifier(rule, false);
                if (ruleOb != null && !(ruleOb is TriadGameModifierNone))
                {
                    preGameMods.Add(ruleOb);
                }
            }

            bool canReadFromProfile = profileGS != null && !profileGS.HasErrors;
            bool canProcessDecks = !parseCtx.HasErrors &&
                // case 1: it's play request screen, no deck info in ui, proceed only if profile reader is available
                ((state.decks.Count == 0 && canReadFromProfile) ||
                // case 2: it's deck selection screen, ui has deck info, proceed only if solved already (profile reader not available)
                (state.decks.Count > 0 && !canReadFromProfile));

            if (canProcessDecks)
            {
                // bump pass id, pending workers from previous update won't try to write their results
                preGameId++;
                preGameDecks.Clear();
                preGameBestId = -1;

                var profileDecks = canReadFromProfile ? profileGS.GetPlayerDecks() : null;
                int numDecks = (profileDecks != null) ? profileDecks.Length : state.decks.Count;

                TriadDeck anyDeckOb = null;
                for (int deckIdx = 0; deckIdx < numDecks; deckIdx++)
                {
                    parseCtx.Reset();

                    var deckData = (profileDecks != null) ?
                        ParseDeckDataFromProfile(profileDecks[deckIdx], parseCtx) :
                        ParseDeckDataFromUI(state.decks[deckIdx], parseCtx);

                    if (!parseCtx.HasErrors && deckData != null)
                    {
                        preGameDecks.Add(deckData.id, deckData);
                        anyDeckOb = deckData.solverDeck;
                    }
                }

                // initialize screenMemory.playerDeck, see comment in OnSolvedDeck() for details
                if (anyDeckOb == null)
                {
                    anyDeckOb = new TriadDeck(PlayerSettingsDB.Get().starterCards);
                }
                screenMemory.UpdatePlayerDeck(anyDeckOb);

                foreach (var kvp in preGameDecks)
                {
                    var session = new TriadGameSession();
                    foreach (var mod in preGameMods)
                    {
                        var modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                        modCopy.OnMatchInit();

                        session.modifiers.Add(modCopy);
                    }

                    session.UpdateSpecialRules();

                    var gameData = session.StartGame(kvp.Value.solverDeck, preGameNpc.Deck, ETriadGameState.InProgressRed);
                    var calcContext = new DeckSolverContext() { session = session, gameData = gameData, deckId = kvp.Value.id, passId = preGameId };

                    Action<object> solverAction = (ctxOb) =>
                    {
                        var ctx = ctxOb as DeckSolverContext;
                        ctx.session.SolverFindBestMove(ctx.gameData, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                        OnSolvedDeck(ctx.passId, ctx.deckId, bestChance);
                    };

                    new TaskFactory().StartNew(solverAction, calcContext);
                }
            }
        }

        private DeckData ParseDeckDataFromProfile(UnsafeReaderProfileGS.PlayerDeck deckOb, GameUIParser ctx)
        {
            // empty profile decks will result in nulls here
            if (deckOb == null)
            {
                return null;
            }

            var deckData = new DeckData() { id = deckOb.id, name = deckOb.name };

            var cards = new TriadCard[5];
            for (int cardIdx = 0; cardIdx < 5; cardIdx++)
            {
                int cardId = deckOb.cardIds[cardIdx];
                cards[cardIdx] = ctx.cards.FindById(cardId);

                if (cards[cardIdx] == null)
                {
                    ctx.OnFailedCard($"id:{cardId}");
                }
            }

            deckData.solverDeck = ctx.HasErrors ? null : new TriadDeck(cards);
            return deckData;
        }

        private DeckData ParseDeckDataFromUI(UIStateTriadPrepDeck deckOb, GameUIParser ctx)
        {
            // empty UI decks are valid objects, but their card data is empty (handled by ctx)

            var deckData = new DeckData() { id = deckOb.id, name = deckOb.name };

            var cards = new TriadCard[5];
            for (int cardIdx = 0; cardIdx < 5; cardIdx++)
            {
                cards[cardIdx] = ctx.ParseCard(deckOb.cardTexPaths[cardIdx]);
            }

            deckData.solverDeck = ctx.HasErrors ? null : new TriadDeck(cards);
            return deckData;
        }

        private void OnSolvedDeck(int passId, int deckId, TriadGameResultChance winChance)
        {
            if (preGameId != passId)
            {
                return;
            }

            lock (preGameLock)
            {
                if (preGameDecks.TryGetValue(deckId, out var deckData))
                {
                    deckData.chance = winChance;
                    preGameSolved++;

                    // TODO: broadcast? (this is still worker thread!)
                    Logger.WriteLine($"deck[{deckId}]:'{deckData.name}', result:{winChance.expectedResult}, win%:{winChance.winChance:P0}, draw%:{winChance.drawChance:P0}");

                    float bestScore = 0;
                    int bestId = -1;
                    foreach (var kvp in preGameDecks)
                    {
                        float testScore = kvp.Value.chance.compScore;
                        if (bestId < 0 || testScore > bestScore)
                        {
                            bestId = kvp.Key;
                            bestScore = testScore;
                        }
                    }
                    
                    // screenMemory.PlayerDeck - originally used for determining swapped cards
                    // there's probably much better way of doing that and it needs further work
                    // for now, just pretend that best scoring deck is the one that player will be using
                    // - yes, player used that one in game - yay, swap detection works correctly
                    // - nope, player picked something else - whatever, build in failsafes in swap detection will handle that after 3-4 matches
                    if (bestId >= 0 && bestId != preGameBestId)
                    {
                        if (preGameDecks.TryGetValue(bestId, out var bestDeckData))
                        {
                            screenMemory.UpdatePlayerDeck(bestDeckData.solverDeck);
                        }
                    }

                    preGameBestId = bestId;
                }
            }
        }
    }
}
