using UnityEngine;
using UnityEngine.Events;

namespace ChemLab
{
    /// <summary>
    /// Tracks the temperature of any lab object (flask, beaker, rod).
    /// When heat is applied by BunsenBurner, material emission changes to
    /// simulate a glowing hot object. Integrates with ChemicalSolution to
    /// raise the solution temperature.
    /// </summary>
    public class HeatedObject : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Temperature")]
        [SerializeField] private float ambientTemperature = 20f;
        [SerializeField] private float coolingRatePerSec  = 2f;
        [Tooltip("Temperature at which the object starts glowing visually")]
        [SerializeField] private float glowStartTemp = 80f;
        [Tooltip("Temperature at which the object reaches maximum glow")]
        [SerializeField] private float glowMaxTemp   = 200f;

        [Header("Material Glow")]
        [Tooltip("Renderers whose emission will be tinted on heating")]
        [SerializeField] private Renderer[] glowRenderers;
        [SerializeField] private string     emissionColorProperty = "_EmissionColor";
        [SerializeField] private Color      coolColor  = Color.black;
        [SerializeField] private Color      hotColor   = new Color(1f, 0.35f, 0f);

        [Header("Steam VFX")]
        [SerializeField] private ParticleSystem steamParticles;
        [Tooltip("Temperature at which steam particles appear")]
        [SerializeField] private float steamStartTemp = 90f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   sizzleClip;
        [SerializeField] private AudioClip   boilClip;

        [Header("Events")]
        public UnityEvent<float> onTemperatureChanged;  // current temp
        public UnityEvent        onBoiling;             // fired at 100 °C
        public UnityEvent        onDangerouslyHot;      // fired at 150 °C

        // ─── Runtime ─────────────────────────────────────────────────────────

        private float temperature;
        private bool  boilingEventFired      = false;
        private bool  dangerousEventFired    = false;
        private bool  steamActive            = false;
        private ChemicalSolution linkedSolution;

        public float Temperature => temperature;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            temperature    = ambientTemperature;
            linkedSolution = GetComponent<ChemicalSolution>();
            UpdateVisuals();
        }

        private void Update()
        {
            // Passive cooling toward ambient
            if (temperature > ambientTemperature)
            {
                temperature = Mathf.MoveTowards(temperature, ambientTemperature,
                                                coolingRatePerSec * Time.deltaTime);
                UpdateVisuals();
                onTemperatureChanged?.Invoke(temperature);
            }

            // Sync with solution component
            if (linkedSolution != null)
                linkedSolution.AddTemperature((temperature - linkedSolution.Temperature) * 0.1f * Time.deltaTime);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>Called by BunsenBurner each frame with delta degrees.</summary>
        public void ApplyHeat(float deltaDegrees)
        {
            temperature += deltaDegrees;

            // Fire one-shot events
            if (!boilingEventFired && temperature >= 100f)
            {
                boilingEventFired = true;
                onBoiling?.Invoke();
                audioSource?.PlayOneShot(boilClip);
            }
            if (!dangerousEventFired && temperature >= 150f)
            {
                dangerousEventFired = true;
                onDangerouslyHot?.Invoke();
            }
            if (temperature < 98f) boilingEventFired = false;
            if (temperature < 148f) dangerousEventFired = false;

            UpdateVisuals();
            onTemperatureChanged?.Invoke(temperature);
        }

        // ─── Visuals ──────────────────────────────────────────────────────────

        private void UpdateVisuals()
        {
            float glowT = Mathf.InverseLerp(glowStartTemp, glowMaxTemp, temperature);

            // Material emission
            foreach (var rend in glowRenderers)
            {
                if (rend == null) continue;
                Color emissionColor = Color.Lerp(coolColor, hotColor, glowT) * Mathf.Pow(glowT, 1.5f) * 3f;
                rend.material.SetColor(emissionColorProperty, emissionColor);
            }

            // Steam particles
            if (steamParticles != null)
            {
                bool shouldSteam = temperature >= steamStartTemp;
                if (shouldSteam && !steamActive)
                {
                    steamParticles.Play();
                    steamActive = true;
                    audioSource?.PlayOneShot(sizzleClip);
                }
                else if (!shouldSteam && steamActive)
                {
                    steamParticles.Stop();
                    steamActive = false;
                }

                if (steamActive)
                {
                    float steamT = Mathf.InverseLerp(steamStartTemp, glowMaxTemp, temperature);
                    var emission = steamParticles.emission;
                    emission.rateOverTimeMultiplier = Mathf.Lerp(2f, 20f, steamT);
                }
            }
        }
    }
}
