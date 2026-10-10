using System.Collections.Generic;

namespace SudokuVR.Core
{
    public struct BoardMove
    {
        public int Row;
        public int Col;
        public byte PreviousValue;
        public byte NewValue;
        public ushort PreviousNotesMask;
        public ushort NewNotesMask;

        public BoardMove(int row, int col, byte prevVal, byte newVal, ushort prevNotes, ushort newNotes)
        {
            Row = row;
            Col = col;
            PreviousValue = prevVal;
            NewValue = newVal;
            PreviousNotesMask = prevNotes;
            NewNotesMask = newNotes;
        }
    }

    /// <summary>
    /// Command stack for board modifications supporting full Undo and Redo operations.
    /// Handles both main digits and pencil/note annotations.
    /// </summary>
    public class MoveHistory
    {
        private readonly Stack<BoardMove> _undoStack = new Stack<BoardMove>();
        private readonly Stack<BoardMove> _redoStack = new Stack<BoardMove>();

        public int UndoCount => _undoStack.Count;
        public int RedoCount => _redoStack.Count;
        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void RecordMove(int row, int col, byte prevVal, byte newVal, ushort prevNotes, ushort newNotes)
        {
            _undoStack.Push(new BoardMove(row, col, prevVal, newVal, prevNotes, newNotes));
            _redoStack.Clear(); // Invalidate redo stack on new action
        }

        public bool TryUndo(out BoardMove move)
        {
            if (_undoStack.Count > 0)
            {
                move = _undoStack.Pop();
                _redoStack.Push(move);
                return true;
            }

            move = default;
            return false;
        }

        public bool TryRedo(out BoardMove move)
        {
            if (_redoStack.Count > 0)
            {
                move = _redoStack.Pop();
                _undoStack.Push(move);
                return true;
            }

            move = default;
            return false;
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
        }
    }
}
