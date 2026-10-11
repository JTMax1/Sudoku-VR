using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Gameplay;

namespace SudokuVR.UI
{
    /// <summary>
    /// Tactile 3D Level Pedestal on the Living Alpine Campaign Diorama.
    /// Displays node index, locked padlock, and 1-3 gold stars.
    /// Responds to direct finger poke and distance ray clicks.
    /// </summary>
    public class DioramaLevelNode : MonoBehaviour
    {
        [Header("Level Information")]
        [SerializeField] private Difficulty difficulty = Difficulty.Beginner;
        [SerializeField] private int levelNumber = 1;

        [Header("Visual Components")]
        [SerializeField] private MeshRenderer pedestalRenderer;
        [SerializeField] private TMP_Text levelNumberText;
        [SerializeField] private GameObject padlockRoot;
        [SerializeField] private GameObject[] starIcons = new GameObject[3];

        [Header("Colors (URP)")]
        [SerializeField] private Color lockedColor = new Color(0.35f, 0.38f, 0.42f, 1f);
        [SerializeField] private Color availableColor = new Color(0.95f, 0.82f, 0.35f, 1f);
        [SerializeField] private Color completedColor = new Color(0.35f, 0.85f, 0.55f, 1f);

        private bool _isUnlocked = true;
        private int _starsEarned = 0;
        private float _lastClickTime;

        public Difficulty Difficulty => difficulty;
        public int LevelNumber => levelNumber;
        public bool IsUnlocked => _isUnlocked;

        public event Action<DioramaLevelNode> OnNodeSelected;

        public void Setup(Difficulty diff, int level, bool unlocked, int stars)
        {
            difficulty = diff;
            levelNumber = level;
            _isUnlocked = unlocked;
            _starsEarned = Mathf.Clamp(stars, 0, 3);

            UpdateVisualState();
        }

        public void UpdateVisualState()
        {
            if (levelNumberText != null)
            {
                levelNumberText.text = levelNumber.ToString();
            }

            if (padlockRoot != null)
            {
                padlockRoot.SetActive(!_isUnlocked);
            }

            if (starIcons != null)
            {
                for (int i = 0; i < starIcons.Length; i++)
                {
                    if (starIcons[i] != null)
                    {
                        starIcons[i].SetActive(_isUnlocked && i < _starsEarned);
                    }
                }
            }

            if (pedestalRenderer != null)
            {
                Material mat = pedestalRenderer.material;
                if (mat != null)
                {
                    if (!_isUnlocked) mat.color = lockedColor;
                    else if (_starsEarned > 0) mat.color = completedColor;
                    else mat.color = availableColor;
                }
            }
        }

        public void TriggerNodeClick()
        {
            if (Time.time - _lastClickTime < 0.3f) return;
            _lastClickTime = Time.time;

            if (!_isUnlocked)
            {
                TactileAudioService.Instance?.PlayConflict();
                Debug.Log($"[Campaign Diorama] Level {levelNumber} is locked! Complete prior tiers to unlock.");
                return;
            }

            TactileAudioService.Instance?.PlayTap();
            OnNodeSelected?.Invoke(this);

            var mm = FindAnyObjectByType<MainMenuController>();
            if (mm != null)
            {
                mm.StartGameWithDifficulty(difficulty);
            }
            else if (SudokuGameManager.Instance != null)
            {
                SudokuGameManager.Instance.StartNewGame(difficulty);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            TriggerNodeClick();
        }
    }
}
