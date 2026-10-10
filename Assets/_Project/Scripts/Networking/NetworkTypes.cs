using System;
using UnityEngine;

namespace SudokuVR.Networking
{
    /// <summary>
    /// Lightweight player identifier for multiplayer ownership and peer cursor highlights.
    /// Memory footprint: 1 byte.
    /// </summary>
    [Serializable]
    public struct PlayerId : IEquatable<PlayerId>
    {
        public const byte Server = 0;
        public const byte Local = 1;
        public const byte Invalid = 255;

        [SerializeField] private byte value;

        public byte Value => value;

        public PlayerId(byte id)
        {
            value = id;
        }

        public bool IsValid => value != Invalid && value != Server;
        public bool IsLocal => value == Local;

        public static PlayerId LocalPlayer => new PlayerId(Local);
        public static PlayerId ServerPlayer => new PlayerId(Server);
        public static PlayerId InvalidPlayer => new PlayerId(Invalid);

        public static Color GetPlayerColor(PlayerId id)
        {
            switch (id.value)
            {
                case 1: return new Color(0.12f, 0.65f, 1.0f);   // Electric Blue
                case 2: return new Color(1.0f, 0.65f, 0.0f);    // Amber Orange
                case 3: return new Color(0.18f, 0.85f, 0.45f);  // Emerald Mint
                case 4: return new Color(0.95f, 0.30f, 0.50f);  // Rose Coral
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        public bool Equals(PlayerId other) => value == other.value;
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => value.GetHashCode();
        public override string ToString() => $"Player({value})";

        public static bool operator ==(PlayerId left, PlayerId right) => left.value == right.value;
        public static bool operator !=(PlayerId left, PlayerId right) => left.value != right.value;
    }

    /// <summary>
    /// Decoupled interface for multiplayer state synchronization.
    /// Ready to plug into Unity Netcode for GameObjects, Photon Fusion, or Normcore.
    /// </summary>
    public interface ISudokuNetworkSync
    {
        bool IsConnected { get; }
        PlayerId LocalPlayerId { get; }

        void SendCellModified(int row, int col, int value, PlayerId player);
        void SendCellSelected(int row, int col, PlayerId player);

        event Action<int, int, int, PlayerId> OnRemoteCellModified;
        event Action<int, int, PlayerId> OnRemoteCellSelected;
    }

    /// <summary>
    /// Local standalone loopback implementation of ISudokuNetworkSync.
    /// Provides zero-overhead local simulation while honoring the multiplayer contract.
    /// </summary>
    public class LocalSudokuNetworkSync : MonoBehaviour, ISudokuNetworkSync
    {
        public bool IsConnected => false;
        public PlayerId LocalPlayerId => PlayerId.LocalPlayer;

        public event Action<int, int, int, PlayerId> OnRemoteCellModified;
        public event Action<int, int, PlayerId> OnRemoteCellSelected;

        public void SendCellModified(int row, int col, int value, PlayerId player)
        {
            // In local standalone mode, changes are processed locally without network roundtrip.
        }

        public void SendCellSelected(int row, int col, PlayerId player)
        {
            // In local standalone mode, selection is processed locally.
        }

        public void SimulateRemoteCellModified(int row, int col, int value, PlayerId peer)
        {
            OnRemoteCellModified?.Invoke(row, col, value, peer);
        }

        public void SimulateRemoteCellSelected(int row, int col, PlayerId peer)
        {
            OnRemoteCellSelected?.Invoke(row, col, peer);
        }
    }
}
