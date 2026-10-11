using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Interaction;
using SudokuVR.Networking;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Manages the 3D spatial monolith grid, Tile3DView hierarchy, and dynamic lighting highlights.
    /// Ergonomically sized for floating VR interaction (approx 0.55m x 0.55m).
    /// </summary>
    public class SudokuBoardView : MonoBehaviour
    {
        [Header("Prefab Reference (Optional)")]
        [SerializeField] private Tile3DView tilePrefab;

        [Header("Metric Board Dimensions (Meters)")]
        [SerializeField] private float cellSize = 0.052f;
        [SerializeField] private float cellSpacing = 0.005f;
        [SerializeField] private float blockSpacing = 0.016f;

        [Header("Monolith Backing Mesh")]
        [SerializeField] private Transform boardBackplate;

        private readonly Tile3DView[,] _tiles = new Tile3DView[SudokuData.BoardSize, SudokuData.BoardSize];
        private int _selectedRow = -1;
        private int _selectedCol = -1;
        private byte _selectedDigit = 0;

        public event Action<int, int> OnCellSelectedByUser;

        public int SelectedRow => _selectedRow;
        public int SelectedCol => _selectedCol;
        public byte SelectedDigit => _selectedDigit;
        public bool HasSelection => _selectedRow >= 0 && _selectedCol >= 0;

        public float TotalBoardSpan => 8 * (cellSize + cellSpacing) + 2 * blockSpacing + cellSize;

        public Tile3DView GetTileView(int row, int col)
        {
            if (row < 0 || row >= SudokuData.BoardSize || col < 0 || col >= SudokuData.BoardSize)
                return null;

            if (_tiles[row, col] == null)
            {
                Transform child = transform.Find($"Tile_{row}_{col}");
                if (child != null) _tiles[row, col] = child.GetComponent<Tile3DView>();
            }

            return _tiles[row, col];
        }

        public void EnsureTilesPopulated()
        {
            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    if (_tiles[r, c] == null)
                    {
                        Transform child = transform.Find($"Tile_{r}_{c}");
                        if (child != null)
                        {
                            Tile3DView tile = child.GetComponent<Tile3DView>();
                            _tiles[r, c] = tile;
                            if (tile != null)
                            {
                                tile.OnTileClicked -= HandleTileClicked;
                                tile.OnTileClicked += HandleTileClicked;
                            }
                        }
                    }
                }
            }
        }

        private void Awake()
        {
            EnsureTilesPopulated();
        }

        /// <summary>
        /// Generates the 81 tactile 3D tile monoliths in metric space centered around (0, 0, 0).
        /// </summary>
        public void BuildGridHierarchy()
        {
            float pitch = cellSize + cellSpacing;
            float totalSpan = 8 * pitch + 2 * blockSpacing;
            float halfSpan = totalSpan * 0.5f;

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                int blockR = r / SudokuData.BlockSize;
                // Vertical TV: Row 0 on Top (+Y), Row 8 on Bottom (-Y)
                float yPos = halfSpan - (r * pitch + blockR * blockSpacing);

                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    int blockC = c / SudokuData.BlockSize;
                    // Vertical TV: Col 0 on Left (-X), Col 8 on Right (+X)
                    float xPos = -halfSpan + (c * pitch + blockC * blockSpacing);

                    Tile3DView tile = _tiles[r, c];
                    if (tile == null)
                    {
                        Transform existingChild = transform.Find($"Tile_{r}_{c}");
                        if (existingChild != null)
                        {
                            tile = existingChild.GetComponent<Tile3DView>();
                        }

                        if (tile == null)
                        {
                            if (tilePrefab != null)
                            {
                                tile = Instantiate(tilePrefab, transform);
                            }
                            else
                            {
                                tile = CreateProceduralTile(r, c);
                            }
                        }

                        _tiles[r, c] = tile;
                    }

                    // Vertical TV layout: tile front face faces -Z towards player
                    tile.transform.localPosition = new Vector3(xPos, yPos, -0.008f);
                    tile.transform.localRotation = Quaternion.identity;
                    tile.transform.localScale = new Vector3(cellSize, cellSize, 0.014f);
                    tile.Setup(r, c);

                    // Re-align main digit text on front face
                    Transform textObj = tile.transform.Find("Text");
                    if (textObj != null)
                    {
                        textObj.localPosition = new Vector3(0f, 0f, -0.52f);
                        textObj.localRotation = Quaternion.identity;
                        textObj.localScale = new Vector3(0.09f, 0.09f, 0.09f);
                        TMP_Text tmp = textObj.GetComponent<TMP_Text>();
                        tile.SetMainDigitText(tmp);
                    }

                    // Re-align pencil notes grid on front face
                    Transform pencilGrid = tile.transform.Find("PencilGrid");
                    if (pencilGrid != null)
                    {
                        pencilGrid.localPosition = new Vector3(0f, 0f, -0.52f);
                        pencilGrid.localRotation = Quaternion.identity;
                    }

                    tile.OnTileClicked -= HandleTileClicked;
                    tile.OnTileClicked += HandleTileClicked;
                }
            }

            // Monolith backplate (stands vertical in X-Y plane behind tiles)
            if (boardBackplate != null)
            {
                float plateMargin = 0.035f;
                float plateWidth = totalSpan + cellSize + plateMargin;
                boardBackplate.localScale = new Vector3(plateWidth, plateWidth, 0.016f);
                boardBackplate.localPosition = new Vector3(0f, 0f, 0.008f);
                boardBackplate.localRotation = Quaternion.identity;
            }
        }

        public void BindToState(SudokuBoardState state)
        {
            if (state == null) return;

            state.OnCellModified -= HandleStateCellModified;
            state.OnCellNotesModified -= HandleStateCellNotesModified;
            state.OnCellSelected -= HandleStateCellSelected;
            state.OnCellDeselected -= HandleStateCellDeselected;
            state.OnBoardCompleted -= HandleStateBoardCompleted;
            state.OnBoardReset -= HandleStateBoardReset;

            state.OnCellModified += HandleStateCellModified;
            state.OnCellNotesModified += HandleStateCellNotesModified;
            state.OnCellSelected += HandleStateCellSelected;
            state.OnCellDeselected += HandleStateCellDeselected;
            state.OnBoardCompleted += HandleStateBoardCompleted;
            state.OnBoardReset += HandleStateBoardReset;

            RefreshAllCells(state);
        }

        public void RefreshAllCells(SudokuBoardState state)
        {
            if (state == null || !state.IsInitialized) return;
            EnsureTilesPopulated();

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    Tile3DView tile = _tiles[r, c];
                    if (tile == null) continue;

                    bool prefilled = state.IsCellPrefilled(r, c);
                    byte val = state.GetCellValue(r, c);
                    ushort notes = state.GetNotes(r, c);

                    tile.SetPrefilled(prefilled);
                    tile.SetValue(val);
                    tile.SetNotes(notes);

                    UpdateTileVisual(state, r, c);
                }
            }

            UpdateHighlights(state);
        }

        private void UpdateTileVisual(SudokuBoardState state, int r, int c)
        {
            Tile3DView tile = _tiles[r, c];
            if (tile == null) return;

            PlayerId peer = state.GetSelectedBy(r, c);
            bool isSelected = (_selectedRow == r && _selectedCol == c);
            bool isConflict = state.IsCellConflict(r, c);
            bool isPrefilled = state.IsCellPrefilled(r, c);

            if (isSelected)
            {
                tile.SetVisualState(CellVisualState.Selected);
            }
            else if (peer.IsValid && !peer.IsLocal)
            {
                tile.SetVisualState(CellVisualState.PeerSelected, peer);
            }
            else if (isConflict)
            {
                tile.SetVisualState(CellVisualState.Conflict);
            }
            else if (isPrefilled)
            {
                tile.SetVisualState(CellVisualState.Prefilled);
            }
            else
            {
                tile.SetVisualState(CellVisualState.Default);
            }
        }

        private void UpdateHighlights(SudokuBoardState state)
        {
            int selectedBlock = (_selectedRow >= 0 && _selectedCol >= 0)
                ? SudokuData.GetBlock(_selectedRow, _selectedCol)
                : -1;

            _selectedDigit = (_selectedRow >= 0 && _selectedCol >= 0 && state != null)
                ? state.GetCellValue(_selectedRow, _selectedCol)
                : (byte)0;

            for (int r = 0; r < SudokuData.BoardSize; r++)
            {
                for (int c = 0; c < SudokuData.BoardSize; c++)
                {
                    Tile3DView tile = _tiles[r, c];
                    if (tile == null) continue;

                    if (_selectedRow < 0 || _selectedCol < 0)
                    {
                        tile.SetRelatedHighlight(false);
                        tile.SetIdenticalNumberHighlight(false);
                    }
                    else
                    {
                        bool isSameRow = (r == _selectedRow);
                        bool isSameCol = (c == _selectedCol);
                        bool isSameBlock = (SudokuData.GetBlock(r, c) == selectedBlock);
                        tile.SetRelatedHighlight(isSameRow || isSameCol || isSameBlock);

                        // Highlight cells sharing the same non-zero digit
                        bool isIdentical = (_selectedDigit > 0 && tile.Value == _selectedDigit && !(r == _selectedRow && c == _selectedCol));
                        tile.SetIdenticalNumberHighlight(isIdentical);
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
            _selectedDigit = 0;
            UpdateHighlights(null);
        }

        private void HandleTileClicked(int row, int col)
        {
            SelectCell(row, col);
        }

        private void HandleStateCellModified(int row, int col, int value, PlayerId player)
        {
            Tile3DView tile = _tiles[row, col];
            if (tile != null)
            {
                tile.SetValue(value);
            }
        }

        private void HandleStateCellNotesModified(int row, int col, ushort notesMask)
        {
            Tile3DView tile = _tiles[row, col];
            if (tile != null)
            {
                tile.SetNotes(notesMask);
            }
        }

        private void HandleStateCellSelected(int row, int col, PlayerId player)
        {
            Tile3DView tile = _tiles[row, col];
            if (tile == null) return;

            if (player.IsLocal)
            {
                _selectedRow = row;
                _selectedCol = col;
                tile.SetVisualState(CellVisualState.Selected);
            }
            else
            {
                tile.SetVisualState(CellVisualState.PeerSelected, player);
            }
        }

        private void HandleStateCellDeselected(int row, int col, PlayerId player)
        {
            Tile3DView tile = _tiles[row, col];
            if (tile == null) return;

            if (player.IsLocal && _selectedRow == row && _selectedCol == col)
            {
                _selectedRow = -1;
                _selectedCol = -1;
            }

            tile.SetVisualState(tile.IsPrefilled ? CellVisualState.Prefilled : CellVisualState.Default);
        }

        private void HandleStateBoardCompleted(bool isCompleted)
        {
            if (isCompleted)
            {
                for (int r = 0; r < SudokuData.BoardSize; r++)
                {
                    for (int c = 0; c < SudokuData.BoardSize; c++)
                    {
                        _tiles[r, c]?.SetVisualState(CellVisualState.Correct);
                    }
                }
            }
        }

        private void HandleStateBoardReset()
        {
            ClearSelection();
        }

        private Tile3DView CreateProceduralTile(int row, int col)
        {
            GameObject cellObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cellObj.name = $"Tile_{row}_{col}";
            cellObj.transform.SetParent(transform, false);

            var rend = cellObj.GetComponent<MeshRenderer>();
            if (rend != null)
            {
                Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s != null)
                {
                    var mat = new Material(s);
                    mat.SetColor("_BaseColor", new Color(0.12f, 0.15f, 0.21f, 1f));
                    rend.sharedMaterial = mat;
                }
            }

            Tile3DView view = cellObj.AddComponent<Tile3DView>();

            // Main 3D Text on front face facing player
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(cellObj.transform, false);
            textObj.transform.localPosition = new Vector3(0f, 0f, -0.52f);
            textObj.transform.localRotation = Quaternion.identity;
            textObj.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);

            TextMeshPro tmp = textObj.AddComponent<TextMeshPro>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 38f;
            tmp.text = "";
            tmp.color = Color.white;

            view.SetMainDigitText(tmp);
            return view;
        }
    }
}
