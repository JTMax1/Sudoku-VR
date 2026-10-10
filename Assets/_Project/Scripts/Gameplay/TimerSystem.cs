using System;
using UnityEngine;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Tracks active gameplay elapsed time with pause, resume, and formatted metrics.
    /// Zero garbage collection allocations during active counting.
    /// </summary>
    public class TimerSystem : MonoBehaviour
    {
        private float _elapsedSeconds;
        private bool _isRunning;

        public float ElapsedSeconds => _elapsedSeconds;
        public bool IsRunning => _isRunning;

        public event Action<float> OnTimerTick;

        private void Update()
        {
            if (_isRunning)
            {
                _elapsedSeconds += Time.deltaTime;
                OnTimerTick?.Invoke(_elapsedSeconds);
            }
        }

        public void StartTimer()
        {
            _isRunning = true;
        }

        public void PauseTimer()
        {
            _isRunning = false;
        }

        public void ResumeTimer()
        {
            _isRunning = true;
        }

        public void ResetTimer()
        {
            _elapsedSeconds = 0f;
            _isRunning = false;
        }

        public static string FormatTime(float totalSeconds)
        {
            int total = Mathf.FloorToInt(totalSeconds);
            int minutes = total / 60;
            int seconds = total % 60;
            return $"{minutes:D2}:{seconds:D2}";
        }
    }
}
