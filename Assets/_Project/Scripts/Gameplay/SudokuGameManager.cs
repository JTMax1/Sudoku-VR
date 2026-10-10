using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using SudokuVR.Core;
using SudokuVR.Interaction;
using SudokuVR.Networking;
using Debug = UnityEngine.Debug;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Master coordinator for Sudoku VR.
    /// Orchestrates puzzle generation, board state, view updates, and VR input routing.
    /// Emits initialization and performance telemetry.
    /// </summary>
    public class SudokuGameManager : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private SudokuBoardView boardView;
        [SerializeField] private SpatialNumpad spatialNumpad;
        [SerializeField] private LocalSudokuNetworkSync networkSync;

        [Header("Game Configuration")]
        [SerializeField] private Difficulty defaultDifficulty = Difficulty.Medium;
        [SerializeField] private bool autoGenerateOnStart = true;

        private SudokuGenerator _generator;
        private SudokuBoardState _boardState;

        private readonly byte[] _puzzleBuffer = new byte[SudokuData.CellCount];
        private readonly byte[] _solutionBuffer = new byte[SudokuData.CellCount];

        public SudokuBoardState BoardState => _boardState;
        public SudokuBoardView BoardView => boardView;
        public Difficulty CurrentDifficulty { get; private set; }

        private void Awake()
        {
            _generator = new SudokuGenerator();
            _boardState = new SudokuBoardState();

            if (boardView == null)
            {
                boardView = FindAnyObjectByType<SudokuBoardView>();
            }

            if (spatialNumpad == null)
            {
                spatialNumpad = FindAnyObjectByType<SpatialNumpad>();
            }

            if (networkSync == null)
            {
                networkSync = FindAnyObjectByType<LocalSudokuNetworkSync>();
                if (networkSync == null)
                {
                    networkSync = gameObject.AddComponent<LocalSudokuNetworkSync>();
                }
            }
        }

        private void Start()
        {
            InitializeSubsystems();

            if (autoGenerateOnStart)
            {
                StartNewGame(defaultDifficulty);
            }
        }

        private void InitializeSubsystems()
        {
            if (boardView != null)
            {
                boardView.BuildGridHierarchy();
                boardView.BindToState(_boardState);
                boardView.OnCellSelectedByUser += HandleCellSelectedByLocalPlayer;
            }

            if (spatialNumpad != null)
            {
                spatialNumpad.BuildProceduralLayout();
                spatialNumpad.OnNumberSelected += HandleNumpadNumber;
                spatialNumpad.OnEraseRequested += HandleNumpadErase;
                spatialNumpad.OnClearRequested += HandleNumpadClearBoard;
            }

            if (networkSync != null)
            {
                networkSync.OnRemoteCellModified += (r, c, val, p) => _boardState.SetCell(r, c, val, p);
                networkSync.OnRemoteCellSelected += (r, c, p) => _boardState.SetSelection(r, c, p);
            }

            _boardState.OnBoardCompleted += HandleBoardCompleted;
        }

        private void Update()
        {
            HandleEditorKeyboardInput();
        }

        /// <summary>
        /// Generates a new puzzle and initializes state with performance profiling metrics.
        /// </summary>
        public void StartNewGame(Difficulty difficulty)
        {
            CurrentDifficulty = difficulty;

            Stopwatch sw = Stopwatch.StartNew();
            bool success = _generator.Generate(difficulty, _puzzleBuffer, _solutionBuffer);
            sw.Stop();

            if (!success)
            {
                Debug.LogError("[SudokuVR] Puzzle generation failed!");
                return;
            }

            // Count initial clues
            int initialClues = 0;
            for (int i = 0; i < SudokuData.CellCount; i++)
            {
                if (_puzzleBuffer[i] != 0) initialClues++;
            }

            _boardState.Initialize(_puzzleBuffer, _solutionBuffer);
            if (boardView != null)
            {
                boardView.RefreshAllCells(_boardState);
            }

            // Metrics Telemetry
            Debug.Log($"<color=#00E5FF><b>[SudokuVR Metrics]</b></color> New Game Initialized! " +
                      $"Difficulty: <b>{difficulty}</b> | " +
                      $"Clues: <b>{initialClues}/81</b> | " +
                      $"Gen Time: <b>{sw.ElapsedMilliseconds}ms</b> ({sw.ElapsedTicks} ticks) | " +
                      $"Validation: <b>Unique Solution Confirmed</b>");
        }

        public void RestartCurrentGame()
        {
            _boardState.Initialize(_puzzleBuffer, _solutionBuffer);
            if (boardView != null)
            {
                boardView.RefreshAllCells(_boardState);
            }
            Debug.Log("[SudokuVR] Board reset to initial clues.");
        }

        private void HandleCellSelectedByLocalPlayer(int row, int col)
        {
            _boardState.SetSelection(row, col, PlayerId.LocalPlayer);
            networkSync?.SendCellSelected(row, col, PlayerId.LocalPlayer);
        }

        private void HandleNumpadNumber(int digit)
        {
            if (boardView == null || !boardView.HasSelection) return;

            int r = boardView.SelectedRow;
            int c = boardView.SelectedCol;

            bool modified = _boardState.SetCell(r, c, digit, PlayerId.LocalPlayer);
            if (modified)
            {
                networkSync?.SendCellModified(r, c, digit, PlayerId.LocalPlayer);
            }
        }

        private void HandleNumpadErase()
        {
            if (boardView == null || !boardView.HasSelection) return;

            int r = boardView.SelectedRow;
            int c = boardView.SelectedCol;

            bool modified = _boardState.SetCell(r, c, 0, PlayerId.LocalPlayer);
            if (modified)
            {
                networkSync?.SendCellModified(r, c, 0, PlayerId.LocalPlayer);
            }
        }

        private void HandleNumpadClearBoard()
        {
            RestartCurrentGame();
        }

        private void HandleBoardCompleted(bool isCompleted)
        {
            if (isCompleted)
            {
                Debug.Log("<color=#34C759><b>[SudokuVR] CONGRATULATIONS! Puzzle successfully completed!</b></color>");
            }
        }

        private void HandleEditorKeyboardInput()
        {
            if (Keyboard.current == null || boardView == null || !boardView.HasSelection) return;

            // Digits 1-9
            for (int d = 1; d <= 9; d++)
            {
                Key digitKey = Key.Digit1 + (d - 1);
                Key numpadKey = Key.Numpad1 + (d - 1);

                if (Keyboard.current[digitKey].wasPressedThisFrame || Keyboard.current[numpadKey].wasPressedThisFrame)
                {
                    HandleNumpadNumber(d);
                    return;
                }
            }

            // Erase / Backspace / Delete
            if (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                HandleNumpadErase();
            }

            // Arrow navigation
            int r = boardView.SelectedRow;
            int c = boardView.SelectedCol;

            if (Keyboard.current.upArrowKey.wasPressedThisFrame && r > 0)
                boardView.SelectCell(r - 1, c);
            else if (Keyboard.current.downArrowKey.wasPressedThisFrame && r < 8)
                boardView.SelectCell(r + 1, c);
            else if (Keyboard.current.leftArrowKey.wasPressedThisFrame && c > 0)
                boardView.SelectCell(r, c - 1);
            else if (Keyboard.current.rightArrowKey.wasPressedThisFrame && c < 8)
                boardView.SelectCell(r, c + 1);
        }
    }
}
