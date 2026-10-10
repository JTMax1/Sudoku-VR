using System;

namespace SudokuVR.Core
{
    /// <summary>
    /// Zero-allocation, high performance Sudoku backtracking solver and candidate evaluator.
    /// Uses bitwise operations for O(1) peer checking and MRV (Minimum Remaining Values) heuristic.
    /// </summary>
    public class SudokuSolver
    {
        private readonly byte[] _workingGrid = new byte[SudokuData.CellCount];
        private readonly ushort[] _rowMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _colMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _blockMasks = new ushort[SudokuData.BoardSize];

        /// <summary>
        /// Solves the puzzle into solutionOut. Returns true if a solution was found.
        /// </summary>
        public bool Solve(byte[] puzzle, byte[] solutionOut)
        {
            if (puzzle == null || solutionOut == null) return false;
            Array.Copy(puzzle, _workingGrid, SudokuData.CellCount);
            RebuildMasksFromGrid(_workingGrid);

            if (SolveRecursive(0))
            {
                Array.Copy(_workingGrid, solutionOut, SudokuData.CellCount);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Counts the number of solutions up to maxCount.
        /// </summary>
        public int CountSolutions(byte[] grid, int maxCount = 2)
        {
            Array.Copy(grid, _workingGrid, SudokuData.CellCount);
            RebuildMasksFromGrid(_workingGrid);

            int solutionCount = 0;
            SolveCountRecursive(0, ref solutionCount, maxCount);
            return solutionCount;
        }

        /// <summary>
        /// Returns the bitmask of available digits (1-9) for a specific cell in the grid.
        /// </summary>
        public ushort GetCandidatesMask(byte[] grid, int cellIndex)
        {
            if (cellIndex < 0 || cellIndex >= SudokuData.CellCount || grid[cellIndex] != 0)
                return 0;

            int row = SudokuData.GetRow(cellIndex);
            int col = SudokuData.GetCol(cellIndex);
            int block = SudokuData.GetBlock(cellIndex);

            ushort used = 0;
            for (int c = 0; c < SudokuData.BoardSize; c++)
            {
                byte val = grid[SudokuData.GetIndex(row, c)];
                if (val > 0) used |= SudokuData.DigitToMask(val);
            }

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                byte val = grid[SudokuData.GetIndex(r, col)];
                if (val > 0) used |= SudokuData.DigitToMask(val);
            }

            int blockStartRow = (row / SudokuData.BlockSize) * SudokuData.BlockSize;
            int blockStartCol = (col / SudokuData.BlockSize) * SudokuData.BlockSize;
            for (int br = 0; br < SudokuData.BlockSize; br++)
            {
                for (int bc = 0; bc < SudokuData.BlockSize; bc++)
                {
                    byte val = grid[SudokuData.GetIndex(blockStartRow + br, blockStartCol + bc)];
                    if (val > 0) used |= SudokuData.DigitToMask(val);
                }
            }

            return (ushort)(SudokuData.AllDigitsMask & ~used);
        }

        private bool SolveRecursive(int cellIdx)
        {
            int bestCell = -1;
            int minCandidates = 10;
            ushort bestMask = 0;

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_workingGrid[i] == 0)
                {
                    int r = SudokuData.GetRow(i);
                    int c = SudokuData.GetCol(i);
                    int b = SudokuData.GetBlock(i);

                    ushort used = (ushort)(_rowMasks[r] | _colMasks[c] | _blockMasks[b]);
                    ushort available = (ushort)(SudokuData.AllDigitsMask & ~used);
                    int count = SudokuData.CountBits(available);

                    if (count == 0) return false;

                    if (count < minCandidates)
                    {
                        minCandidates = count;
                        bestCell = i;
                        bestMask = available;
                        if (count == 1) break;
                    }
                }
            }

            if (bestCell == -1) return true;

            int row = SudokuData.GetRow(bestCell);
            int col = SudokuData.GetCol(bestCell);
            int block = SudokuData.GetBlock(bestCell);

            for (int digit = 1; digit <= 9; digit++)
            {
                ushort bit = SudokuData.DigitToMask(digit);
                if ((bestMask & bit) != 0)
                {
                    _workingGrid[bestCell] = (byte)digit;
                    _rowMasks[row] |= bit;
                    _colMasks[col] |= bit;
                    _blockMasks[block] |= bit;

                    if (SolveRecursive(cellIdx + 1)) return true;

                    _workingGrid[bestCell] = 0;
                    _rowMasks[row] &= (ushort)~bit;
                    _colMasks[col] &= (ushort)~bit;
                    _blockMasks[block] &= (ushort)~bit;
                }
            }

            return false;
        }

        private void SolveCountRecursive(int cellIdx, ref int solutionCount, int maxCount)
        {
            if (solutionCount >= maxCount) return;

            int bestCell = -1;
            int minCandidates = 10;
            ushort bestMask = 0;

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_workingGrid[i] == 0)
                {
                    int r = SudokuData.GetRow(i);
                    int c = SudokuData.GetCol(i);
                    int b = SudokuData.GetBlock(i);

                    ushort used = (ushort)(_rowMasks[r] | _colMasks[c] | _blockMasks[b]);
                    ushort available = (ushort)(SudokuData.AllDigitsMask & ~used);
                    int count = SudokuData.CountBits(available);

                    if (count == 0) return;

                    if (count < minCandidates)
                    {
                        minCandidates = count;
                        bestCell = i;
                        bestMask = available;
                        if (count == 1) break;
                    }
                }
            }

            if (bestCell == -1)
            {
                solutionCount++;
                return;
            }

            int row = SudokuData.GetRow(bestCell);
            int col = SudokuData.GetCol(bestCell);
            int block = SudokuData.GetBlock(bestCell);

            for (int digit = 1; digit <= 9; digit++)
            {
                ushort bit = SudokuData.DigitToMask(digit);
                if ((bestMask & bit) != 0)
                {
                    _workingGrid[bestCell] = (byte)digit;
                    _rowMasks[row] |= bit;
                    _colMasks[col] |= bit;
                    _blockMasks[block] |= bit;

                    SolveCountRecursive(cellIdx + 1, ref solutionCount, maxCount);

                    _workingGrid[bestCell] = 0;
                    _rowMasks[row] &= (ushort)~bit;
                    _colMasks[col] &= (ushort)~bit;
                    _blockMasks[block] &= (ushort)~bit;

                    if (solutionCount >= maxCount) return;
                }
            }
        }

        private void RebuildMasksFromGrid(byte[] grid)
        {
            Array.Clear(_rowMasks, 0, SudokuData.BoardSize);
            Array.Clear(_colMasks, 0, SudokuData.BoardSize);
            Array.Clear(_blockMasks, 0, SudokuData.BoardSize);

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                byte val = grid[i];
                if (val >= 1 && val <= 9)
                {
                    int r = SudokuData.GetRow(i);
                    int c = SudokuData.GetCol(i);
                    int b = SudokuData.GetBlock(i);
                    ushort bit = SudokuData.DigitToMask(val);

                    _rowMasks[r] |= bit;
                    _colMasks[c] |= bit;
                    _blockMasks[b] |= bit;
                }
            }
        }
    }
}
