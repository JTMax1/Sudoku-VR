using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Tactile 3D arcade Daily Reward Gumball/Capsule Dispenser for Sudoku Zenith MR.
    /// Features physical pull lever, tumbling prize capsule, coin/hint credit grant,
    /// and strict once-per-day persistence to prevent duplicate claims.
    /// </summary>
    public class RewardDispenserProp : MonoBehaviour
    {
        [Header("Physical Dispenser Components")]
        [SerializeField] private Transform leverHandle;
        [SerializeField] private Transform prizeCapsule;
        [SerializeField] private TMP_Text statusText;

        [Header("Animation Settings")]
        [SerializeField] private float leverTravelAngle = 45f;
        [SerializeField] private float leverReturnSpeed = 120f;

        private bool _isDispensing = false;
        private bool _isLeverPulled = false;
        private Quaternion _leverRestRotation;
        private Quaternion _leverPulledRotation;
        private Vector3 _capsuleRestLocalPos;

        private const string LastRewardDateKey = "Zenith_LastDailyRewardDate";

        private void Awake()
        {
            if (leverHandle != null)
            {
                _leverRestRotation = leverHandle.localRotation;
                _leverPulledRotation = _leverRestRotation * Quaternion.Euler(leverTravelAngle, 0f, 0f);
            }

            if (prizeCapsule != null)
            {
                _capsuleRestLocalPos = prizeCapsule.localPosition;
                prizeCapsule.gameObject.SetActive(false);
            }

            UpdateStatusDisplay();
        }

        private void Update()
        {
            if (_isLeverPulled && leverHandle != null)
            {
                leverHandle.localRotation = Quaternion.RotateTowards(
                    leverHandle.localRotation,
                    _leverRestRotation,
                    leverReturnSpeed * Time.deltaTime
                );

                if (Quaternion.Angle(leverHandle.localRotation, _leverRestRotation) < 0.5f)
                {
                    leverHandle.localRotation = _leverRestRotation;
                    _isLeverPulled = false;
                }
            }
        }

        public bool CanClaimDailyReward()
        {
            string lastDate = PlayerPrefs.GetString(LastRewardDateKey, string.Empty);
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            return lastDate != today;
        }

        public void PullLever()
        {
            if (_isDispensing) return;

            if (!CanClaimDailyReward())
            {
                TactileAudioService.Instance?.PlayConflict();
                if (statusText != null) statusText.text = "COME BACK TOMORROW!";
                Debug.Log("[Reward Dispenser] Daily reward already claimed today!");
                return;
            }

            _isDispensing = true;
            _isLeverPulled = true;
            if (leverHandle != null)
            {
                leverHandle.localRotation = _leverPulledRotation;
            }

            TactileAudioService.Instance?.PlayTap();

            CancelInvoke(nameof(DispenseCapsule));
            Invoke(nameof(DispenseCapsule), 0.35f);
        }

        private void DispenseCapsule()
        {
            if (prizeCapsule != null)
            {
                prizeCapsule.gameObject.SetActive(true);
                prizeCapsule.localPosition = _capsuleRestLocalPos + new Vector3(0f, 0.08f, 0f); // Drops into tray
            }

            // Grant rewards (100 Brain Coins + 1 Free Smart Hint)
            int coinReward = 100;
            int hintReward = 1;

            var profile = ProgressionManager.Instance?.Profile;
            if (profile != null)
            {
                profile.brainCoins += coinReward;
                profile.hintsAvailable += hintReward;
                ProgressionManager.Instance.SaveProfile();
            }

            // Mark date claimed
            string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
            PlayerPrefs.SetString(LastRewardDateKey, today);
            PlayerPrefs.Save();

            TactileAudioService.Instance?.PlayVictoryFanfare();

            if (statusText != null)
            {
                statusText.text = $"+{coinReward} COINS! +{hintReward} HINT!";
            }

            Debug.Log($"<color=#FFD700><b>[Daily Reward] Granted +{coinReward} Brain Coins and +{hintReward} Smart Hint!</b></color>");

            CancelInvoke(nameof(ResetDispenser));
            Invoke(nameof(ResetDispenser), 3.0f);
        }

        private void ResetDispenser()
        {
            _isDispensing = false;
            if (prizeCapsule != null)
            {
                prizeCapsule.gameObject.SetActive(false);
            }
            UpdateStatusDisplay();
        }

        private void UpdateStatusDisplay()
        {
            if (statusText == null) return;

            if (CanClaimDailyReward())
            {
                statusText.text = "PULL LEVER FOR DAILY REWARD!";
                statusText.color = new Color(0.95f, 0.85f, 0.3f, 1f);
            }
            else
            {
                statusText.text = "DAILY REWARD CLAIMED!";
                statusText.color = new Color(0.6f, 0.65f, 0.7f, 1f);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            PullLever();
        }
    }
}
