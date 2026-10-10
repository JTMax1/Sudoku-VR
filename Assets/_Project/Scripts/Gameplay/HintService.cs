using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    public struct HintResult
    {
        public bool Found;
        public int CellIndex;
        public int Row;
        public int Col;
        public int Digit;
        public string Technique;
        public string Description;
    }

    /// <summary>
    /// Evaluates logical Sudoku deductions (Naked Single, Hidden Single) or provides
    /// guided candidate hints without guessing.
    /// </summary>
    public class HintService : MonoBehaviour
    {
        private readonly SudokuSolver _solver = new SudokuSolver();

        public HintResult GetSmartHint(SudokuBoardState boardState)
        {
            HintResult result = new HintResult();
            if (boardState == null || !boardState.IsInitialized) return result;

            byte[] current = boardState.CurrentValues;

            // 1. Try to find a Naked Single (a cell with only 1 possible candidate)
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (current[i] == 0)
                {
                    ushort mask = _solver.GetCandidatesMask(current, i);
                    if (SudokuData.CountBits(mask) == 1)
                    {
                        int digit = SudokuData.MaskToDigit(mask);
                        result.Found = true;
                        result.CellIndex = i;
                        result.Row = SudokuData.GetRow(i);
                        result.Col = SudokuData.GetCol(i);
                        result.Digit = digit;
                        result.Technique = "Naked Single";
                        result.Description = $"Row {result.Row + 1}, Col {result.Col + 1} has only one valid candidate: {digit}!";
                        return result;
                    }
                }
            }

            // 2. Try to find a Hidden Single in 3x3 blocks
            for (int b = 0; b < SudokuData.BoardSize; b++)
            {
                int blockStartR = (b / SudokuData.BlockSize) * SudokuData.BlockSize;
                int blockStartC = (b % SudokuData.BlockSize) * SudokuData.BlockSize;

                for (int digit = 1; digit <= 9; digit++)
                {
                    int possibleCell = -1;
                    int count = 0;

                    for (int br = 0; br < SudokuData.BlockSize; br++)
                    {
                        for (int bc = 0; bc < SudokuData.BlockSize; bc++)
                        {
                            int idx = SudokuData.GetIndex(blockStartR + br, blockStartC + bc);
                            if (current[idx] == 0)
                            {
                                ushort mask = _solver.GetCandidatesMask(current, idx);
                                if ((mask & SudokuData.DigitToMask(digit)) != 0)
                                {
                                    count++;
                                    possibleCell = idx;
                                }
                            }
                        }
                    }

                    if (count == 1 && possibleCell >= 0)
                    {
                        result.Found = true;
                        result.CellIndex = possibleCell;
                        result.Row = SudokuData.GetRow(possibleCell);
                        result.Col = SudokuData.GetCol(possibleCell);
                        result.Digit = digit;
                        result.Technique = "Hidden Single";
                        result.Description = $"In this 3x3 block, {digit} can only fit in Row {result.Row + 1}, Col {result.Col + 1}!";
                        return result;
                    }
                }
            }

            // 3. Fallback: Provide solution digit for the first unresolved empty cell
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (current[i] == 0)
                {
                    int r = SudokuData.GetRow(i);
                    int c = SudokuData.GetCol(i);
                    byte solVal = boardState.GetSolutionValue(r, c);

                    result.Found = true;
                    result.CellIndex = i;
                    result.Row = r;
                    result.Col = c;
                    result.Digit = solVal;
                    result.Technique = "Direct Clue";
                    result.Description = $"Solution clue: Row {r + 1}, Col {c + 1} is {solVal}.";
                    return result;
                }
            }

            return result;
        }
    }
}
