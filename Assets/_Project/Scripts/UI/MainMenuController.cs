using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Core;
using SudokuVR.Gameplay;
using SudokuVR.Interaction;

namespace SudokuVR.UI
{
    public enum NavigationScreen
    {
        HubHome,
        DifficultySelect,
        BattleLobby,
        PassportGallery,
        InGamePuzzle
    }

    /// <summary>
    /// Master spatial menu controller.
    /// Manages top stats ribbon, hero cards, bottom dock navigation, difficulty modal gating,
    /// and seamless transitions into the 3D floating puzzle space.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Root Spatial Panels")]
        [SerializeField] private GameObject hubHomePanel;
        [SerializeField] private GameObject difficultyModalPanel;
        [SerializeField] private GameObject battleLobbyPanel;
        [SerializeField] private GameObject passportGalleryPanel;
        [SerializeField] private GameObject inGameRoot;

        [Header("Top Stats Ribbon")]
        [SerializeField] private TMP_Text coinsText;
        [SerializeField] private TMP_Text streakText;
        [SerializeField] private TMP_Text gamesWonText;

        [Header("Hero Triggers")]
        [SerializeField] private Button playHeroButton;
        [SerializeField] private Button continueHeroButton;
        [SerializeField] private Button dailyChallengeButton;
        [SerializeField] private Button battleHeroButton;

        [Header("Bottom Dock Navigation")]
        [SerializeField] private Button navHomeButton;
        [SerializeField] private Button navBattleButton;
        [SerializeField] private Button navPassportButton;
        [SerializeField] private Button navPassthroughToggle;

        [Header("Difficulty Modal Buttons (6 Tiers)")]
        [SerializeField] private Button[] difficultyButtons = new Button[6];
        [SerializeField] private TMP_Text[] difficultyCriteriaTexts = new TMP_Text[6];
        [SerializeField] private GameObject[] padlockIcons = new GameObject[6];
        [SerializeField] private Button closeDifficultyModalButton;

        private NavigationScreen _currentScreen = NavigationScreen.HubHome;
        public NavigationScreen CurrentScreen => _currentScreen;

        private void Start()
        {
            RegisterButtonCallbacks();
            UpdateStatsRibbon();

            var titleScreen = FindAnyObjectByType<TitleScreen3DController>();
            if (titleScreen != null && titleScreen.gameObject.activeInHierarchy)
            {
                // Defer showing hub until user clicks ENTER ZENITH on TitleScreen_3D
                gameObject.SetActive(false);
                return;
            }

            ShowHubView();
        }

        private void RegisterButtonCallbacks()
        {
            if (playHeroButton != null) playHeroButton.onClick.AddListener(ShowDifficultyModal);
            if (continueHeroButton != null) continueHeroButton.onClick.AddListener(ContinueActiveGame);
            if (dailyChallengeButton != null) dailyChallengeButton.onClick.AddListener(() => StartGameWithDifficulty(Difficulty.Medium));
            if (battleHeroButton != null) battleHeroButton.onClick.AddListener(ShowBattleLobby);

            if (navHomeButton != null) navHomeButton.onClick.AddListener(ShowHubView);
            if (navBattleButton != null) navBattleButton.onClick.AddListener(ShowBattleLobby);
            if (navPassportButton != null) navPassportButton.onClick.AddListener(ShowPassportGallery);
            if (navPassthroughToggle != null) navPassthroughToggle.onClick.AddListener(TogglePassthrough);

            if (closeDifficultyModalButton != null) closeDifficultyModalButton.onClick.AddListener(ShowHubView);

            for (int i = 0; i < difficultyButtons.Length; i++)
            {
                if (difficultyButtons[i] != null)
                {
                    Difficulty diff = (Difficulty)i;
                    difficultyButtons[i].onClick.AddListener(() => OnDifficultySelected(diff));
                }
            }
        }

        public void UpdateStatsRibbon()
        {
            ProgressionManager pm = ProgressionManager.Instance;
            if (pm != null && pm.Profile != null)
            {
                if (coinsText != null) coinsText.text = $"{pm.Profile.brainCoins} COINS";
                if (streakText != null) streakText.text = $"{pm.Profile.currentWinStreak} STREAK";
                if (gamesWonText != null) gamesWonText.text = $"{pm.Profile.totalGamesWon} WINS";
            }
        }

        public void ShowHubView()
        {
            gameObject.SetActive(true);
            _currentScreen = NavigationScreen.HubHome;
            SetActivePanel(hubHomePanel);
            if (inGameRoot != null) inGameRoot.SetActive(false);
            UpdateStatsRibbon();

            SudokuGameManager gm = SudokuGameManager.Instance ?? FindAnyObjectByType<SudokuGameManager>();
            bool hasActive = gm != null && gm.HasActiveGame;
            if (continueHeroButton != null)
            {
                continueHeroButton.gameObject.SetActive(hasActive);
            }

            var diorama = GetComponentInChildren<CampaignDioramaController>();
            diorama?.RefreshAllNodes();
        }

        public void ContinueActiveGame()
        {
            _currentScreen = NavigationScreen.InGamePuzzle;
            SetActivePanel(null); // Hide hub panels
            gameObject.SetActive(false); // Hide hub console completely during gameplay

            if (inGameRoot != null)
            {
                inGameRoot.SetActive(true);
            }

            SudokuGameManager gm = SudokuGameManager.Instance ?? FindAnyObjectByType<SudokuGameManager>();
            if (gm != null)
            {
                gm.ResumeGame();
                SpatialHUDView hud = FindAnyObjectByType<SpatialHUDView>();
                hud?.UpdateDifficultyBadge(gm.CurrentDifficulty);
            }

            SpatialBoardAnchor anchor = FindAnyObjectByType<SpatialBoardAnchor>();
            anchor?.RecenterToPlayerGaze();
        }

        public void ShowDifficultyModal()
        {
            _currentScreen = NavigationScreen.DifficultySelect;
            SetActivePanel(difficultyModalPanel);
            RefreshDifficultyCards();
        }

        public void ShowBattleLobby()
        {
            _currentScreen = NavigationScreen.BattleLobby;
            SetActivePanel(battleLobbyPanel);
            if (inGameRoot != null) inGameRoot.SetActive(false);
        }

        public void ShowPassportGallery()
        {
            _currentScreen = NavigationScreen.PassportGallery;
            SetActivePanel(passportGalleryPanel);
            if (inGameRoot != null) inGameRoot.SetActive(false);

            PassportRoomView gallery = passportGalleryPanel != null ? passportGalleryPanel.GetComponentInChildren<PassportRoomView>() : null;
            gallery?.RefreshGallery();
        }

        public void StartGameWithDifficulty(Difficulty difficulty)
        {
            ProgressionManager pm = ProgressionManager.Instance;
            if (pm != null && !pm.IsTierUnlocked(difficulty))
            {
                Debug.LogWarning($"[Zenith Sudoku XR] Difficulty {difficulty} is locked!");
                return;
            }

            _currentScreen = NavigationScreen.InGamePuzzle;
            SetActivePanel(null); // Hide menu panels
            gameObject.SetActive(false); // Hide hub console completely during gameplay

            if (inGameRoot != null)
            {
                inGameRoot.SetActive(true);
            }

            SudokuGameManager gm = SudokuGameManager.Instance ?? FindAnyObjectByType<SudokuGameManager>();
            if (gm != null)
            {
                gm.StartNewGame(difficulty);
            }

            SpatialHUDView hud = FindAnyObjectByType<SpatialHUDView>();
            hud?.UpdateDifficultyBadge(difficulty);

            // Automatically recenter board for crisp ergonomics
            SpatialBoardAnchor anchor = FindAnyObjectByType<SpatialBoardAnchor>();
            anchor?.RecenterToPlayerGaze();
        }

        private void OnDifficultySelected(Difficulty diff)
        {
            StartGameWithDifficulty(diff);
        }

        private void RefreshDifficultyCards()
        {
            ProgressionManager pm = ProgressionManager.Instance;
            if (pm == null) return;

            for (int i = 0; i < 6; i++)
            {
                Difficulty diff = (Difficulty)i;
                bool unlocked = pm.IsTierUnlocked(diff);

                if (difficultyButtons[i] != null)
                {
                    difficultyButtons[i].interactable = unlocked;
                }

                if (padlockIcons[i] != null)
                {
                    padlockIcons[i].SetActive(!unlocked);
                }

                if (difficultyCriteriaTexts[i] != null)
                {
                    DifficultyTierProgress tier = pm.GetTierProgress(diff);
                    difficultyCriteriaTexts[i].text = unlocked
                        ? $"Stars: {tier.starsEarned}/{tier.totalStars} | Best: {(tier.bestTimeSeconds > 0 ? TimerSystem.FormatTime(tier.bestTimeSeconds) : "--:--")}"
                        : pm.GetUnlockCriteriaDescription(diff);
                }
            }
        }

        private void TogglePassthrough()
        {
            SpatialBoardAnchor anchor = FindAnyObjectByType<SpatialBoardAnchor>();
            anchor?.TogglePassthrough();
        }

        private void SetActivePanel(GameObject target)
        {
            if (hubHomePanel != null) hubHomePanel.SetActive(hubHomePanel == target);
            if (difficultyModalPanel != null) difficultyModalPanel.SetActive(difficultyModalPanel == target);
            if (battleLobbyPanel != null) battleLobbyPanel.SetActive(battleLobbyPanel == target);
            if (passportGalleryPanel != null) passportGalleryPanel.SetActive(passportGalleryPanel == target);
        }
    }
}
