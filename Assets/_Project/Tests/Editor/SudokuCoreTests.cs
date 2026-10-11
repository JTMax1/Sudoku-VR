using NUnit.Framework;
using SudokuVR.Core;

namespace SudokuVR.Tests
{
    [TestFixture]
    public class SudokuCoreTests
    {
        private SudokuSolver _solver;
        private SudokuGenerator _generator;

        [SetUp]
        public void Setup()
        {
            _solver = new SudokuSolver();
            _generator = new SudokuGenerator(42);
        }

        [Test]
        public void Solver_SolvesKnownPuzzle_Correctly()
        {
            // Valid known puzzle
            byte[] puzzle = new byte[SudokuData.CellCount];
            byte[] expectedSolution = new byte[SudokuData.CellCount];
            bool hasSeed = VerifiedPuzzleCatalogue.TryGetSeed(Difficulty.Beginner, 0, puzzle, expectedSolution);
            Assert.IsTrue(hasSeed, "Failed to retrieve Beginner seed from VerifiedPuzzleCatalogue");

            byte[] solvedOut = new byte[SudokuData.CellCount];
            bool solved = _solver.Solve(puzzle, solvedOut);

            Assert.IsTrue(solved, "SudokuSolver failed to solve valid puzzle.");
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                Assert.AreEqual(expectedSolution[i], solvedOut[i], $"Mismatch at cell index {i}");
            }
        }

        [Test]
        public void Solver_CountsSolutions_UniqueSolutionIsOne()
        {
            byte[] puzzle = new byte[SudokuData.CellCount];
            byte[] solution = new byte[SudokuData.CellCount];
            VerifiedPuzzleCatalogue.TryGetSeed(Difficulty.Easy, 0, puzzle, solution);

            int solutionCount = _solver.CountSolutions(puzzle, 2);
            Assert.AreEqual(1, solutionCount, "Expected unique solution count to be exactly 1.");
        }

        [Test]
        public void BoardState_ImmutableClues_CannotBeOverwrittenByPlayer()
        {
            byte[] puzzle = new byte[SudokuData.CellCount];
            byte[] solution = new byte[SudokuData.CellCount];
            VerifiedPuzzleCatalogue.TryGetSeed(Difficulty.Beginner, 0, puzzle, solution);

            var board = new SudokuBoardState();
            board.Initialize(puzzle, solution);

            // Find an initial clue
            int clueIndex = -1;
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (puzzle[i] != 0)
                {
                    clueIndex = i;
                    break;
                }
            }
            Assert.GreaterOrEqual(clueIndex, 0, "No clue found in puzzle.");

            int row = SudokuData.GetRow(clueIndex);
            int col = SudokuData.GetCol(clueIndex);
            byte originalVal = puzzle[clueIndex];

            // Attempt to overwrite
            byte attemptVal = (byte)(originalVal == 9 ? 1 : originalVal + 1);
            bool success = board.SetCell(row, col, attemptVal, Networking.PlayerId.ServerPlayer);

            Assert.IsFalse(success, "Immutable clue was erroneously modified.");
            Assert.AreEqual(originalVal, board.GetCellValue(row, col), "Clue value changed after illegal modification attempt.");
        }

        [Test]
        public void BoardState_PencilMarks_ToggleCorrectly()
        {
            byte[] puzzle = new byte[SudokuData.CellCount];
            byte[] solution = new byte[SudokuData.CellCount];
            VerifiedPuzzleCatalogue.TryGetSeed(Difficulty.Beginner, 0, puzzle, solution);

            var board = new SudokuBoardState();
            board.Initialize(puzzle, solution);

            // Find an empty cell
            int emptyIndex = -1;
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (puzzle[i] == 0)
                {
                    emptyIndex = i;
                    break;
                }
            }
            Assert.GreaterOrEqual(emptyIndex, 0, "No empty cell found in puzzle.");

            int row = SudokuData.GetRow(emptyIndex);
            int col = SudokuData.GetCol(emptyIndex);

            // Toggle note 5 ON
            board.ToggleNote(row, col, 5);
            Assert.IsTrue((board.GetNotes(row, col) & (1 << 5)) != 0, "Note 5 should be active.");

            // Toggle note 5 OFF
            board.ToggleNote(row, col, 5);
            Assert.IsFalse((board.GetNotes(row, col) & (1 << 5)) != 0, "Note 5 should be inactive.");
        }

        [Test]
        public void MoveHistory_UndoAndRedo_PreservesInvariants()
        {
            var history = new MoveHistory();
            Assert.AreEqual(0, history.UndoCount);
            Assert.IsFalse(history.CanUndo);
            Assert.IsFalse(history.CanRedo);

            history.RecordMove(2, 3, 0, 7, 0, 0);
            Assert.IsTrue(history.CanUndo);
            Assert.IsFalse(history.CanRedo);

            bool undone = history.TryUndo(out BoardMove move);
            Assert.IsTrue(undone);
            Assert.AreEqual(2, move.Row);
            Assert.AreEqual(3, move.Col);
            Assert.AreEqual(0, move.PreviousValue);
            Assert.AreEqual(7, move.NewValue);
            Assert.IsFalse(history.CanUndo);
            Assert.IsTrue(history.CanRedo);

            bool redone = history.TryRedo(out BoardMove redoMove);
            Assert.IsTrue(redone);
            Assert.AreEqual(7, redoMove.NewValue);
            Assert.IsTrue(history.CanUndo);
            Assert.IsFalse(history.CanRedo);
        }

        [Test]
        public void BoardState_CompletionDetection_IdentifiesCompleteBoard()
        {
            byte[] puzzle = new byte[SudokuData.CellCount];
            byte[] solution = new byte[SudokuData.CellCount];
            VerifiedPuzzleCatalogue.TryGetSeed(Difficulty.Beginner, 0, puzzle, solution);

            var board = new SudokuBoardState();
            board.Initialize(solution, solution); // Initialized directly as completed solution

            Assert.IsTrue(board.IsCompleted, "Board should report completed when all cells match valid solution.");
        }
    }
}
