using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Networking;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Manages the visual layout, cell view hierarchy, and responsive states of the 9x9 Sudoku grid.
    /// Sized for ergonomic VR tabletop interaction (approx 0.6m x 0.6m).
    /// </summary>
    public class SudokuBoardView : MonoBehaviour
    {
        [Header("Prefab Reference")]
        [SerializeField] private SudokuCellView cellPrefab;

        [Header("Board VR Metric Dimensions")]
        [Tooltip("Size of each individual cell in meters")]
        [SerializeField] private float cellSize = 0.056f;
        [Tooltip("Spacing between adjacent cells within a 3x3 block in meters")]
        [SerializeField] private float cellSpacing = 0.005f;
        [Tooltip("Additional spacing between 3x3 blocks in meters")]
        [SerializeField] private float blockSpacing = 0.015f;

        [Header("Board Backing Mesh")]
        [SerializeField] private Transform boardBackplate;

        private readonly SudokuCellView[,] _cells = new SudokuCellView[SudokuData.BoardSize, SudokuData.BoardSize];
        private int _selectedRow = -1;
        private int _selectedCol = -1;

        public event Action<int, int> OnCellSelectedByUser;

        public int SelectedRow => _selectedRow;
        public int SelectedCol => _selectedCol;
        public bool HasSelection => _selectedRow >= 0 && _selectedCol >= 0;

        public SudokuCellView GetCellView(int row, int col)
        {
            if (row < 0 || row >= SudokuData.BoardSize || col < 0 || col >= SudokuData.BoardSize)
                return null;
            return _cells[row, col];
        }

        /// <summary>
        /// Generates the 81 cell GameObjects and positions them in metric 3D space if not already created.
        /// </summary>
        public void BuildGridHierarchy()
        {
            float pitch = cellSize + cellSpacing;
            // Total board span calculation to center grid around local origin (0, 0, 0)
            float totalSpan = 8 * pitch + 2 * blockSpacing;
            float halfSpan = totalSpan * 0.5f;

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                int blockR = r / SudokuData.BlockSize;
                float zPos = halfSpan - (r * pitch + blockR * blockSpacing);

                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    int blockC = c / SudokuData.BlockSize;
                    float xPos = -halfSpan + (c * pitch + blockC * blockSpacing);

                    SudokuCellView cell = _cells[r, c];
                    if (cell == null)
                    {
                        Transform existingChild = transform.Find($"Cell_{r}_{c}");
                        if (existingChild != null)
                        {
                            cell = existingChild.GetComponent<SudokuCellView>();
                        }

                        if (cell == null)
                        {
                            if (cellPrefab != null)
                            {
                                cell = Instantiate(cellPrefab, transform);
                            }
                            else
                            {
                                cell = CreateProceduralCell(r, c);
                            }
                        }

                        _cells[r, c] = cell;
                    }

                    cell.transform.localPosition = new Vector3(xPos, 0.005f, zPos);
                    cell.transform.localRotation = Quaternion.identity;
                    cell.transform.localScale = new Vector3(cellSize, 0.01f, cellSize);
                    cell.Setup(r, c);

                    cell.OnCellClicked -= HandleCellClicked;
                    cell.OnCellClicked += HandleCellClicked;
                }
            }

            // Size backplate if present
            if (boardBackplate != null)
            {
                float plateMargin = 0.04f;
                float plateWidth = totalSpan + cellSize + plateMargin;
                boardBackplate.localScale = new Vector3(plateWidth, 0.02f, plateWidth);
                boardBackplate.localPosition = new Vector3(0f, -0.01f, 0f);
            }
        }

        public void BindToState(SudokuBoardState state)
        {
            if (state == null) return;

            // Unsubscribe existing
            state.OnCellModified -= HandleStateCellModified;
            state.OnCellSelected -= HandleStateCellSelected;
            state.OnCellDeselected -= HandleStateCellDeselected;
            state.OnBoardCompleted -= HandleStateBoardCompleted;
            state.OnBoardReset -= HandleStateBoardReset;

            // Subscribe
            state.OnCellModified += HandleStateCellModified;
            state.OnCellSelected += HandleStateCellSelected;
            state.OnCellDeselected += HandleStateCellDeselected;
            state.OnBoardCompleted += HandleStateBoardCompleted;
            state.OnBoardReset += HandleStateBoardReset;

            RefreshAllCells(state);
        }

        public void RefreshAllCells(SudokuBoardState state)
        {
            if (state == null || !state.IsInitialized) return;

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    SudokuCellView view = _cells[r, c];
                    if (view == null) continue;

                    bool prefilled = state.IsCellPrefilled(r, c);
                    byte val = state.GetCellValue(r, c);

                    view.SetPrefilled(prefilled);
                    view.SetValue(val);

                    UpdateCellVisual(state, r, c);
                }
            }

            UpdateRelatedHighlights();
        }

        private void UpdateCellVisual(SudokuBoardState state, int r, int c)
        {
            SudokuCellView view = _cells[r, c];
            if (view == null) return;

            PlayerId peer = state.GetSelectedBy(r, c);
            bool isSelected = (_selectedRow == r && _selectedCol == c);
            bool isConflict = state.IsCellConflict(r, c);
            bool isPrefilled = state.IsCellPrefilled(r, c);

            if (isSelected)
            {
                view.SetVisualState(CellVisualState.Selected);
            }
            else if (peer.IsValid && !peer.IsLocal)
            {
                view.SetVisualState(CellVisualState.PeerSelected, peer);
            }
            else if (isConflict)
            {
                view.SetVisualState(CellVisualState.Conflict);
            }
            else if (isPrefilled)
            {
                view.SetVisualState(CellVisualState.Prefilled);
            }
            else
            {
                view.SetVisualState(CellVisualState.Default);
            }
        }

        private void UpdateRelatedHighlights()
        {
            int selectedBlock = (_selectedRow >= 0 && _selectedCol >= 0)
                ? SudokuData.GetBlock(_selectedRow, _selectedCol)
                : -1;

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    SudokuCellView view = _cells[r, c];
                    if (view == null) continue;

                    if (_selectedRow < 0 || _selectedCol < 0)
                    {
                        view.SetRelatedHighlight(false);
                    }
                    else
                    {
                        bool isSameRow = (r == _selectedRow);
                        bool isSameCol = (c == _selectedCol);
                        bool isSameBlock = (SudokuData.GetBlock(r, c) == selectedBlock);
                        view.SetRelatedHighlight(isSameRow || isSameCol || isSameBlock);
                    }
                }
            }
        }

        public void SelectCell(int row, int col)
        {
            _selectedRow = row;
            _selectedCol = col;
            OnCellSelectedByUser?.Invoke(row, col);
        }

        public void ClearSelection()
        {
            _selectedRow = -1;
            _selectedCol = -1;
            UpdateRelatedHighlights();
        }

        private void HandleCellClicked(int row, int col)
        {
            SelectCell(row, col);
        }

        private void HandleStateCellModified(int row, int col, int value, PlayerId player)
        {
            SudokuCellView view = _cells[row, col];
            if (view != null)
            {
                view.SetValue(value);
            }
        }

        private void HandleStateCellSelected(int row, int col, PlayerId player)
        {
            SudokuCellView view = _cells[row, col];
            if (view == null) return;

            if (player.IsLocal)
            {
                _selectedRow = row;
                _selectedCol = col;
                view.SetVisualState(CellVisualState.Selected);
                UpdateRelatedHighlights();
            }
            else
            {
                view.SetVisualState(CellVisualState.PeerSelected, player);
            }
        }

        private void HandleStateCellDeselected(int row, int col, PlayerId player)
        {
            SudokuCellView view = _cells[row, col];
            if (view == null) return;

            if (player.IsLocal && _selectedRow == row && _selectedCol == col)
            {
                _selectedRow = -1;
                _selectedCol = -1;
                UpdateRelatedHighlights();
            }

            view.SetVisualState(view.IsPrefilled ? CellVisualState.Prefilled : CellVisualState.Default);
        }

        private void HandleStateBoardCompleted(bool isCompleted)
        {
            if (isCompleted)
            {
                for (int r = 0; r < SudokuData.BoardSize; r++)
                {
                    for (int c = 0; c < SudokuData.BoardSize; c++)
                    {
                        _cells[r, c]?.SetVisualState(CellVisualState.Correct);
                    }
                }
            }
        }

        private void HandleStateBoardReset()
        {
            ClearSelection();
        }

        private SudokuCellView CreateProceduralCell(int row, int col)
        {
            GameObject cellObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cellObj.name = $"Cell_{row}_{col}";
            cellObj.transform.SetParent(transform, false);

            SudokuCellView view = cellObj.AddComponent<SudokuCellView>();

            // Add TextMeshPro 3D text child
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(cellObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0.51f, 0f);
            textObj.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            textObj.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 40f;
            tmp.text = "";
            tmp.color = Color.white;

            return view;
        }
    }
}
