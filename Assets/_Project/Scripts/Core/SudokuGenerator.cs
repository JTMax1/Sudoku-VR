using System;
using UnityEngine;

namespace SudokuVR.Core
{
    /// <summary>
    /// High-performance, zero-allocation Sudoku board generator and backtracking solver.
    /// Uses bitmasks for O(1) candidate checks and pre-allocated buffers.
    /// </summary>
    public class SudokuGenerator
    {
        // Pre-allocated internal buffers to guarantee zero GC allocations during solving/generating
        private readonly byte[] _workingGrid = new byte[SudokuData.CellCount];
        private readonly ushort[] _rowMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _colMasks = new ushort[SudokuData.BoardSize];
        private readonly ushort[] _blockMasks = new ushort[SudokuData.BoardSize];

        private readonly int[] _cellOrder = new int[SudokuData.CellCount];
        private readonly int[] _digitsShuffled = new int[9];

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
        /// Generates a new puzzle and its unique solution based on difficulty.
        /// </summary>
        /// <param name="difficulty">Easy (~38 clues), Medium (~30 clues), Hard (~25 clues)</param>
        /// <param name="puzzleOut">Pre-allocated 81-byte array for output clues</param>
        /// <param name="solutionOut">Pre-allocated 81-byte array for output solution</param>
        /// <returns>True if generation succeeded</returns>
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

            // Copy solution to solutionOut
            Array.Copy(_workingGrid, solutionOut, SudokuData.CellCount);
            Array.Copy(_workingGrid, puzzleOut, SudokuData.CellCount);

            // 3. Determine target clues based on difficulty
            int targetClues;
            switch (difficulty)
            {
                case Difficulty.Easy:
                    targetClues = _rng.Next(36, 40); // 36 - 39 clues
                    break;
                case Difficulty.Medium:
                    targetClues = _rng.Next(30, 34); // 30 - 33 clues
                    break;
                case Difficulty.Hard:
                default:
                    targetClues = _rng.Next(24, 28); // 24 - 27 clues
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

                // Temporarily remove clue
                puzzleOut[cellIdx] = 0;

                // Verify uniqueness (count solutions, stop as soon as count > 1)
                if (CountSolutions(puzzleOut, 2) != 1)
                {
                    // Cannot remove this cell, put it back
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

        /// <summary>
        /// Counts the number of solutions for a given grid up to maxCount.
        /// Zero allocations.
        /// </summary>
        public int CountSolutions(byte[] grid, int maxCount = 2)
        {
            Array.Copy(grid, _workingGrid, SudokuData.CellCount);
            RebuildMasksFromGrid(_workingGrid);

            int solutionCount = 0;
            SolveCountRecursive(0, ref solutionCount, maxCount);
            return solutionCount;
        }

        private void SolveCountRecursive(int cellIdx, ref int solutionCount, int maxCount)
        {
            if (solutionCount >= maxCount) return;

            // Find next empty cell with Minimum Remaining Values (MRV heuristic for speed)
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

                    if (count == 0)
                    {
                        // Dead end
                        return;
                    }

                    if (count < minCandidates)
                    {
                        minCandidates = count;
                        bestCell = i;
                        bestMask = available;
                        if (count == 1) break; // Can't beat 1 candidate
                    }
                }
            }

            // If no empty cells remain, we found a valid solution
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
