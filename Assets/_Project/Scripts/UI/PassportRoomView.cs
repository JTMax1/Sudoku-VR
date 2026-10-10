using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SudokuVR.Networking;

namespace SudokuVR.UI
{
    /// <summary>
    /// Gallery view of country postage stamps earned in 1v1 Battle Mode.
    /// Displays collection metrics, country flags/seals, and victory timestamps.
    /// </summary>
    public class PassportRoomView : MonoBehaviour
    {
        [Header("Header References")]
        [SerializeField] private TMP_Text stampCountText;
        [SerializeField] private Button backButton;

        [Header("Grid Layout Container")]
        [SerializeField] private Transform gridContainer;
        [SerializeField] private GameObject stampCardPrefab;

        private void Start()
        {
            if (backButton != null)
            {
                backButton.onClick.AddListener(OnBackClicked);
            }
            RefreshGallery();
        }

        public void RefreshGallery()
        {
            CountryPassportTracker tracker = CountryPassportTracker.Instance ?? FindAnyObjectByType<CountryPassportTracker>();
            if (tracker == null) return;

            List<CountryStamp> stamps = tracker.Stamps;
            int unlocked = tracker.UnlockedCount;

            if (stampCountText != null)
            {
                stampCountText.text = $"PASSPORT STAMPS: {unlocked}/{stamps.Count} UNLOCKED";
            }

            if (gridContainer == null) return;

            // Clear previous items
            for (int i = gridContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(gridContainer.GetChild(i).gameObject);
            }

            // Populate cards
            for (int i = 0; i < stamps.Count; i++)
            {
                CountryStamp stamp = stamps[i];
                GameObject card = stampCardPrefab != null
                    ? Instantiate(stampCardPrefab, gridContainer)
                    : CreateProceduralCard(stamp);

                card.name = $"Stamp_{stamp.countryCode}";
            }
        }

        private GameObject CreateProceduralCard(CountryStamp stamp)
        {
            GameObject cardObj = new GameObject($"Card_{stamp.countryCode}", typeof(RectTransform), typeof(Image));
            cardObj.transform.SetParent(gridContainer, false);

            Image img = cardObj.GetComponent<Image>();
            img.color = stamp.isUnlocked ? new Color(0.18f, 0.24f, 0.35f, 0.95f) : new Color(0.10f, 0.12f, 0.15f, 0.6f);

            // Stamp Code Label
            GameObject codeObj = new GameObject("Code", typeof(RectTransform), typeof(TextMeshProUGUI));
            codeObj.transform.SetParent(cardObj.transform, false);
            TextMeshProUGUI codeTmp = codeObj.GetComponent<TextMeshProUGUI>();
            codeTmp.alignment = TextAlignmentOptions.Center;
            codeTmp.fontSize = 20f;
            codeTmp.fontStyle = FontStyles.Bold;
            codeTmp.text = stamp.countryCode;
            codeTmp.color = stamp.isUnlocked ? new Color(1.0f, 0.85f, 0.3f) : new Color(0.5f, 0.5f, 0.5f);

            // Country Name
            GameObject nameObj = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI));
            nameObj.transform.SetParent(cardObj.transform, false);
            TextMeshProUGUI nameTmp = nameObj.GetComponent<TextMeshProUGUI>();
            nameTmp.alignment = TextAlignmentOptions.Center;
            nameTmp.fontSize = 12f;
            nameTmp.text = stamp.isUnlocked ? stamp.countryName : "???";
            nameTmp.color = stamp.isUnlocked ? Color.white : new Color(0.4f, 0.4f, 0.4f);

            RectTransform codeRect = codeObj.GetComponent<RectTransform>();
            codeRect.anchoredPosition = new Vector2(0f, 12f);
            codeRect.sizeDelta = new Vector2(100f, 30f);

            RectTransform nameRect = nameObj.GetComponent<RectTransform>();
            nameRect.anchoredPosition = new Vector2(0f, -16f);
            nameRect.sizeDelta = new Vector2(100f, 25f);

            return cardObj;
        }

        private void OnBackClicked()
        {
            MainMenuController home = FindAnyObjectByType<MainMenuController>();
            if (home != null) home.ShowHubView();
        }
    }
}
