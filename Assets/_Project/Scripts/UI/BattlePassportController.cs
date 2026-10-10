using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Core;
using SudokuVR.Networking;

namespace SudokuVR.UI
{
    /// <summary>
    /// Controls 1v1 Battle Mode matchmaking, season ranking stats, and match initiation.
    /// </summary>
    public class BattlePassportController : MonoBehaviour
    {
        [Header("Header & Season")]
        [SerializeField] private TMP_Text seasonTitleText;
        [SerializeField] private TMP_Text winRateStatsText;
        [SerializeField] private TMP_Text coinsText;

        [Header("Matchmaking UI")]
        [SerializeField] private Button findMatchButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private TMP_Text matchStatusText;
        [SerializeField] private GameObject matchFoundBanner;
        [SerializeField] private TMP_Text opponentInfoText;

        [Header("Navigation")]
        [SerializeField] private Button backToHomeButton;

        private MultiplayerLobbyBridge _lobby;

        private void Start()
        {
            _lobby = MultiplayerLobbyBridge.Instance ?? FindAnyObjectByType<MultiplayerLobbyBridge>();

            if (findMatchButton != null) findMatchButton.onClick.AddListener(OnFindMatchClicked);
            if (cancelButton != null) cancelButton.onClick.AddListener(OnCancelClicked);
            if (backToHomeButton != null) backToHomeButton.onClick.AddListener(OnBackClicked);

            if (_lobby != null)
            {
                _lobby.OnStatusChanged += HandleStatusChanged;
                _lobby.OnMatchFound += HandleMatchFound;
            }

            UpdateStatsDisplay();
            if (matchFoundBanner != null) matchFoundBanner.SetActive(false);
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_lobby != null)
            {
                _lobby.OnStatusChanged -= HandleStatusChanged;
                _lobby.OnMatchFound -= HandleMatchFound;
            }
        }

        private void UpdateStatsDisplay()
        {
            if (seasonTitleText != null)
            {
                seasonTitleText.text = _lobby != null ? _lobby.SeasonName : "Season 1: Horizon Zenith";
            }

            ProgressionManager pm = ProgressionManager.Instance;
            if (pm != null && pm.Profile != null)
            {
                if (winRateStatsText != null)
                {
                    winRateStatsText.text = $"WINS: {pm.Profile.totalGamesWon} | STREAK: {pm.Profile.currentWinStreak} | RATING: 1250";
                }
                if (coinsText != null)
                {
                    coinsText.text = $"BRAIN COINS: {pm.Profile.brainCoins}";
                }
            }
        }

        private void OnFindMatchClicked()
        {
            if (_lobby != null)
            {
                _lobby.StartMatchmaking();
            }
            if (findMatchButton != null) findMatchButton.gameObject.SetActive(false);
            if (cancelButton != null) cancelButton.gameObject.SetActive(true);
            if (matchStatusText != null) matchStatusText.text = "Searching for rival in Meta Quest network...";
        }

        private void OnCancelClicked()
        {
            if (_lobby != null)
            {
                _lobby.CancelMatchmaking();
            }
            if (findMatchButton != null) findMatchButton.gameObject.SetActive(true);
            if (cancelButton != null) cancelButton.gameObject.SetActive(false);
            if (matchStatusText != null) matchStatusText.text = "Ready to battle.";
            if (matchFoundBanner != null) matchFoundBanner.SetActive(false);
        }

        private void HandleStatusChanged(MatchmakingStatus status)
        {
            if (matchStatusText != null)
            {
                matchStatusText.text = $"Matchmaking: {status}";
            }
        }

        private void HandleMatchFound(string name, string country)
        {
            if (matchFoundBanner != null) matchFoundBanner.SetActive(true);
            if (opponentInfoText != null)
            {
                opponentInfoText.text = $"OPPONENT FOUND!\n<b>{name}</b> from <b>{country}</b>\nDuel starting in 3... 2... 1...";
            }

            // Award country stamp upon matching rival
            CountryPassportTracker tracker = CountryPassportTracker.Instance;
            tracker?.UnlockCountryStamp(country);

            CancelInvoke(nameof(StartDuelPuzzle));
            Invoke(nameof(StartDuelPuzzle), 2.5f);
        }

        private void StartDuelPuzzle()
        {
            MainMenuController home = FindAnyObjectByType<MainMenuController>();
            if (home != null)
            {
                home.StartGameWithDifficulty(Difficulty.Medium);
            }
        }

        private void OnBackClicked()
        {
            MainMenuController home = FindAnyObjectByType<MainMenuController>();
            if (home != null) home.ShowHubView();
        }
    }
}
