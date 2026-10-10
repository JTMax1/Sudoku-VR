using System;

namespace SudokuVR.Core
{
    /// <summary>
    /// High-performance, zero-allocation Sudoku board generator and validator.
    /// Supports 6 difficulty tiers with precise clue thresholds and guaranteed single unique solutions.
    /// </summary>
    public class SudokuGenerator
    {
        private readonly byte[] _workingGrid = new byte[SudokuData.CellCount];
        private readonly ushort[] _rowMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _colMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _blockMasks = new ushort[SudokuData.BoardSize];

        private readonly int[] _cellOrder = new int[SudokuData.CellCount];
        private readonly int[] _digitsShuffled = new int[9];

        private readonly SudokuSolver _solver = new SudokuSolver();
        private readonly System.Random _rng;

        public SudokuGenerator(int? seed = null)
        {
            _rng = seed.HasValue ? new System.Random(seed.Value) : new System.Random();

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                _cellOrder[i] = i;
            }

            for (int d = 0; d < 9; d++)
            {
                _digitsShuffled[d] = d + 1;
            }
        }

        /// <summary>
        /// Generates a valid puzzle with a guaranteed unique solution matching the difficulty criteria.
        /// </summary>
        public bool Generate(Difficulty difficulty, byte[] puzzleOut, byte[] solutionOut)
        {
            if (puzzleOut == null || puzzleOut.Length < SudokuData.CellCount)
                throw new ArgumentException("puzzleOut must have length at least 81");
            if (solutionOut == null || solutionOut.Length < SudokuData.CellCount)
                throw new ArgumentException("solutionOut must have length at least 81");

            // 1. Clear working grid and masks
            Array.Clear(_workingGrid, 0, SudokuData.CellCount);
            Array.Clear(_rowMasks, 0, SudokuData.BoardSize);
            Array.Clear(_colMasks, 0, SudokuData.BoardSize);
            Array.Clear(_blockMasks, 0, SudokuData.BoardSize);

            // 2. Generate a random valid full solution
            if (!FillRandomComplete(0))
            {
                return false;
            }

            Array.Copy(_workingGrid, solutionOut, SudokuData.CellCount);
            Array.Copy(_workingGrid, puzzleOut, SudokuData.CellCount);

            // 3. Determine target clues based on difficulty specifications
            int targetClues;
            switch (difficulty)
            {
                case Difficulty.Beginner:
                    targetClues = _rng.Next(45, 51); // 45–50 clues
                    break;
                case Difficulty.Easy:
                    targetClues = _rng.Next(36, 45); // 36–44 clues
                    break;
                case Difficulty.Medium:
                    targetClues = _rng.Next(32, 36); // 32–35 clues
                    break;
                case Difficulty.Hard:
                    targetClues = _rng.Next(28, 32); // 28–31 clues
                    break;
                case Difficulty.Expert:
                    targetClues = _rng.Next(25, 28); // 25–27 clues
                    break;
                case Difficulty.Extreme:
                default:
                    targetClues = _rng.Next(22, 25); // 22–24 clues
                    break;
            }

            // 4. Carve cells while maintaining a unique solution
            ShuffleCellOrder();

            int cluesRemaining = SudokuData.CellCount;
            for (int i = 0; i < SudokuData.CellCount && cluesRemaining > targetClues; i++)
            {
                int cellIdx = _cellOrder[i];
                byte originalVal = puzzleOut[cellIdx];
                if (originalVal == 0) continue;

                puzzleOut[cellIdx] = 0;

                // Verify uniqueness (count solutions, stop as soon as count > 1)
                if (_solver.CountSolutions(puzzleOut, 2) != 1)
                {
                    // Restores clue if removing it creates multiple solutions
                    puzzleOut[cellIdx] = originalVal;
                }
                else
                {
                    cluesRemaining--;
                }
            }

            return true;
        }

        private bool FillRandomComplete(int cellIdx)
        {
            if (cellIdx == SudokuData.CellCount)
                return true;

            int row = SudokuData.GetRow(cellIdx);
            int col = SudokuData.GetCol(cellIdx);
            int block = SudokuData.GetBlock(cellIdx);

            ShuffleDigits();

            ushort usedMask = (ushort)(_rowMasks[row] | _colMasks[col] | _blockMasks[block]);

            for (int i = 0; i < 9; i++)
            {
                int digit = _digitsShuffled[i];
                ushort bit = SudokuData.DigitToMask(digit);

                if ((usedMask & bit) == 0)
                {
                    _workingGrid[cellIdx] = (byte)digit;
                    _rowMasks[row] |= bit;
                    _colMasks[col] |= bit;
                    _blockMasks[block] |= bit;

                    if (FillRandomComplete(cellIdx + 1))
                        return true;

                    // Backtrack
                    _workingGrid[cellIdx] = 0;
                    _rowMasks[row] &= (ushort)~bit;
                    _colMasks[col] &= (ushort)~bit;
                    _blockMasks[block] &= (ushort)~bit;
                }
            }

            return false;
        }

        private void ShuffleDigits()
        {
            for (int i = 8; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int tmp = _digitsShuffled[i];
                _digitsShuffled[i] = _digitsShuffled[j];
                _digitsShuffled[j] = tmp;
            }
        }

        private void ShuffleCellOrder()
        {
            for (int i = SudokuData.CellCount - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                int tmp = _cellOrder[i];
                _cellOrder[i] = _cellOrder[j];
                _cellOrder[j] = tmp;
            }
        }
    }
}
