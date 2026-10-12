using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    public enum GameEventMode
    {
        ClassicZen = 0,
        SupernovaRush = 1,
        MirrorSymmetryTrial = 2,
        GrandPrixQualifier = 3
    }

    /// <summary>
    /// Event mode controller delivering unique time-based and rule-based challenges:
    /// - Supernova Rush: Countdown clock where placements add +15s and streaks add +30s.
    /// - Mirror Symmetry Trial: Symmetrical quadrant bonuses.
    /// - Grand Prix Qualifier: Zero hints, strict heart mistakes, trophy rewards.
    /// </summary>
    public class SudokuEventModes : MonoBehaviour
    {
        public static SudokuEventModes Instance { get; private set; }

        [SerializeField] private GameEventMode activeMode = GameEventMode.ClassicZen;

        [Header("Supernova Rush Parameters")]
        [SerializeField] private float rushStartingTime = 90f;
        [SerializeField] private float timePerPlacement = 15f;
        [SerializeField] private float timePerStreak = 30f;

        private float _rushTimeRemaining;
        private bool _isRushActive;

        public GameEventMode ActiveMode => activeMode;
        public float RushTimeRemaining => _rushTimeRemaining;
        public bool IsRushActive => _isRushActive;

        public event Action<float> OnRushTimeUpdated;
        public event Action OnSupernovaExpired;

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else if (Instance != this) { Destroy(gameObject); return; }
        }

        private void Start()
        {
            var gm = SudokuGameManager.Instance;
            if (gm != null)
            {
                gm.OnGameFinished += HandleGameFinished;
            }
        }

        private void Update()
        {
            if (_isRushActive && activeMode == GameEventMode.SupernovaRush)
            {
                _rushTimeRemaining -= Time.deltaTime;
                OnRushTimeUpdated?.Invoke(_rushTimeRemaining);

                if (_rushTimeRemaining <= 0f)
                {
                    _isRushActive = false;
                    _rushTimeRemaining = 0f;
                    OnSupernovaExpired?.Invoke();
                    TactileAudioService.Instance?.PlayConflict();
                    Debug.LogWarning("<color=#FF3B30><b>[Supernova Rush] Time expired!</b></color>");
                }
            }
        }

        public void StartEventMode(GameEventMode mode, Difficulty difficulty)
        {
            activeMode = mode;

            if (mode == GameEventMode.SupernovaRush)
            {
                _rushTimeRemaining = rushStartingTime;
                _isRushActive = true;
                Debug.Log($"<color=#FFD700><b>[Supernova Rush Started] {rushStartingTime}s on the clock! Every correct placement adds +{timePerPlacement}s!</b></color>");
            }
            else
            {
                _isRushActive = false;
            }

            SudokuGameManager.Instance?.StartNewGame(difficulty);
        }

        public void NotifyPlacement(bool isCorrect, int streakCount)
        {
            if (activeMode == GameEventMode.SupernovaRush && _isRushActive && isCorrect)
            {
                float addTime = timePerPlacement;
                if (streakCount >= 3)
                {
                    addTime += timePerStreak;
                    TactileAudioService.Instance?.PlayVictoryFanfare();
                }

                _rushTimeRemaining += addTime;
                Debug.Log($"<color=#00FFFF><b>[Supernova Surge] +{addTime}s added to countdown! Remaining: {_rushTimeRemaining:F1}s</b></color>");
            }
        }

        private void HandleGameFinished(bool victory)
        {
            _isRushActive = false;
        }
    }
}
