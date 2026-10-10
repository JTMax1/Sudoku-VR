using System;
using UnityEngine;

namespace SudokuVR.Core
{
    public enum Difficulty
    {
        Beginner = 0,
        Easy = 1,
        Medium = 2,
        Hard = 3,
        Expert = 4,
        Extreme = 5
    }

    public enum CellVisualState
    {
        Default = 0,
        Prefilled = 1,
        Selected = 2,
        Related = 3,
        IdenticalNumber = 4,
        Conflict = 5,
        Correct = 6,
        PeerSelected = 7,
        PencilNotes = 8
    }

    /// <summary>
    /// Static lookup tables and bitmask helpers for 9x9 Sudoku grid mathematics.
    /// Fully pre-calculated to ensure zero allocations during runtime validation and gameplay.
    /// </summary>
    public static class SudokuData
    {
        public const int BoardSize = 9;
        public const int CellCount = 81;
        public const int BlockSize = 3;

        // Bitmask for valid digits 1-9: (1 << 1) | (1 << 2) ... | (1 << 9) = 0x3FE
        public const ushort AllDigitsMask = 0x03FE;

        // Precalculated peer indices: Each of the 81 cells has exactly 20 distinct peers (8 row + 8 col + 4 block)
        private static readonly int[,] PrecalcPeers = new int[CellCount, 20];
        private static readonly int[] PrecalcRow = new int[CellCount];
        private static readonly int[] PrecalcCol = new int[CellCount];
        private static readonly int[] PrecalcBlock = new int[CellCount];

        static SudokuData()
        {
            // Initialize row, column, block lookup for each index 0..80
            for (int i = 0; i < CellCount; i++)
            {
                int r = i / BoardSize;
                int c = i % BoardSize;
                int b = (r / BlockSize) * BlockSize + (c / BlockSize);

                PrecalcRow[i] = r;
                PrecalcCol[i] = c;
                PrecalcBlock[i] = b;
            }

            // Precalculate 20 peers for each cell to eliminate set allocations during validation
            for (int i = 0; i < CellCount; i++)
            {
                int r = PrecalcRow[i];
                int c = PrecalcCol[i];
                int b = PrecalcBlock[i];

                int peerIdx = 0;

                // Same row
                for (int col = 0; col < BoardSize; col++)
                {
                    int neighbor = GetIndex(r, col);
                    if (neighbor != i && !Contains(PrecalcPeers, i, peerIdx, neighbor))
                    {
                        PrecalcPeers[i, peerIdx++] = neighbor;
                    }
                }

                // Same column
                for (int row = 0; row < BoardSize; row++)
                {
                    int neighbor = GetIndex(row, c);
                    if (neighbor != i && !Contains(PrecalcPeers, i, peerIdx, neighbor))
                    {
                        PrecalcPeers[i, peerIdx++] = neighbor;
                    }
                }

                // Same 3x3 block
                int blockStartRow = (r / BlockSize) * BlockSize;
                int blockStartCol = (c / BlockSize) * BlockSize;
                for (int br = 0; br < BlockSize; br++)
                {
                    for (int bc = 0; bc < BlockSize; bc++)
                    {
                        int neighbor = GetIndex(blockStartRow + br, blockStartCol + bc);
                        if (neighbor != i && !Contains(PrecalcPeers, i, peerIdx, neighbor))
                        {
                            PrecalcPeers[i, peerIdx++] = neighbor;
                        }
                    }
                }
            }
        }

        private static bool Contains(int[,] array, int row, int count, int value)
        {
            for (int i = 0; i < count; i++)
            {
                if (array[row, i] == value) return true;
            }
            return false;
        }

        public static int GetIndex(int row, int col) => row * BoardSize + col;
        public static int GetRow(int index) => PrecalcRow[index];
        public static int GetCol(int index) => PrecalcCol[index];
        public static int GetBlock(int index) => PrecalcBlock[index];
        public static int GetBlock(int row, int col) => (row / BlockSize) * BlockSize + (col / BlockSize);

        public static int GetPeerIndex(int cellIndex, int peerNum) => PrecalcPeers[cellIndex, peerNum];

        public static ushort DigitToMask(int digit)
        {
            if (digit < 1 || digit > 9) return 0;
            return (ushort)(1 << digit);
        }

        public static int MaskToDigit(ushort mask)
        {
            for (int d = 1; d <= 9; d++)
            {
                if ((mask & (1 << d)) != 0) return d;
            }
            return 0;
        }

        public static int CountBits(ushort mask)
        {
            int count = 0;
            while (mask != 0)
            {
                mask &= (ushort)(mask - 1);
                count++;
            }
            return count;
        }
    }
}
