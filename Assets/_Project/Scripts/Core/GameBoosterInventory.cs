using System;
using UnityEngine;

namespace SudokuVR.Core
{
    /// <summary>
    /// Authoritative player booster and power-up inventory system.
    /// Deeply integrates the Treasure Chest, Daily Vending Machine, Heart Tokens,
    /// and Action Dock into the moment-to-moment Sudoku gameplay loop.
    /// </summary>
    public class GameBoosterInventory : MonoBehaviour
    {
        public static GameBoosterInventory Instance { get; private set; }

        [Header("Active Booster Inventory")]
        [SerializeField] private int heartShields = 1;      // Absorbs a mistake before losing a heart token
        [SerializeField] private int magicWandHints = 3;    // Commands Orbi to point out logical deductions
        [SerializeField] private int timeFreezeCount = 1;   // Freezes session timer for 45s to secure 3-star rating
        [SerializeField] private int treasureKeys = 1;      // Keys earned from 3-star level clears to unlock chests

        public int HeartShields => heartShields;
        public int MagicWandHints => magicWandHints;
        public int TimeFreezeCount => timeFreezeCount;
        public int TreasureKeys => treasureKeys;

        public event Action OnInventoryChanged;
        public event Action OnHeartShieldAbsorbed;
        public event Action<float> OnTimerFrozen; // Duration in seconds

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }

            LoadInventoryFromProfile();
        }

        public void LoadInventoryFromProfile()
        {
            var profile = ProgressionManager.Instance?.Profile;
            if (profile != null)
            {
                magicWandHints = Mathf.Max(magicWandHints, profile.hintsAvailable);
            }
            OnInventoryChanged?.Invoke();
        }

        public void SaveInventoryToProfile()
        {
            var profile = ProgressionManager.Instance?.Profile;
            if (profile != null)
            {
                profile.hintsAvailable = magicWandHints;
                ProgressionManager.Instance.SaveProfile();
            }
            OnInventoryChanged?.Invoke();
        }

        #region Booster Usage API

        /// <summary>
        /// Consumes a Heart Shield to prevent losing a heart upon an invalid placement.
        /// Returns true if a shield was active and consumed.
        /// </summary>
        public bool TryConsumeHeartShield()
        {
            if (heartShields > 0)
            {
                heartShields--;
                SaveInventoryToProfile();
                OnHeartShieldAbsorbed?.Invoke();
                TactileAudioService.Instance?.PlayHint();
                Debug.Log("<color=#00FFFF><b>[Booster Active] Heart Shield absorbed the mistake! No heart was lost.</b></color>");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Consumes a Magic Wand charge to trigger Orbi's logical deduction assistance.
        /// </summary>
        public bool TryConsumeMagicWand()
        {
            if (magicWandHints > 0)
            {
                magicWandHints--;
                SaveInventoryToProfile();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Freezes the timer for 45 seconds to aid in earning 3 stars on tough puzzles.
        /// </summary>
        public bool TryConsumeTimeFreeze(float durationSeconds = 45f)
        {
            if (timeFreezeCount > 0)
            {
                timeFreezeCount--;
                SaveInventoryToProfile();
                OnTimerFrozen?.Invoke(durationSeconds);
                TactileAudioService.Instance?.PlayHint();
                Debug.Log($"<color=#FFD700><b>[Booster Active] Zen Hourglass frozen timer for {durationSeconds} seconds!</b></color>");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Grants rewards from Treasure Chests or Daily Vending Dispenser.
        /// </summary>
        public void GrantRewards(int shields, int hints, int timeFreezes, int coins)
        {
            heartShields += shields;
            magicWandHints += hints;
            timeFreezeCount += timeFreezes;

            var profile = ProgressionManager.Instance?.Profile;
            if (profile != null)
            {
                profile.brainCoins += coins;
            }

            SaveInventoryToProfile();
            Debug.Log($"<color=#34C759><b>[Inventory Rewarded] +{shields} Shields, +{hints} Wands, +{timeFreezes} Freezes, +{coins} Coins!</b></color>");
        }

        public void AddTreasureKey(int count = 1)
        {
            treasureKeys += count;
            SaveInventoryToProfile();
        }

        public bool TryConsumeTreasureKey()
        {
            if (treasureKeys > 0)
            {
                treasureKeys--;
                SaveInventoryToProfile();
                return true;
            }
            return false;
        }

        #endregion
    }
}
