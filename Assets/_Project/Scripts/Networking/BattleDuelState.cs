using System;
using UnityEngine;

namespace SudokuVR.Networking
{
    [Serializable]
    public class DuelProfile
    {
        public string playerName;
        public string country;
        public int currentRating;
        public float completionPercentage;
        public int currentMistakes;
    }

    /// <summary>
    /// Tracks head-to-head 1v1 speed duel telemetry and race progress.
    /// </summary>
    public class BattleDuelState : MonoBehaviour
    {
        [SerializeField] private DuelProfile localPlayer = new DuelProfile { playerName = "You", country = "Global", currentRating = 1200 };
        [SerializeField] private DuelProfile opponent = new DuelProfile { playerName = "Valkyrie_XR", country = "Denmark", currentRating = 1240 };

        public DuelProfile LocalPlayer => localPlayer;
        public DuelProfile Opponent => opponent;

        public event Action<float, float> OnProgressUpdated; // (localPct, opponentPct)
        public event Action<string> OnDuelEnded; // winnerName

        public void UpdateLocalProgress(float pct)
        {
            localPlayer.completionPercentage = Mathf.Clamp01(pct);
            OnProgressUpdated?.Invoke(localPlayer.completionPercentage, opponent.completionPercentage);

            if (localPlayer.completionPercentage >= 1.0f)
            {
                OnDuelEnded?.Invoke(localPlayer.playerName);
            }
        }

        public void SimulateOpponentTick(float delta)
        {
            opponent.completionPercentage = Mathf.Clamp01(opponent.completionPercentage + delta);
            OnProgressUpdated?.Invoke(localPlayer.completionPercentage, opponent.completionPercentage);

            if (opponent.completionPercentage >= 1.0f)
            {
                OnDuelEnded?.Invoke(opponent.playerName);
            }
        }
    }
}
