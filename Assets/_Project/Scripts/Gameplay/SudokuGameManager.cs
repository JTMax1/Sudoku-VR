using System;
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
    /// Master coordinator for "Zenith Sudoku XR".
    /// Orchestrates 6-tier puzzle generation, board state, tactile view updates,
    /// 3-strikes rule, scoring engine, active timer, pencil notes, move history, and smart hints.
    /// </summary>
    public class SudokuGameManager : MonoBehaviour
    {
        public static SudokuGameManager Instance { get; private set; }

        [Header("Scene References")]
        [SerializeField] private SudokuBoardView boardView;
        [SerializeField] private FloatingKeypadView floatingKeypad;
        [SerializeField] private SpatialBoardAnchor boardAnchor;
        [SerializeField] private TimerSystem timerSystem;
        [SerializeField] private ScoringEngine scoringEngine;
        [SerializeField] private HintService hintService;
        [SerializeField] private LocalSudokuNetworkSync networkSync;

        [Header("Game Configuration")]
        [SerializeField] private Difficulty defaultDifficulty = Difficulty.Beginner;
        [SerializeField] private bool autoGenerateOnStart = true;
        [SerializeField] private int maxMistakesAllowed = 3;

        private SudokuGenerator _generator;
        private SudokuBoardState _boardState;
        private MoveHistory _moveHistory;

        private readonly byte[] _puzzleBuffer = new byte[SudokuData.CellCount];
        private readonly byte[] _solutionBuffer = new byte[SudokuData.CellCount];

        private int _mistakeCount = 0;
        private bool _isGameOver = false;

        public SudokuBoardState BoardState => _boardState;
        public SudokuBoardView BoardView => boardView;
        public FloatingKeypadView FloatingKeypad => floatingKeypad;
        public SpatialBoardAnchor BoardAnchor => boardAnchor;
        public TimerSystem Timer => timerSystem;
        public ScoringEngine ScoreEngine => scoringEngine;
        public MoveHistory History => _moveHistory;
        public Difficulty CurrentDifficulty { get; private set; }

        public int MistakeCount => _mistakeCount;
        public int MaxMistakes => maxMistakesAllowed;
        public bool IsGameOver => _isGameOver;

        public event Action<int, int> OnMistakesUpdated; // (current, max)
        public event Action<bool> OnGameFinished; // true for victory, false for defeat
        public event Action<HintResult> OnHintReceived;

        private void Awake()
        {
            if (Instance == null) Instance = this;

            _generator = new SudokuGenerator();
            _boardState = new SudokuBoardState();
            _moveHistory = new MoveHistory();

            if (boardView == null) boardView = FindAnyObjectByType<SudokuBoardView>();
            if (floatingKeypad == null) floatingKeypad = FindAnyObjectByType<FloatingKeypadView>();
            if (boardAnchor == null) boardAnchor = FindAnyObjectByType<SpatialBoardAnchor>();
            if (timerSystem == null)
            {
                timerSystem = GetComponent<TimerSystem>() ?? gameObject.AddComponent<TimerSystem>();
            }
            if (scoringEngine == null)
            {
                scoringEngine = GetComponent<ScoringEngine>() ?? gameObject.AddComponent<ScoringEngine>();
            }
            if (hintService == null)
            {
                hintService = GetComponent<HintService>() ?? gameObject.AddComponent<HintService>();
            }
            if (networkSync == null)
            {
                networkSync = GetComponent<LocalSudokuNetworkSync>() ?? gameObject.AddComponent<LocalSudokuNetworkSync>();
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

            if (boardAnchor != null)
            {
                boardAnchor.BuildBoundingFrame(boardView != null ? boardView.TotalBoardSpan : 0.6f);
            }

            if (floatingKeypad != null)
            {
                floatingKeypad.BuildProceduralKeypad();
                floatingKeypad.OnNumberSelected += HandleInputDigit;
                floatingKeypad.OnEraseRequested += HandleInputErase;
                floatingKeypad.OnUndoRequested += HandleInputUndo;
                floatingKeypad.OnPencilToggled += HandleInputPencilToggled;
                floatingKeypad.OnHintRequested += HandleInputHintRequested;
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
            _mistakeCount = 0;
            _isGameOver = false;
            _moveHistory.Clear();

            OnMistakesUpdated?.Invoke(_mistakeCount, maxMistakesAllowed);

            Stopwatch sw = Stopwatch.StartNew();
            bool success = _generator.Generate(difficulty, _puzzleBuffer, _solutionBuffer);
            sw.Stop();

            if (!success)
            {
                Debug.LogError("[SudokuVR] Puzzle generation failed!");
                return;
            }

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

            if (floatingKeypad != null)
            {
                floatingKeypad.UpdateRemainingCounts(_boardState);
                floatingKeypad.SetPencilMode(false);
            }

            if (timerSystem != null)
            {
                timerSystem.ResetTimer();
                timerSystem.StartTimer();
            }

            if (scoringEngine != null)
            {
                scoringEngine.ResetScore();
            }

            Debug.Log($"<color=#00E5FF><b>[Zenith Sudoku XR]</b></color> New Game Initialized! " +
                      $"Difficulty: <b>{difficulty}</b> | " +
                      $"Clues: <b>{initialClues}/81</b> | " +
                      $"Gen Time: <b>{sw.ElapsedMilliseconds}ms</b> | " +
                      $"Validation: <b>Guaranteed Unique Single Solution</b>");
        }

        public void RestartCurrentGame()
        {
            _mistakeCount = 0;
            _isGameOver = false;
            _moveHistory.Clear();
            OnMistakesUpdated?.Invoke(_mistakeCount, maxMistakesAllowed);

            _boardState.Initialize(_puzzleBuffer, _solutionBuffer);

            if (boardView != null) boardView.RefreshAllCells(_boardState);
            if (floatingKeypad != null) floatingKeypad.UpdateRemainingCounts(_boardState);
            if (timerSystem != null)
            {
                timerSystem.ResetTimer();
                timerSystem.StartTimer();
            }
            if (scoringEngine != null) scoringEngine.ResetScore();

            Debug.Log("[Zenith Sudoku XR] Board reset to initial clues.");
        }

        public void ReviveGame()
        {
            _mistakeCount = 2; // Gives 1 more chance
            _isGameOver = false;
            OnMistakesUpdated?.Invoke(_mistakeCount, maxMistakesAllowed);
            timerSystem?.ResumeTimer();
            Debug.Log("[Zenith Sudoku XR] Player revived with 1 strike remaining!");
        }

        private void HandleCellSelectedByLocalPlayer(int row, int col)
        {
            _boardState.SetSelection(row, col, PlayerId.LocalPlayer);
            networkSync?.SendCellSelected(row, col, PlayerId.LocalPlayer);
        }

        public void HandleInputDigit(int digit)
        {
            if (_isGameOver || boardView == null || !boardView.HasSelection) return;

            int r = boardView.SelectedRow;
            int c = boardView.SelectedCol;

            if (_boardState.IsCellPrefilled(r, c)) return;

            bool isPencilMode = floatingKeypad != null && floatingKeypad.IsPencilModeActive;

            if (isPencilMode)
            {
                // In pencil mode, toggle candidate sub-digit
                ushort prevNotes = _boardState.GetNotes(r, c);
                _boardState.ToggleNote(r, c, digit);
                ushort newNotes = _boardState.GetNotes(r, c);

                _moveHistory.RecordMove(r, c, 0, 0, prevNotes, newNotes);
            }
            else
            {
                byte prevVal = _boardState.GetCellValue(r, c);
                ushort prevNotes = _boardState.GetNotes(r, c);
                byte solutionVal = _boardState.GetSolutionValue(r, c);

                bool isCorrect = (digit == solutionVal);

                if (!isCorrect)
                {
                    _mistakeCount++;
                    scoringEngine?.RecordMistake();
                    OnMistakesUpdated?.Invoke(_mistakeCount, maxMistakesAllowed);

                    Debug.LogWarning($"[Zenith Sudoku XR] Mistake registered: ({r},{c}) = {digit}. Strike {_mistakeCount}/{maxMistakesAllowed}!");

                    if (_mistakeCount >= maxMistakesAllowed)
                    {
                        TriggerGameOver(false);
                        return;
                    }
                }
                else
                {
                    scoringEngine?.RecordCorrectPlacement(CurrentDifficulty);
                }

                bool modified = _boardState.SetCell(r, c, digit, PlayerId.LocalPlayer);
                if (modified)
                {
                    _moveHistory.RecordMove(r, c, prevVal, (byte)digit, prevNotes, 0);
                    networkSync?.SendCellModified(r, c, digit, PlayerId.LocalPlayer);
                    floatingKeypad?.UpdateRemainingCounts(_boardState);
                }
            }
        }

        public void HandleInputErase()
        {
            if (_isGameOver || boardView == null || !boardView.HasSelection) return;

            int r = boardView.SelectedRow;
            int c = boardView.SelectedCol;

            if (_boardState.IsCellPrefilled(r, c)) return;

            byte prevVal = _boardState.GetCellValue(r, c);
            ushort prevNotes = _boardState.GetNotes(r, c);

            if (prevVal > 0)
            {
                _boardState.SetCell(r, c, 0, PlayerId.LocalPlayer);
                _moveHistory.RecordMove(r, c, prevVal, 0, prevNotes, 0);
                networkSync?.SendCellModified(r, c, 0, PlayerId.LocalPlayer);
                floatingKeypad?.UpdateRemainingCounts(_boardState);
            }
            else if (prevNotes > 0)
            {
                _boardState.ClearNotes(r, c);
                _moveHistory.RecordMove(r, c, 0, 0, prevNotes, 0);
            }
        }

        public void HandleInputUndo()
        {
            if (_isGameOver) return;

            if (_moveHistory.TryUndo(out BoardMove move))
            {
                if (move.PreviousValue != move.NewValue)
                {
                    _boardState.SetCell(move.Row, move.Col, move.PreviousValue, PlayerId.LocalPlayer);
                    floatingKeypad?.UpdateRemainingCounts(_boardState);
                }

                if (move.PreviousNotesMask != move.NewNotesMask)
                {
                    _boardState.SetNotes(move.Row, move.Col, move.PreviousNotesMask);
                }

                boardView?.SelectCell(move.Row, move.Col);
            }
        }

        private void HandleInputPencilToggled(bool active)
        {
            // Pencil mode state updated in floatingKeypad
        }

        public void HandleInputHintRequested()
        {
            if (_isGameOver || hintService == null) return;

            HintResult hint = hintService.GetSmartHint(_boardState);
            if (hint.Found)
            {
                boardView?.SelectCell(hint.Row, hint.Col);
                OnHintReceived?.Invoke(hint);
                Debug.Log($"<color=#FFD700><b>[Smart Hint]</b></color> {hint.Technique}: {hint.Description}");
            }
        }

        private void HandleBoardCompleted(bool isCompleted)
        {
            if (isCompleted && !_isGameOver)
            {
                TriggerGameOver(true);
            }
        }

        private void TriggerGameOver(bool victory)
        {
            _isGameOver = true;
            timerSystem?.PauseTimer();

            float elapsed = timerSystem != null ? timerSystem.ElapsedSeconds : 0f;

            if (victory)
            {
                scoringEngine?.CalculateFinalBonus(elapsed, CurrentDifficulty, _mistakeCount);
                ProgressionManager.Instance?.RecordGameVictory(CurrentDifficulty, elapsed, _mistakeCount);

                Debug.Log("<color=#34C759><b>[Zenith Sudoku XR] VICTORY! Puzzle solved with perfection!</b></color>");
            }
            else
            {
                ProgressionManager.Instance?.RecordGameDefeat();
                Debug.LogWarning("<color=#FF3B30><b>[Zenith Sudoku XR] DEFEAT! 3 Strikes reached.</b></color>");
            }

            OnGameFinished?.Invoke(victory);
        }

        private void HandleEditorKeyboardInput()
        {
            if (Keyboard.current == null || boardView == null || !boardView.HasSelection) return;

            for (int d = 1; d <= 9; d++)
            {
                Key digitKey = Key.Digit1 + (d - 1);
                Key numpadKey = Key.Numpad1 + (d - 1);

                if (Keyboard.current[digitKey].wasPressedThisFrame || Keyboard.current[numpadKey].wasPressedThisFrame)
                {
                    HandleInputDigit(d);
                    return;
                }
            }

            if (Keyboard.current.backspaceKey.wasPressedThisFrame || Keyboard.current.deleteKey.wasPressedThisFrame)
            {
                HandleInputErase();
            }

            // Undo shortcut: Ctrl+Z or Cmd+Z
            if (Keyboard.current.zKey.wasPressedThisFrame &&
               (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.leftCommandKey.isPressed))
            {
                HandleInputUndo();
            }

            // Pencil shortcut: N or P
            if (Keyboard.current.pKey.wasPressedThisFrame || Keyboard.current.nKey.wasPressedThisFrame)
            {
                bool active = !floatingKeypad.IsPencilModeActive;
                floatingKeypad.SetPencilMode(active);
            }

            // Hint shortcut: H
            if (Keyboard.current.hKey.wasPressedThisFrame)
            {
                HandleInputHintRequested();
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
