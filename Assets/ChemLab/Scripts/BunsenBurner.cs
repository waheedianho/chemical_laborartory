using UnityEngine;
using UnityEngine.Events;

namespace ChemLab
{
    /// <summary>
    /// Bunsen burner — toggled on/off by Switch1/Switch2 or by direct grab+squeeze.
    /// When on, emits heat into a spherical zone; objects with HeatedObject component
    /// inside the zone receive temperature increases each frame.
    /// </summary>
    public class BunsenBurner : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Flame VFX")]
        [SerializeField] private ParticleSystem flameParticles;
        [SerializeField] private ParticleSystem heatHazeParticles;
        [SerializeField] private Light          flameLight;
        [SerializeField] private Transform      flameTip;

        [Header("Heat Zone")]
        [Tooltip("Radius around the tip that applies heat to objects")]
        [SerializeField] private float heatRadius = 0.35f;
        [Tooltip("Temperature increase per second at the flame tip (°C/s)")]
        [SerializeField] private float heatRateAtTip = 40f;
        [Tooltip("Layer mask for heated objects")]
        [SerializeField] private LayerMask heatedObjectsMask = ~0;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   igniteClip;
        [SerializeField] private AudioClip   burnLoopClip;
        [SerializeField] private AudioClip   extinguishClip;

        [Header("Flame Light Animation")]
        [SerializeField] private float flickerSpeed     = 15f;
        [SerializeField] private float flickerAmplitude = 0.08f;
        [SerializeField] private float baseIntensity    = 1.4f;

        [Header("Events")]
        public UnityEvent onIgnite;
        public UnityEvent onExtinguish;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private bool isOn = false;
        private float flickerOffset;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            flickerOffset = Random.Range(0f, 100f);
            if (flameTip == null) flameTip = transform.Find("FlameTip");
            SetBurnerState(false, silent: true);
        }

        private void Update()
        {
            if (!isOn) return;

            // Flicker light
            if (flameLight != null)
            {
                float noise = Mathf.PerlinNoise(Time.time * flickerSpeed, flickerOffset);
                flameLight.intensity = baseIntensity + (noise - 0.5f) * 2f * flickerAmplitude;
            }

            // Apply heat to objects in zone (using flame tip position)
            Vector3 heatOrigin = flameTip != null ? flameTip.position : transform.position;
            Collider[] hits = Physics.OverlapSphere(heatOrigin, heatRadius, heatedObjectsMask);
            foreach (var col in hits)
            {
                var heated = col.GetComponent<HeatedObject>()
                          ?? col.GetComponentInParent<HeatedObject>();

                if (heated != null)
                {
                    float dist    = Vector3.Distance(heatOrigin, col.transform.position);
                    float falloff = 1f - Mathf.Clamp01(dist / heatRadius);
                    heated.ApplyHeat(heatRateAtTip * falloff * Time.deltaTime);
                }
            }
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public bool IsOn => isOn;

        /// <summary>Toggle burner state. Called by SwitchController.</summary>
        public void Toggle() => SetBurnerState(!isOn);

        public void TurnOn()  => SetBurnerState(true);
        public void TurnOff() => SetBurnerState(false);

        // ─── Private ──────────────────────────────────────────────────────────

        private void SetBurnerState(bool on, bool silent = false)
        {
            isOn = on;

            if (flameParticles != null)
            {
                if (on) flameParticles.Play(true);
                else    flameParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (heatHazeParticles != null)
            {
                if (on) heatHazeParticles.Play();
                else    heatHazeParticles.Stop();
            }

            if (flameLight != null) flameLight.enabled = on;

            if (!silent && audioSource != null)
            {
                if (on)
                {
                    if (igniteClip != null) audioSource.PlayOneShot(igniteClip);
                    if (burnLoopClip != null)
                    {
                        audioSource.clip = burnLoopClip;
                        audioSource.loop = true;
                        audioSource.PlayDelayed(igniteClip != null ? igniteClip.length : 0f);
                    }
                }
                else
                {
                    audioSource.Stop();
                    if (extinguishClip != null) audioSource.PlayOneShot(extinguishClip);
                }
            }

            if (!silent)
            {
                if (on) onIgnite?.Invoke();
                else    onExtinguish?.Invoke();
            }
        }

        // ─── Gizmos ───────────────────────────────────────────────────────────
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = isOn ? new Color(1f, 0.4f, 0f, 0.3f) : new Color(0.4f, 0.4f, 0.4f, 0.2f);
            Gizmos.DrawSphere(transform.position, heatRadius);
        }
        #endif
    }
}
