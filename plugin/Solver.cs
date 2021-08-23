using Dalamud.Logging;
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

        // game
        private TriadGameScreenMemory screenMemory = new();
        public TriadNpc currentNpc;
        public TriadCard moveCard => screenMemory.deckBlue?.GetCard(moveCardIdx);
        public int moveCardIdx;
        public int moveBoardIdx;
        public TriadGameResultChance moveWinChance;
        public bool hasMove;

        // deck selection
        public TriadNpc preGameNpc;
        public List<TriadGameModifier> preGameMods = new();
        public List<TriadGameResultChance> preGameDeckChance = new();
        private int preGameId = 0;
        private object preGameLock = new();

        public Status status;
        public bool HasErrors => status != Status.NoErrors;

        public event Action<bool> OnMoveChanged;

        public Solver()
        {
            TriadGameSession.StaticInitialize();
        }

        public void UpdateGame(TriadGameUIState stateOb)
        {
            status = Status.NoErrors;

            ScannerTriad.GameState screenOb = null;
            if (stateOb != null)
            {
                var parseCtx = new TriadUIParser();
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

            if (currentNpc != null && screenOb.turnState == ScannerTriad.ETurnState.Active)
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
            public int deckIdx;
            public int passId;
        }

        public void UpdateDecks(TriadPrepUIState state)
        {
            // don't report status here, just log stuff out
            var parseCtx = new TriadUIParser();
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

            // bump pass id, pending workers from previous update won't try to write their results
            preGameId++;
            preGameDeckChance.Clear();

            if (!parseCtx.HasErrors)
            {
                for (int deckIdx = 0; deckIdx < state.decks.Count; deckIdx++)
                {
                    preGameDeckChance.Add(new TriadGameResultChance());
                }

                for (int deckIdx = 0; deckIdx < state.decks.Count; deckIdx++)
                {
                    parseCtx.Reset();

                    var cards = new TriadCard[5];
                    for (int cardIdx = 0; cardIdx < 5; cardIdx++)
                    {
                        cards[cardIdx] = parseCtx.ParseCard(state.decks[deckIdx].cardTexPaths[cardIdx]);
                    }

                    if (!parseCtx.HasErrors)
                    {
                        var session = new TriadGameSession();
                        foreach (var mod in preGameMods)
                        {
                            var modCopy = (TriadGameModifier)Activator.CreateInstance(mod.GetType());
                            modCopy.OnMatchInit();

                            session.modifiers.Add(modCopy);
                        }

                        session.UpdateSpecialRules();

                        var gameData = session.StartGame(new TriadDeck(cards), preGameNpc.Deck, ETriadGameState.InProgressRed);
                        var calcContext = new DeckSolverContext() { session = session, gameData = gameData, deckIdx = deckIdx, passId = preGameId };

                        Action<object> solverAction = (ctxOb) =>
                        {
                            var ctx = ctxOb as DeckSolverContext;
                            ctx.session.SolverFindBestMove(ctx.gameData, out int bestNextPos, out TriadCard bestNextCard, out TriadGameResultChance bestChance);
                            OnSolvedDeck(ctx.passId, ctx.deckIdx, bestChance);
                        };

                        new TaskFactory().StartNew(solverAction, calcContext);
                    }
                }
            }
        }

        private void OnSolvedDeck(int passId, int deckIdx, TriadGameResultChance winChance)
        {
            if (preGameId != passId)
            {
                return;
            }

            lock (preGameLock)
            {
                if (deckIdx >= 0 && deckIdx < preGameDeckChance.Count)
                {
                    preGameDeckChance[deckIdx] = winChance;
                    // TODO: broadcast? (this is still worker thread!)
                    PluginLog.Log($"deck: {deckIdx}, result:{winChance.expectedResult}, win%:{winChance.winChance:P0}, draw%:{winChance.drawChance:P0}");
                }
            }
        }
    }
}
