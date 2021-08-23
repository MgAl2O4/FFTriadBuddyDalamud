using FFTriadBuddy;
using MgAl2O4.Utils;
using System;

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

        private TriadGameScreenMemory screenMemory = new TriadGameScreenMemory();

        public TriadNpc currentNpc;
        public TriadCard moveCard => screenMemory.deckBlue?.GetCard(moveCardIdx);
        public int moveCardIdx;
        public int moveBoardIdx;
        public TriadGameResultChance moveWinChance;
        public bool hasMove;

        public Status status;
        public bool HasErrors => status != Status.NoErrors;

        public event Action<bool> OnMoveChanged;

        public Solver()
        {
            TriadGameSession.StaticInitialize();
        }

        public void Update(TriadGameUIState stateOb)
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
    }
}
