using System;
using UnityEngine;

namespace SudokuVR.Core
{
    /// <summary>
    /// Premium procedural and spatial audio synthesizer for Sudoku Zenith MR.
    /// Provides rich acoustic feedback (ceramic taps, pentatonic musical placement,
    /// soft eraser swooshes, conflict warnings, and victory fanfare) with zero external asset dependencies.
    /// Supports Meta Quest controller haptic impulses.
    /// </summary>
    public class TactileAudioService : MonoBehaviour
    {
        public static TactileAudioService Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource fanfareSource;

        [Header("Volume Controls")]
        [Range(0f, 1f)] [SerializeField] private float masterVolume = 0.85f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 0.9f;
        [SerializeField] private bool hapticsEnabled = true;

        // Procedural Audio Clips
        private AudioClip _clipTapCeramic;
        private AudioClip[] _clipsDigitPlacements; // 9 pentatonic tones for digits 1-9
        private AudioClip _clipErase;
        private AudioClip _clipConflict;
        private AudioClip _clipVictory;
        private AudioClip _clipHint;

        private const int SampleRate = 44100;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }
            else if (Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0.2f; // Slight spatial depth
            }

            if (fanfareSource == null)
            {
                fanfareSource = gameObject.AddComponent<AudioSource>();
                fanfareSource.playOnAwake = false;
                fanfareSource.spatialBlend = 0f; // 2D immersion for victory
            }

            GenerateProceduralAudioClips();
        }

        private void GenerateProceduralAudioClips()
        {
            _clipTapCeramic = GenerateCeramicTap();
            _clipErase = GenerateEraseBrush();
            _clipConflict = GenerateConflictWarning();
            _clipVictory = GenerateVictoryFanfare();
            _clipHint = GenerateHintTwinkle();

            // 9 musical pentatonic marimba tones (C4, D4, E4, G4, A4, C5, D5, E5, G5)
            float[] pentatonicFrequencies = { 261.63f, 293.66f, 329.63f, 392.00f, 440.00f, 523.25f, 587.33f, 659.25f, 783.99f };
            _clipsDigitPlacements = new AudioClip[9];
            for (int i = 0; i < 9; i++)
            {
                _clipsDigitPlacements[i] = GenerateResonantMarimbaTone(pentatonicFrequencies[i], $"Digit_{i + 1}");
            }
        }

        #region Public Playback API

        public void PlayTap()
        {
            PlayClip(_clipTapCeramic, 0.45f);
            TriggerHaptic(0.2f, 0.05f);
        }

        public void PlayDigitPlaced(int digit)
        {
            int index = Mathf.Clamp(digit - 1, 0, 8);
            if (_clipsDigitPlacements != null && index < _clipsDigitPlacements.Length)
            {
                PlayClip(_clipsDigitPlacements[index], 0.7f);
            }
            TriggerHaptic(0.35f, 0.08f);
        }

        public void PlayErase()
        {
            PlayClip(_clipErase, 0.5f);
            TriggerHaptic(0.15f, 0.06f);
        }

        public void PlayConflict()
        {
            PlayClip(_clipConflict, 0.65f);
            TriggerHaptic(0.6f, 0.18f);
        }

        public void PlayHint()
        {
            PlayClip(_clipHint, 0.6f);
            TriggerHaptic(0.25f, 0.1f);
        }

        public void PlayVictoryFanfare()
        {
            if (fanfareSource != null && _clipVictory != null)
            {
                fanfareSource.volume = masterVolume * sfxVolume;
                fanfareSource.clip = _clipVictory;
                fanfareSource.Play();
            }
            TriggerHaptic(0.5f, 0.4f);
        }

        private void PlayClip(AudioClip clip, float volumeScale)
        {
            if (sfxSource != null && clip != null)
            {
                sfxSource.PlayOneShot(clip, masterVolume * sfxVolume * volumeScale);
            }
        }

        private void TriggerHaptic(float amplitude, float duration)
        {
            if (!hapticsEnabled) return;
            OVRInput.SetControllerVibration(amplitude, amplitude, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(amplitude, amplitude, OVRInput.Controller.LTouch);
            CancelInvoke(nameof(StopHaptics));
            Invoke(nameof(StopHaptics), duration);
        }

        private void StopHaptics()
        {
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.RTouch);
            OVRInput.SetControllerVibration(0, 0, OVRInput.Controller.LTouch);
        }

        #endregion

        #region Procedural Acoustic Synthesis Algorithms

        private AudioClip GenerateCeramicTap()
        {
            int samples = (int)(SampleRate * 0.06f); // 60ms
            float[] data = new float[samples];
            float freq1 = 2800f;
            float freq2 = 1420f;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 90f); // Fast percussive decay
                float wave = Mathf.Sin(2f * Mathf.PI * freq1 * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * freq2 * t) * 0.4f;
                data[i] = wave * env;
            }

            AudioClip clip = AudioClip.Create("TapCeramic", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateResonantMarimbaTone(float fundamentalFreq, string name)
        {
            int samples = (int)(SampleRate * 0.28f); // 280ms
            float[] data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 14f); // Warm natural decay
                float fundamental = Mathf.Sin(2f * Mathf.PI * fundamentalFreq * t);
                float harmonic2 = Mathf.Sin(2f * Mathf.PI * (fundamentalFreq * 2.75f) * t) * 0.25f;
                float harmonic3 = Mathf.Sin(2f * Mathf.PI * (fundamentalFreq * 4f) * t) * 0.1f;
                data[i] = (fundamental + harmonic2 + harmonic3) * env * 0.7f;
            }

            AudioClip clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateEraseBrush()
        {
            int samples = (int)(SampleRate * 0.08f); // 80ms
            float[] data = new float[samples];
            var rng = new System.Random(42);

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Sin(Mathf.PI * (t / 0.08f)) * Mathf.Exp(-t * 30f);
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = noise * env * 0.4f;
            }

            AudioClip clip = AudioClip.Create("EraseBrush", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateConflictWarning()
        {
            int samples = (int)(SampleRate * 0.22f); // 220ms
            float[] data = new float[samples];
            float f1 = 160f;
            float f2 = 196f; // Dissonant minor interval

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float env = Mathf.Exp(-t * 18f);
                float wave = (Mathf.Sin(2f * Mathf.PI * f1 * t) + Mathf.Sin(2f * Mathf.PI * f2 * t)) * 0.5f;
                data[i] = wave * env * 0.6f;
            }

            AudioClip clip = AudioClip.Create("ConflictWarning", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateHintTwinkle()
        {
            int samples = (int)(SampleRate * 0.35f); // 350ms
            float[] data = new float[samples];
            float[] chimeFreqs = { 1046.50f, 1318.51f, 1567.98f, 2093.00f }; // C6, E6, G6, C7

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float sum = 0f;
                for (int c = 0; c < chimeFreqs.Length; c++)
                {
                    float noteOffset = c * 0.06f;
                    if (t >= noteOffset)
                    {
                        float noteT = t - noteOffset;
                        float env = Mathf.Exp(-noteT * 22f);
                        sum += Mathf.Sin(2f * Mathf.PI * chimeFreqs[c] * noteT) * env * 0.25f;
                    }
                }
                data[i] = sum;
            }

            AudioClip clip = AudioClip.Create("HintTwinkle", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private AudioClip GenerateVictoryFanfare()
        {
            int samples = (int)(SampleRate * 1.2f); // 1.2s triumphant fanfare
            float[] data = new float[samples];
            // Triumphant rising arpeggio: C5, E5, G5, C6 (extended hold)
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.50f };
            float[] durations = { 0.16f, 0.16f, 0.16f, 0.72f };

            float runningTime = 0f;
            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = runningTime;
                float noteDuration = durations[n];
                float noteFreq = notes[n];
                runningTime += (n < 3 ? 0.14f : 0.72f);

                for (int i = 0; i < samples; i++)
                {
                    float t = (float)i / SampleRate;
                    if (t >= noteStart && t < noteStart + noteDuration)
                    {
                        float localT = t - noteStart;
                        float env = (n == 3) ? Mathf.Exp(-localT * 3.5f) : Mathf.Exp(-localT * 8f);
                        float fundamental = Mathf.Sin(2f * Mathf.PI * noteFreq * localT);
                        float shimmer = Mathf.Sin(2f * Mathf.PI * (noteFreq * 2f) * localT) * 0.3f;
                        data[i] += (fundamental + shimmer) * env * 0.35f;
                    }
                }
            }

            AudioClip clip = AudioClip.Create("VictoryFanfare", samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        #endregion
    }
}
