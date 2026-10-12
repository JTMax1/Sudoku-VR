using System;
using TMPro;
using UnityEngine;
using SudokuVR.Core;
using SudokuVR.Gameplay;

namespace SudokuVR.Interaction
{
    /// <summary>
    /// Chunky 3D Action Tokens Dock for Sudoku Zenith MR.
    /// Hosts 4 physical handheld tokens (Undo, Erase, Notes, and Magic Wand Hint).
    /// Integrates directly with the GameBoosterInventory and SudokuGameManager.
    /// </summary>
    public class ActionTokensDock : MonoBehaviour
    {
        [Header("Token Handles")]
        [SerializeField] private Transform tokenUndo;
        [SerializeField] private Transform tokenErase;
        [SerializeField] private Transform tokenNotes;
        [SerializeField] private Transform tokenWand;

        [Header("Badge Displays")]
        [SerializeField] private TMP_Text wandCountBadge;
        [SerializeField] private TMP_Text shieldCountBadge;

        [Header("Tactile Depression")]
        [SerializeField] private float pressDepth = 0.010f; // 10mm physical depression

        private Vector3 _undoRestPos;
        private Vector3 _eraseRestPos;
        private Vector3 _notesRestPos;
        private Vector3 _wandRestPos;

        private void Awake()
        {
            if (tokenUndo != null) _undoRestPos = tokenUndo.localPosition;
            if (tokenErase != null) _eraseRestPos = tokenErase.localPosition;
            if (tokenNotes != null) _notesRestPos = tokenNotes.localPosition;
            if (tokenWand != null) _wandRestPos = tokenWand.localPosition;
        }

        private void Start()
        {
            var inv = GameBoosterInventory.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged += UpdateBadgeCounts;
            }
            UpdateBadgeCounts();
        }

        private void OnDestroy()
        {
            var inv = GameBoosterInventory.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged -= UpdateBadgeCounts;
            }
        }

        public void UpdateBadgeCounts()
        {
            var inv = GameBoosterInventory.Instance;
            if (inv != null)
            {
                if (wandCountBadge != null)
                {
                    wandCountBadge.text = $"x{inv.MagicWandHints}";
                    wandCountBadge.color = inv.MagicWandHints > 0 ? Color.yellow : Color.gray;
                }
                if (shieldCountBadge != null)
                {
                    shieldCountBadge.text = $"x{inv.HeartShields}";
                    shieldCountBadge.color = inv.HeartShields > 0 ? Color.cyan : Color.gray;
                }
            }
        }

        #region Token Poke Actions

        public void PokeUndo()
        {
            DepressToken(tokenUndo, _undoRestPos);
            SudokuGameManager.Instance?.HandleInputUndo();
        }

        public void PokeErase()
        {
            DepressToken(tokenErase, _eraseRestPos);
            SudokuGameManager.Instance?.HandleInputErase();
        }

        public void PokeNotes()
        {
            DepressToken(tokenNotes, _notesRestPos);
            var keypad = FindAnyObjectByType<FloatingKeypadView>();
            if (keypad != null)
            {
                keypad.TogglePencilMode();
            }
        }

        public void PokeMagicWand()
        {
            DepressToken(tokenWand, _wandRestPos);

            var inv = GameBoosterInventory.Instance;
            if (inv != null && inv.TryConsumeMagicWand())
            {
                SudokuGameManager.Instance?.HandleInputHintRequested();
            }
            else
            {
                TactileAudioService.Instance?.PlayConflict();
                Debug.LogWarning("[Action Dock] No Magic Wand charges remaining! Earn more from the Treasure Chest.");
            }
        }

        private void DepressToken(Transform token, Vector3 restPos)
        {
            if (token != null)
            {
                token.localPosition = restPos + new Vector3(0f, 0f, pressDepth);
                CancelInvoke(nameof(ReturnTokens));
                Invoke(nameof(ReturnTokens), 0.12f);
            }
        }

        private void ReturnTokens()
        {
            if (tokenUndo != null) tokenUndo.localPosition = _undoRestPos;
            if (tokenErase != null) tokenErase.localPosition = _eraseRestPos;
            if (tokenNotes != null) tokenNotes.localPosition = _notesRestPos;
            if (tokenWand != null) tokenWand.localPosition = _wandRestPos;
        }

        #endregion
    }
}
