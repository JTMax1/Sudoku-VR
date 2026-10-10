using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Dynamic scoring engine. Calculates points based on base value, completion speed,
    /// and combo streak multiplier without mistakes.
    /// </summary>
    public class ScoringEngine : MonoBehaviour
    {
        [SerializeField] private int baseDigitPoints = 50;
        [SerializeField] private int mistakePenalty = 100;
        [SerializeField] private float streakMultiplierIncrement = 0.2f;
        [SerializeField] private float maxStreakMultiplier = 3.0f;

        private int _currentScore;
        private int _currentStreak;
        private float _currentMultiplier = 1.0f;

        public int CurrentScore => _currentScore;
        public int CurrentStreak => _currentStreak;
        public float CurrentMultiplier => _currentMultiplier;

        public event Action<int, int> OnScoreChanged; // (currentScore, pointsDelta)

        public void ResetScore()
        {
            _currentScore = 0;
            _currentStreak = 0;
            _currentMultiplier = 1.0f;
            OnScoreChanged?.Invoke(_currentScore, 0);
        }

        public void RecordCorrectPlacement(Difficulty difficulty)
        {
            _currentStreak++;
            _currentMultiplier = Mathf.Min(1.0f + (_currentStreak * streakMultiplierIncrement), maxStreakMultiplier);

            int diffFactor = (int)difficulty + 1;
            int points = Mathf.RoundToInt(baseDigitPoints * diffFactor * _currentMultiplier);

            _currentScore += points;
            OnScoreChanged?.Invoke(_currentScore, points);
        }

        public void RecordMistake()
        {
            _currentStreak = 0;
            _currentMultiplier = 1.0f;
            _currentScore = Mathf.Max(0, _currentScore - mistakePenalty);
            OnScoreChanged?.Invoke(_currentScore, -mistakePenalty);
        }

        public int CalculateFinalBonus(float elapsedSeconds, Difficulty difficulty, int mistakes)
        {
            if (mistakes >= 3) return 0;

            int speedBonus = Mathf.Max(0, 1000 - Mathf.RoundToInt(elapsedSeconds * 2f));
            int accuracyBonus = (mistakes == 0) ? 500 : (mistakes == 1 ? 250 : 100);
            int diffBonus = ((int)difficulty + 1) * 300;

            int finalBonus = speedBonus + accuracyBonus + diffBonus;
            _currentScore += finalBonus;
            OnScoreChanged?.Invoke(_currentScore, finalBonus);
            return finalBonus;
        }
    }
}
