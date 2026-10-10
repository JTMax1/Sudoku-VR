using System;
using UnityEngine;
using SudokuVR.Networking;

namespace SudokuVR.Core
{
    /// <summary>
    /// Encapsulates the 9x9 Sudoku board state using flat 81-byte arrays.
    /// Manages cell mutations, conflict tracking, prefilled immutability,
    /// and event-driven notifications for local and networked gameplay.
    /// </summary>
    public class SudokuBoardState
    {
        // Flat state arrays (81 bytes each)
        private readonly byte[] _initialPuzzle = new byte[SudokuData.CellCount];
        private readonly byte[] _currentValues = new byte[SudokuData.CellCount];
        private readonly byte[] _solution = new byte[SudokuData.CellCount];
        private readonly bool[] _isPrefilled = new bool[SudokuData.CellCount];
        private readonly bool[] _isConflict = new bool[SudokuData.CellCount];
        private readonly PlayerId[] _cellOwners = new PlayerId[SudokuData.CellCount];

        // Active peer selections
        private readonly PlayerId[] _selectedBy = new PlayerId[SudokuData.CellCount];

        // Events
        public event Action<int, int, int, PlayerId> OnCellModified;
        public event Action<int, int, PlayerId> OnCellSelected;
        public event Action<int, int, PlayerId> OnCellDeselected;
        public event Action<bool> OnBoardCompleted;
        public event Action OnBoardReset;

        public bool IsInitialized { get; private set; }
        public bool IsCompleted { get; private set; }

        public byte[] InitialPuzzle => _initialPuzzle;
        public byte[] CurrentValues => _currentValues;
        public byte[] Solution => _solution;

        /// <summary>
        /// Initializes the board with a given puzzle and solution.
        /// </summary>
        public void Initialize(byte[] puzzle, byte[] solution)
        {
            if (puzzle == null || puzzle.Length < SudokuData.CellCount)
                throw new ArgumentException("Invalid puzzle array length.");
            if (solution == null || solution.Length < SudokuData.CellCount)
                throw new ArgumentException("Invalid solution array length.");

            Array.Copy(puzzle, _initialPuzzle, SudokuData.CellCount);
            Array.Copy(puzzle, _currentValues, SudokuData.CellCount);
            Array.Copy(solution, _solution, SudokuData.CellCount);

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                _isPrefilled[i] = _initialPuzzle[i] != 0;
                _isConflict[i] = false;
                _cellOwners[i] = _isPrefilled[i] ? PlayerId.ServerPlayer : PlayerId.InvalidPlayer;
                _selectedBy[i] = PlayerId.InvalidPlayer;
            }

            IsCompleted = false;
            IsInitialized = true;

            RecalculateConflicts();
            OnBoardReset?.Invoke();
        }

        /// <summary>
        /// Sets a cell value. Rejects modifications on immutable prefilled cells.
        /// </summary>
        public bool SetCell(int row, int col, int value, PlayerId player)
        {
            int index = SudokuData.GetIndex(row, col);
            if (index < 0 || index >= SudokuData.CellCount) return false;

            if (_isPrefilled[index])
            {
                // Cannot overwrite immutable prefilled clues
                return false;
            }

            byte valByte = (byte)Mathf.Clamp(value, 0, 9);
            if (_currentValues[index] == valByte)
            {
                return false; // No change
            }

            _currentValues[index] = valByte;
            _cellOwners[index] = valByte > 0 ? player : PlayerId.InvalidPlayer;

            RecalculateConflicts();

            OnCellModified?.Invoke(row, col, valByte, player);

            bool completed = CheckBoardCompletion();
            if (completed && !IsCompleted)
            {
                IsCompleted = true;
                OnBoardCompleted?.Invoke(true);
            }
            else if (!completed && IsCompleted)
            {
                IsCompleted = false;
                OnBoardCompleted?.Invoke(false);
            }

            return true;
        }

        public void SetSelection(int row, int col, PlayerId player)
        {
            int targetIdx = SudokuData.GetIndex(row, col);

            // Clear previous selection for this player
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_selectedBy[i] == player && i != targetIdx)
                {
                    _selectedBy[i] = PlayerId.InvalidPlayer;
                    OnCellDeselected?.Invoke(SudokuData.GetRow(i), SudokuData.GetCol(i), player);
                }
            }

            if (targetIdx >= 0 && targetIdx < SudokuData.CellCount)
            {
                _selectedBy[targetIdx] = player;
                OnCellSelected?.Invoke(row, col, player);
            }
        }

        public void ClearSelection(PlayerId player)
        {
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_selectedBy[i] == player)
                {
                    _selectedBy[i] = PlayerId.InvalidPlayer;
                    OnCellDeselected?.Invoke(SudokuData.GetRow(i), SudokuData.GetCol(i), player);
                }
            }
        }

        public byte GetCellValue(int row, int col)
        {
            int index = SudokuData.GetIndex(row, col);
            return (index >= 0 && index < SudokuData.CellCount) ? _currentValues[index] : (byte)0;
        }

        public bool IsCellPrefilled(int row, int col)
        {
            int index = SudokuData.GetIndex(row, col);
            return index >= 0 && index < SudokuData.CellCount && _isPrefilled[index];
        }

        public bool IsCellConflict(int row, int col)
        {
            int index = SudokuData.GetIndex(row, col);
            return index >= 0 && index < SudokuData.CellCount && _isConflict[index];
        }

        public PlayerId GetCellOwner(int row, int col)
        {
            int index = SudokuData.GetIndex(row, col);
            return (index >= 0 && index < SudokuData.CellCount) ? _cellOwners[index] : PlayerId.InvalidPlayer;
        }

        public PlayerId GetSelectedBy(int row, int col)
        {
            int index = SudokuData.GetIndex(row, col);
            return (index >= 0 && index < SudokuData.CellCount) ? _selectedBy[index] : PlayerId.InvalidPlayer;
        }

        /// <summary>
        /// Recalculates conflicts across all 81 cells using precomputed peer tables.
        /// Zero allocations.
        /// </summary>
        public void RecalculateConflicts()
        {
            Array.Clear(_isConflict, 0, SudokuData.CellCount);

            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                byte val = _currentValues[i];
                if (val == 0) continue;

                // Check all 20 precomputed peers
                for (int p = 0; p < 20; p++)
                {
                    int peerIdx = SudokuData.GetPeerIndex(i, p);
                    if (_currentValues[peerIdx] == val)
                    {
                        _isConflict[i] = true;
                        _isConflict[peerIdx] = true;
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the board is completely and correctly solved.
        /// </summary>
        public bool CheckBoardCompletion()
        {
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_currentValues[i] == 0 || _isConflict[i])
                {
                    return false;
                }
            }
            return true;
        }

        #region Network Serialization Helpers
        /// <summary>
        /// Exports board state into a contiguous byte buffer for network replication.
        /// Payload: 81 bytes current values + 81 bytes initial puzzle. Total = 162 bytes.
        /// </summary>
        public byte[] ExportStatePayload()
        {
            byte[] payload = new byte[SudokuData.CellCount * 2];
            Buffer.BlockCopy(_currentValues, 0, payload, 0, SudokuData.CellCount);
            Buffer.BlockCopy(_initialPuzzle, 0, payload, SudokuData.CellCount, SudokuData.CellCount);
            return payload;
        }

        public void ImportStatePayload(byte[] payload, byte[] solutionGrid)
        {
            if (payload == null || payload.Length < SudokuData.CellCount * 2) return;

            byte[] newPuzzle = new byte[SudokuData.CellCount];
            byte[] newValues = new byte[SudokuData.CellCount];

            Buffer.BlockCopy(payload, SudokuData.CellCount, newPuzzle, 0, SudokuData.CellCount);
            Buffer.BlockCopy(payload, 0, newValues, 0, SudokuData.CellCount);

            Initialize(newPuzzle, solutionGrid);
            Array.Copy(newValues, _currentValues, SudokuData.CellCount);
            RecalculateConflicts();
            CheckBoardCompletion();
        }
        #endregion
    }
}
