using System;
using System.IO;
using UnityEngine;

namespace SudokuVR.Core
{
    [Serializable]
    public class DifficultyTierProgress
    {
        public Difficulty difficulty;
        public int completedCount;
        public int requiredToUnlockNext;
        public int starsEarned;
        public int totalStars;
        public float bestTimeSeconds;
        public bool isUnlocked;

        public DifficultyTierProgress(Difficulty diff, int requiredQuota, int totalAvailableStars, bool unlockedByDefault = false)
        {
            difficulty = diff;
            completedCount = 0;
            requiredToUnlockNext = requiredQuota;
            starsEarned = 0;
            totalStars = totalAvailableStars;
            bestTimeSeconds = 0f;
            isUnlocked = unlockedByDefault;
        }
    }

    [Serializable]
    public class PlayerProfileData
    {
        public int totalGamesWon = 0;
        public int perfectWins = 0; // Won with 0 mistakes
        public int currentWinStreak = 0;
        public int bestWinStreak = 0;
        public int brainCoins = 150; // Starting currency
        public int hintsAvailable = 3;
        public DifficultyTierProgress[] tierProgress = new DifficultyTierProgress[6];

        public PlayerProfileData()
        {
            tierProgress[0] = new DifficultyTierProgress(Difficulty.Beginner, 10, 10, true);
            tierProgress[1] = new DifficultyTierProgress(Difficulty.Easy, 10, 15, false);
            tierProgress[2] = new DifficultyTierProgress(Difficulty.Medium, 10, 20, false);
            tierProgress[3] = new DifficultyTierProgress(Difficulty.Hard, 10, 25, false);
            tierProgress[4] = new DifficultyTierProgress(Difficulty.Expert, 10, 30, false);
            tierProgress[5] = new DifficultyTierProgress(Difficulty.Extreme, 10, 35, false);
        }
    }

    /// <summary>
    /// Manages campaign progression, difficulty tier unlock gating, player stats,
    /// and JSON persistence in Application.persistentDataPath.
    /// </summary>
    public class ProgressionManager : MonoBehaviour
    {
        public static ProgressionManager Instance { get; private set; }

        private PlayerProfileData _profile;
        private string _saveFilePath;

        public PlayerProfileData Profile => _profile;
        public event Action OnProgressionUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializePersistence();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializePersistence()
        {
            _saveFilePath = Path.Combine(Application.persistentDataPath, "zenith_sudoku_profile.json");
            LoadProfile();
        }

        public void LoadProfile()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    string json = File.ReadAllText(_saveFilePath);
                    _profile = JsonUtility.FromJson<PlayerProfileData>(json);
                    if (_profile == null || _profile.tierProgress == null || _profile.tierProgress.Length != 6)
                    {
                        _profile = new PlayerProfileData();
                    }
                }
                else
                {
                    _profile = new PlayerProfileData();
                    SaveProfile();
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ProgressionManager] Failed to load profile: {ex.Message}. Initializing fresh profile.");
                _profile = new PlayerProfileData();
            }

            OnProgressionUpdated?.Invoke();
        }

        public void SaveProfile()
        {
            try
            {
                string json = JsonUtility.ToJson(_profile, true);
                File.WriteAllText(_saveFilePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ProgressionManager] Failed to save profile: {ex.Message}");
            }
        }

        public bool IsTierUnlocked(Difficulty difficulty)
        {
            int index = (int)difficulty;
            if (index < 0 || index >= _profile.tierProgress.Length) return false;
            return _profile.tierProgress[index].isUnlocked;
        }

        public DifficultyTierProgress GetTierProgress(Difficulty difficulty)
        {
            int index = (int)difficulty;
            return _profile.tierProgress[index];
        }

        public string GetUnlockCriteriaDescription(Difficulty difficulty)
        {
            int index = (int)difficulty;
            if (index == 0) return "Available";
            Difficulty prev = (Difficulty)(index - 1);
            DifficultyTierProgress prevProgress = _profile.tierProgress[index - 1];
            return $"Complete {prevProgress.requiredToUnlockNext} {prev} puzzles ({prevProgress.completedCount}/{prevProgress.requiredToUnlockNext})";
        }

        /// <summary>
        /// Records victory, updates unlocked tiers, star counts, coins, and best times.
        /// </summary>
        public void RecordGameVictory(Difficulty difficulty, float timeSeconds, int mistakes)
        {
            int tierIdx = (int)difficulty;
            DifficultyTierProgress tier = _profile.tierProgress[tierIdx];

            tier.completedCount++;
            if (tier.starsEarned < tier.totalStars)
            {
                tier.starsEarned++;
            }

            if (tier.bestTimeSeconds <= 0f || timeSeconds < tier.bestTimeSeconds)
            {
                tier.bestTimeSeconds = timeSeconds;
            }

            _profile.totalGamesWon++;
            if (mistakes == 0)
            {
                _profile.perfectWins++;
            }

            _profile.currentWinStreak++;
            if (_profile.currentWinStreak > _profile.bestWinStreak)
            {
                _profile.bestWinStreak = _profile.currentWinStreak;
            }

            // Award brain coins: base 50 + difficulty bonus + perfect bonus
            int earnedCoins = 50 + (tierIdx * 25) + (mistakes == 0 ? 50 : 0);
            _profile.brainCoins += earnedCoins;

            // Check if next tier should unlock
            if (tierIdx + 1 < _profile.tierProgress.Length)
            {
                if (tier.completedCount >= tier.requiredToUnlockNext)
                {
                    _profile.tierProgress[tierIdx + 1].isUnlocked = true;
                }
            }

            SaveProfile();
            OnProgressionUpdated?.Invoke();
        }

        public void RecordGameDefeat()
        {
            _profile.currentWinStreak = 0;
            SaveProfile();
            OnProgressionUpdated?.Invoke();
        }
    }
}
