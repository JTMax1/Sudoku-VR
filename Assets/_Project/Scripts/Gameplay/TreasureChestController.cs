using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Interactive 3D Treasure Chest using the imported FBX model.
    /// Provides full anticipation animation, lid rotation, reward item emergence,
    /// and grants tangible boosters (Shields, Wand Hints, Coins) directly to inventory.
    /// </summary>
    public class TreasureChestController : MonoBehaviour
    {
        [Header("Chest Model References")]
        [SerializeField] private Transform chestBase;
        [SerializeField] private Transform chestLid;
        [SerializeField] private Transform rewardSpawnPoint;
        [SerializeField] private TMP_Text rewardBannerText;

        [Header("Animation Settings")]
        [SerializeField] private float lidOpenAngle = 80f;
        [SerializeField] private float lidOpenSpeed = 160f;

        private bool _isUnlocked = false;
        private bool _isOpening = false;
        private Quaternion _lidClosedRot;
        private Quaternion _lidOpenRot;
        private Vector3 _baseInitialLocalPos;

        private void Awake()
        {
            if (chestBase == null)
            {
                chestBase = transform.Find("chest") ?? transform;
            }

            _baseInitialLocalPos = chestBase.localPosition;

            if (chestLid == null)
            {
                // Find or create lid pivot
                Transform foundLid = chestBase.Find("Lid") ?? chestBase.Find("chest_lid");
                if (foundLid != null)
                {
                    chestLid = foundLid;
                }
            }

            if (chestLid != null)
            {
                _lidClosedRot = chestLid.localRotation;
                _lidOpenRot = _lidClosedRot * Quaternion.Euler(-lidOpenAngle, 0f, 0f);
            }

            if (rewardBannerText != null)
            {
                rewardBannerText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // Anticipation wobble during opening
            if (_isOpening)
            {
                float wobble = Mathf.Sin(Time.time * 24f) * 0.008f;
                chestBase.localPosition = _baseInitialLocalPos + new Vector3(wobble, 0f, 0f);

                if (chestLid != null)
                {
                    chestLid.localRotation = Quaternion.RotateTowards(
                        chestLid.localRotation,
                        _lidOpenRot,
                        lidOpenSpeed * Time.deltaTime
                    );

                    if (Quaternion.Angle(chestLid.localRotation, _lidOpenRot) < 1f)
                    {
                        chestLid.localRotation = _lidOpenRot;
                        _isOpening = false;
                        chestBase.localPosition = _baseInitialLocalPos;
                        DispensePayload();
                    }
                }
            }
        }

        public void TriggerUnlockChest()
        {
            if (_isUnlocked || _isOpening) return;

            _isUnlocked = true;
            _isOpening = true;

            TactileAudioService.Instance?.PlayTap();
            Debug.Log("[Treasure Chest] Unlocking chest sequence initiated!");
        }

        private void DispensePayload()
        {
            // Grant tangible boosters: +1 Heart Shield, +2 Magic Wand Hints, +150 Brain Coins
            int shields = 1;
            int hints = 2;
            int timeFreezes = 1;
            int coins = 150;

            GameBoosterInventory.Instance?.GrantRewards(shields, hints, timeFreezes, coins);
            TactileAudioService.Instance?.PlayVictoryFanfare();

            if (rewardBannerText != null)
            {
                rewardBannerText.gameObject.SetActive(true);
                rewardBannerText.text = $"<color=#00FFFF>+1 SHIELD!</color>  <color=#FFD700>+2 WANDS!</color>  <color=#34C759>+{coins} COINS!</color>";
            }

            CancelInvoke(nameof(HideChestBanner));
            Invoke(nameof(HideChestBanner), 4.5f);
        }

        private void HideChestBanner()
        {
            if (rewardBannerText != null)
            {
                rewardBannerText.gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TriggerUnlockChest();
        }
    }
}
