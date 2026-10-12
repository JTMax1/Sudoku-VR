using System;
using UnityEngine;
using SudokuVR.Core;

namespace SudokuVR.Gameplay
{
    /// <summary>
    /// Volumetric 3D Heart Tokens Bar for Sudoku Zenith MR.
    /// Manages the 3 glowing physical heart tokens floating above the monolith frame.
    /// Features:
    /// - Protective Heart Shield aura integration
    /// - Physical heart cracking and shattering with particle bursts
    /// - Dynamic rest and pulse animations
    /// </summary>
    public class HeartTokensBar : MonoBehaviour
    {
        [Header("Heart Token Roots (3 Tokens)")]
        [SerializeField] private Transform[] heartRoots = new Transform[3];
        [SerializeField] private MeshRenderer[] heartRenderers = new MeshRenderer[3];
        [SerializeField] private GameObject shieldAuraIndicator;

        [Header("Visual Colors (URP Lit)")]
        [SerializeField] private Color heartAliveColor = new Color(0.95f, 0.22f, 0.28f, 1f);     // Vibrant Ruby Red
        [SerializeField] private Color heartCrackedColor = new Color(0.40f, 0.15f, 0.18f, 1f);   // Shattered Dark Crimson
        [SerializeField] private Color shieldAuraColor = new Color(0.20f, 0.85f, 1.0f, 0.8f);    // Ethereal Cyan Shield

        private int _currentLives = 3;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();

            if (heartRoots == null || heartRoots.Length == 0 || heartRoots[0] == null)
            {
                BuildProceduralHeartTokens();
            }

            UpdateVisuals();
        }

        private void Start()
        {
            var gm = SudokuGameManager.Instance;
            if (gm != null)
            {
                gm.OnMistakesUpdated += HandleMistakesUpdated;
            }

            var inv = GameBoosterInventory.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged += UpdateShieldAura;
                inv.OnHeartShieldAbsorbed += HandleShieldAbsorbed;
            }

            UpdateShieldAura();
        }

        private void OnDestroy()
        {
            var gm = SudokuGameManager.Instance;
            if (gm != null)
            {
                gm.OnMistakesUpdated -= HandleMistakesUpdated;
            }

            var inv = GameBoosterInventory.Instance;
            if (inv != null)
            {
                inv.OnInventoryChanged -= UpdateShieldAura;
                inv.OnHeartShieldAbsorbed -= HandleShieldAbsorbed;
            }
        }

        private void Update()
        {
            // Gentle floating heart bobbing
            float bob = Mathf.Sin(Time.time * 2.5f) * 0.003f;
            for (int i = 0; i < heartRoots.Length; i++)
            {
                if (heartRoots[i] != null && i < _currentLives)
                {
                    heartRoots[i].localPosition = new Vector3((i - 1) * 0.065f, bob, 0f);
                }
            }
        }

        private void HandleMistakesUpdated(int currentMistakes, int maxMistakes)
        {
            int remaining = Mathf.Clamp(maxMistakes - currentMistakes, 0, 3);
            if (remaining < _currentLives)
            {
                // Heart lost — trigger shatter
                int shatteredIndex = remaining;
                TriggerHeartShatter(shatteredIndex);
            }
            _currentLives = remaining;
            UpdateVisuals();
        }

        private void TriggerHeartShatter(int index)
        {
            if (index >= 0 && index < heartRoots.Length && heartRoots[index] != null)
            {
                // Squash and scale drop
                heartRoots[index].localScale = new Vector3(0.015f, 0.015f, 0.015f);
                TactileAudioService.Instance?.PlayConflict();
            }
        }

        private void HandleShieldAbsorbed()
        {
            // Flash cyan shield aura
            if (shieldAuraIndicator != null)
            {
                shieldAuraIndicator.SetActive(true);
                CancelInvoke(nameof(HideShieldAura));
                Invoke(nameof(HideShieldAura), 1.2f);
            }
        }

        private void HideShieldAura()
        {
            UpdateShieldAura();
        }

        private void UpdateShieldAura()
        {
            if (shieldAuraIndicator != null)
            {
                bool hasShield = GameBoosterInventory.Instance != null && GameBoosterInventory.Instance.HeartShields > 0;
                shieldAuraIndicator.SetActive(hasShield);
            }
        }

        public void UpdateVisuals()
        {
            for (int i = 0; i < heartRenderers.Length; i++)
            {
                if (heartRenderers[i] != null)
                {
                    bool isAlive = i < _currentLives;
                    Color col = isAlive ? heartAliveColor : heartCrackedColor;

                    heartRenderers[i].GetPropertyBlock(_propBlock);
                    _propBlock.SetColor("_BaseColor", col);
                    _propBlock.SetColor("_Color", col);
                    _propBlock.SetColor("_EmissionColor", isAlive ? col * 0.35f : Color.black);
                    heartRenderers[i].SetPropertyBlock(_propBlock);

                    heartRoots[i].localScale = isAlive ? new Vector3(0.035f, 0.035f, 0.025f) : new Vector3(0.02f, 0.02f, 0.015f);
                }
            }
        }

        private void BuildProceduralHeartTokens()
        {
            heartRoots = new Transform[3];
            heartRenderers = new MeshRenderer[3];

            Material heartMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            heartMat.color = heartAliveColor;
            heartMat.SetFloat("_Smoothness", 0.88f);
            heartMat.EnableKeyword("_EMISSION");

            for (int i = 0; i < 3; i++)
            {
                GameObject root = new GameObject($"Heart_Token_{i + 1}");
                root.transform.SetParent(transform, false);
                root.transform.localPosition = new Vector3((i - 1) * 0.065f, 0f, 0f);
                heartRoots[i] = root.transform;

                // Stylized heart volumetric capsule
                GameObject meshObj = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                meshObj.name = "Heart_Mesh";
                meshObj.transform.SetParent(root.transform, false);
                meshObj.transform.localScale = new Vector3(0.035f, 0.035f, 0.025f);
                Destroy(meshObj.GetComponent<Collider>());

                var mr = meshObj.GetComponent<MeshRenderer>();
                mr.sharedMaterial = heartMat;
                heartRenderers[i] = mr;
            }

            // Shield Aura Ring
            GameObject aura = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            aura.name = "Shield_Aura_Indicator";
            aura.transform.SetParent(transform, false);
            aura.transform.localPosition = new Vector3(0f, 0f, 0.01f);
            aura.transform.localScale = new Vector3(0.24f, 0.002f, 0.05f);
            Destroy(aura.GetComponent<Collider>());

            Material auraMat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            auraMat.color = shieldAuraColor;
            auraMat.EnableKeyword("_EMISSION");
            auraMat.SetColor("_EmissionColor", shieldAuraColor * 0.5f);
            aura.GetComponent<MeshRenderer>().sharedMaterial = auraMat;
            shieldAuraIndicator = aura;
            shieldAuraIndicator.SetActive(false);
        }
    }
}
