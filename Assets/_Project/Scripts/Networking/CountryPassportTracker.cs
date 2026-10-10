using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SudokuVR.Networking
{
    [Serializable]
    public class CountryStamp
    {
        public string countryCode;
        public string countryName;
        public bool isUnlocked;
        public string dateUnlocked;
        public int victoriesAgainst;

        public CountryStamp(string code, string name)
        {
            countryCode = code;
            countryName = name;
            isUnlocked = false;
            dateUnlocked = "";
            victoriesAgainst = 0;
        }
    }

    [Serializable]
    public class PassportInventoryData
    {
        public List<CountryStamp> stamps = new List<CountryStamp>();
    }

    /// <summary>
    /// Tracks collection of country postage stamps earned in 1v1 Battle Mode duels.
    /// Stores persistent stamp gallery in persistentDataPath.
    /// </summary>
    public class CountryPassportTracker : MonoBehaviour
    {
        public static CountryPassportTracker Instance { get; private set; }

        private PassportInventoryData _data = new PassportInventoryData();
        private string _savePath;

        public List<CountryStamp> Stamps => _data.stamps;
        public int UnlockedCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < _data.stamps.Count; i++)
                {
                    if (_data.stamps[i].isUnlocked) c++;
                }
                return c;
            }
        }

        public event Action<CountryStamp> OnStampUnlocked;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitializeStamps();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void InitializeStamps()
        {
            _savePath = Path.Combine(Application.persistentDataPath, "zenith_passport_stamps.json");
            LoadStamps();

            if (_data.stamps == null || _data.stamps.Count == 0)
            {
                SeedCountryList();
                SaveStamps();
            }
        }

        private void SeedCountryList()
        {
            string[] popularCountries = new string[]
            {
                "Denmark", "Japan", "Norway", "United States", "Germany", "United Kingdom",
                "France", "Canada", "Australia", "South Korea", "Sweden", "Finland",
                "Netherlands", "Switzerland", "New Zealand", "Italy", "Spain", "Brazil",
                "Argentina", "Mexico", "Singapore", "Ireland", "Iceland", "Belgium",
                "Austria", "Poland", "Czech Republic", "Portugal", "Greece", "South Africa"
            };

            _data.stamps = new List<CountryStamp>();
            for (int i = 0; i < popularCountries.Length; i++)
            {
                string name = popularCountries[i];
                string code = name.Substring(0, Math.Min(3, name.Length)).ToUpper();
                CountryStamp stamp = new CountryStamp(code, name);

                // Start with 2 default sample stamps unlocked for initial visual delight
                if (i == 0 || i == 1)
                {
                    stamp.isUnlocked = true;
                    stamp.dateUnlocked = "2026-10-10";
                    stamp.victoriesAgainst = 1;
                }

                _data.stamps.Add(stamp);
            }
        }

        public void UnlockCountryStamp(string countryName)
        {
            CountryStamp stamp = _data.stamps.Find(s => s.countryName.Equals(countryName, StringComparison.OrdinalIgnoreCase));
            if (stamp != null)
            {
                stamp.isUnlocked = true;
                stamp.dateUnlocked = DateTime.UtcNow.ToString("yyyy-MM-dd");
                stamp.victoriesAgainst++;
                SaveStamps();
                OnStampUnlocked?.Invoke(stamp);
            }
        }

        public void SaveStamps()
        {
            try
            {
                string json = JsonUtility.ToJson(_data, true);
                File.WriteAllText(_savePath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CountryPassportTracker] Failed to save stamps: {ex.Message}");
            }
        }

        public void LoadStamps()
        {
            try
            {
                if (File.Exists(_savePath))
                {
                    string json = File.ReadAllText(_savePath);
                    _data = JsonUtility.FromJson<PassportInventoryData>(json);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CountryPassportTracker] Error loading stamps: {ex.Message}");
            }
        }
    }
}
