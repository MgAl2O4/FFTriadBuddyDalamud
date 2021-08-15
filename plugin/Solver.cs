using FFTriadBuddy;
using MgAl2O4.Utils;
using System;

namespace TriadBuddyPlugin
{
    public class Solver
    {
        private TriadGameScreenMemory screenMemory = new TriadGameScreenMemory();

        public TriadNpc currentNpc;
        public TriadCard moveCard => screenMemory.deckBlue?.GetCard(moveCardIdx);
        public int moveCardIdx;
        public int moveBoardIdx;
        public TriadGameResultChance moveWinChance;
        public bool hasMove;

        public event Action<bool> OnMoveChanged;

        public Solver()
        {
            TriadGameSession.StaticInitialize();
        }

        public void Update(GameUI gameUI)
        {
            if (gameUI == null || gameUI.currentState == null)
            {
                currentNpc = null;
                if (hasMove)
                {
                    hasMove = false;
                    OnMoveChanged?.Invoke(hasMove);
                }

                return;
            }

            var (screenOb, screenNpc) = gameUI.ConvertToTriadScreen();
            currentNpc = screenNpc;

            if (screenOb.turnState == ScannerTriad.ETurnState.Active)
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
