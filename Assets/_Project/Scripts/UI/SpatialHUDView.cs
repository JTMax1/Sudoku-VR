using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Core;
using SudokuVR.Gameplay;
using SudokuVR.Interaction;

namespace SudokuVR.UI
{
    /// <summary>
    /// Floating curved in-game spatial HUD arc, pause dialog, and game over modal.
    /// Displays score counter, active timer, mistake counter (X/3), difficulty, and smart hint messages.
    /// Provides one-tap Pause, Mixed Reality Passthrough toggle, and Instant Board Recentering.
    /// </summary>
    public class SpatialHUDView : MonoBehaviour
    {
        [Header("HUD Metrics Texts")]
        [SerializeField] private TMP_Text difficultyText;
        [SerializeField] private TMP_Text timerText;
        [SerializeField] private TMP_Text scoreText;
        [SerializeField] private TMP_Text mistakesText;
        [SerializeField] private TMP_Text hintNotificationText;

        [Header("HUD Action Buttons")]
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button passthroughButton;
        [SerializeField] private Button recenterButton;

        [Header("Pause Modal Panel")]
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button pauseHomeButton;

        [Header("Game Over / Victory Dialog Panel")]
        [SerializeField] private GameObject endScreenPanel;
        [SerializeField] private TMP_Text endTitleText;
        [SerializeField] private TMP_Text endSubText;
        [SerializeField] private Button playAgainButton;
        [SerializeField] private Button reviveButton;
        [SerializeField] private Button returnHomeButton;

        private SudokuGameManager _gameManager;
        private SpatialBoardAnchor _boardAnchor;

        private void Start()
        {
            _gameManager = SudokuGameManager.Instance ?? FindAnyObjectByType<SudokuGameManager>();
            _boardAnchor = FindAnyObjectByType<SpatialBoardAnchor>();

            if (_gameManager != null)
            {
                _gameManager.OnMistakesUpdated += HandleMistakesUpdated;
                _gameManager.OnGameFinished += HandleGameFinished;
                _gameManager.OnHintReceived += HandleHintReceived;
                _gameManager.OnPauseStateChanged += HandlePauseStateChanged;

                if (_gameManager.ScoreEngine != null)
                {
                    _gameManager.ScoreEngine.OnScoreChanged += HandleScoreChanged;
                }

                if (_gameManager.Timer != null)
                {
                    _gameManager.Timer.OnTimerTick += HandleTimerTick;
                }

                UpdateDifficultyBadge(_gameManager.CurrentDifficulty);
                UpdateMistakesDisplay(_gameManager.MistakeCount, _gameManager.MaxMistakes);
            }

            // HUD Action Buttons
            if (pauseButton != null) pauseButton.onClick.AddListener(OnPauseClicked);
            if (passthroughButton != null) passthroughButton.onClick.AddListener(OnPassthroughClicked);
            if (recenterButton != null) recenterButton.onClick.AddListener(OnRecenterClicked);

            // Pause Modal Buttons
            if (resumeButton != null) resumeButton.onClick.AddListener(OnResumeClicked);
            if (restartButton != null) restartButton.onClick.AddListener(OnRestartClicked);
            if (pauseHomeButton != null) pauseHomeButton.onClick.AddListener(OnReturnHomeClicked);

            // End Screen Buttons
            if (playAgainButton != null) playAgainButton.onClick.AddListener(OnPlayAgainClicked);
            if (reviveButton != null) reviveButton.onClick.AddListener(OnReviveClicked);
            if (returnHomeButton != null) returnHomeButton.onClick.AddListener(OnReturnHomeClicked);

            if (pausePanel != null) pausePanel.SetActive(false);
            if (endScreenPanel != null) endScreenPanel.SetActive(false);

            if (hintNotificationText != null)
            {
                hintNotificationText.text = "";
            }
        }

        private void OnDestroy()
        {
            if (_gameManager != null)
            {
                _gameManager.OnMistakesUpdated -= HandleMistakesUpdated;
                _gameManager.OnGameFinished -= HandleGameFinished;
                _gameManager.OnHintReceived -= HandleHintReceived;
                _gameManager.OnPauseStateChanged -= HandlePauseStateChanged;

                if (_gameManager.ScoreEngine != null)
                {
                    _gameManager.ScoreEngine.OnScoreChanged -= HandleScoreChanged;
                }

                if (_gameManager.Timer != null)
                {
                    _gameManager.Timer.OnTimerTick -= HandleTimerTick;
                }
            }
        }

        public void UpdateDifficultyBadge(Difficulty diff)
        {
            if (difficultyText != null)
            {
                difficultyText.text = diff.ToString().ToUpper();
            }
        }

        private void HandleTimerTick(float elapsedSeconds)
        {
            if (timerText != null)
            {
                timerText.text = TimerSystem.FormatTime(elapsedSeconds);
            }
        }

        private void HandleScoreChanged(int currentScore, int pointsDelta)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {currentScore}";
            }
        }

        private void HandleMistakesUpdated(int current, int max)
        {
            UpdateMistakesDisplay(current, max);
        }

        private void UpdateMistakesDisplay(int current, int max)
        {
            if (mistakesText != null)
            {
                mistakesText.text = $"MISTAKES: {current}/{max}";
                mistakesText.color = (current >= 2) ? new Color(1f, 0.3f, 0.3f) : Color.white;
            }
        }

        private void HandleHintReceived(HintResult hint)
        {
            if (hintNotificationText != null)
            {
                hintNotificationText.text = $"<color=#FFD700><b>HINT:</b></color> {hint.Description}";
                CancelInvoke(nameof(ClearHintText));
                Invoke(nameof(ClearHintText), 5f);
            }
        }

        private void ClearHintText()
        {
            if (hintNotificationText != null)
            {
                hintNotificationText.text = "";
            }
        }

        private void HandlePauseStateChanged(bool isPaused)
        {
            if (pausePanel != null)
            {
                pausePanel.SetActive(isPaused);
            }
        }

        private void OnPauseClicked()
        {
            _gameManager?.TogglePause();
        }

        private void OnResumeClicked()
        {
            _gameManager?.ResumeGame();
        }

        private void OnRestartClicked()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            _gameManager?.RestartCurrentGame();
        }

        private void OnPassthroughClicked()
        {
            if (_boardAnchor == null) _boardAnchor = FindAnyObjectByType<SpatialBoardAnchor>();
            _boardAnchor?.TogglePassthrough();
        }

        private void OnRecenterClicked()
        {
            if (_boardAnchor == null) _boardAnchor = FindAnyObjectByType<SpatialBoardAnchor>();
            _boardAnchor?.RecenterToPlayerGaze();
        }

        private void HandleGameFinished(bool victory)
        {
            if (endScreenPanel != null)
            {
                endScreenPanel.SetActive(true);
            }

            if (victory)
            {
                if (endTitleText != null)
                {
                    endTitleText.text = "<color=#34C759>PUZZLE COMPLETED!</color>";
                }
                if (endSubText != null)
                {
                    int finalScore = _gameManager != null && _gameManager.ScoreEngine != null ? _gameManager.ScoreEngine.CurrentScore : 0;
                    string time = _gameManager != null && _gameManager.Timer != null ? TimerSystem.FormatTime(_gameManager.Timer.ElapsedSeconds) : "00:00";
                    endSubText.text = $"Score: <b>{finalScore}</b> | Time: <b>{time}</b>";
                }
                if (reviveButton != null) reviveButton.gameObject.SetActive(false);
                if (playAgainButton != null) playAgainButton.gameObject.SetActive(true);
            }
            else
            {
                if (endTitleText != null)
                {
                    endTitleText.text = "<color=#FF3B30>3 MISTAKES - GAME OVER</color>";
                }
                if (endSubText != null)
                {
                    endSubText.text = "You reached the 3-mistake limit. Try again or use a brain revive token!";
                }
                if (reviveButton != null) reviveButton.gameObject.SetActive(true);
                if (playAgainButton != null) playAgainButton.gameObject.SetActive(true);
            }
        }

        private void OnPlayAgainClicked()
        {
            if (endScreenPanel != null) endScreenPanel.SetActive(false);
            _gameManager?.StartNewGame(_gameManager.CurrentDifficulty);
            UpdateDifficultyBadge(_gameManager.CurrentDifficulty);
        }

        private void OnReviveClicked()
        {
            if (endScreenPanel != null) endScreenPanel.SetActive(false);
            _gameManager?.ReviveGame();
        }

        private void OnReturnHomeClicked()
        {
            if (pausePanel != null) pausePanel.SetActive(false);
            if (endScreenPanel != null) endScreenPanel.SetActive(false);

            _gameManager?.PauseGame();

            MainMenuController home = FindAnyObjectByType<MainMenuController>();
            if (home != null)
            {
                home.ShowHubView();
            }
        }
    }
}
