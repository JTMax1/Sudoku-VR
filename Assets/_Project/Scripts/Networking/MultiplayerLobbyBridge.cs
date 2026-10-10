using System;
using UnityEngine;

namespace SudokuVR.Networking
{
    public enum MatchmakingStatus
    {
        Idle,
        Searching,
        OpponentFound,
        MatchStarting,
        InMatch
    }

    /// <summary>
    /// Matchmaking bridge for 1v1 Battle Mode.
    /// Handles seasonal ticket entry, search simulation, and duel matchmaking state.
    /// </summary>
    public class MultiplayerLobbyBridge : MonoBehaviour
    {
        public static MultiplayerLobbyBridge Instance { get; private set; }

        [Header("Matchmaking Configuration")]
        [SerializeField] private int ticketCost = 25;
        [SerializeField] private string seasonName = "Season 1: Horizon Zenith";

        private MatchmakingStatus _status = MatchmakingStatus.Idle;

        public MatchmakingStatus Status => _status;
        public string SeasonName => seasonName;
        public int TicketCost => ticketCost;

        public event Action<MatchmakingStatus> OnStatusChanged;
        public event Action<string, string> OnMatchFound; // opponentName, opponentCountry

        private void Awake()
        {
            if (Instance == null) Instance = this;
        }

        public void StartMatchmaking()
        {
            _status = MatchmakingStatus.Searching;
            OnStatusChanged?.Invoke(_status);

            // Simulate quick matchmaking delay
            CancelInvoke(nameof(SimulateOpponentFound));
            Invoke(nameof(SimulateOpponentFound), 2.5f);
        }

        public void CancelMatchmaking()
        {
            CancelInvoke(nameof(SimulateOpponentFound));
            _status = MatchmakingStatus.Idle;
            OnStatusChanged?.Invoke(_status);
        }

        private void SimulateOpponentFound()
        {
            _status = MatchmakingStatus.OpponentFound;
            OnStatusChanged?.Invoke(_status);

            string[] names = { "Aura_07", "Solstice_VR", "Krona99", "PixelZen", "NordicMind" };
            string[] countries = { "Denmark", "Japan", "Norway", "France", "Germany" };

            int idx = UnityEngine.Random.Range(0, names.Length);
            OnMatchFound?.Invoke(names[idx], countries[idx]);
        }
    }
}
