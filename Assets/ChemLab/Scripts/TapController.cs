using Unity.VRTemplate;
using UnityEngine;

// XRKnob namespace

namespace ChemLab
{
    /// <summary>
    /// Controls the lab water tap / faucet.
    /// Reads the XRKnob value [0–1] and drives a water stream particle system.
    /// Any ChemicalSolution held directly below the stream receives Water.
    /// </summary>
    [RequireComponent(typeof(XRKnob))]
    public class TapController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Stream VFX")]
        [SerializeField] private ParticleSystem waterStreamParticles;
        [SerializeField] private Transform       streamOrigin;

        [Header("Fill Settings")]
        [Tooltip("Max mL/s of water delivered at knob = 1")]
        [SerializeField] private float maxFillRateMlPerSec = 50f;
        [Tooltip("Detection radius for vessels under the tap")]
        [SerializeField] private float fillDetectionRadius  = 0.15f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   waterLoopClip;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private XRKnob knob;
        private bool   isFlowing  = false;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            knob = GetComponent<XRKnob>();
            if (waterStreamParticles != null) waterStreamParticles.Stop();
        }

        private void Update()
        {
            float flowValue = knob.value;   // 0 = off, 1 = full

            // Start / stop VFX
            if (flowValue > 0.02f && !isFlowing)
                StartFlow();
            else if (flowValue <= 0.02f && isFlowing)
                StopFlow();

            if (!isFlowing) return;

            // Scale particle emission rate with knob value
            SetParticleRate(flowValue);

            // Fill any vessel under the stream
            Vector3 origin = streamOrigin != null ? streamOrigin.position : transform.position;
            Collider[] hits = Physics.OverlapSphere(origin + Vector3.down * 0.05f, fillDetectionRadius);

            float mlThisFrame = maxFillRateMlPerSec * flowValue * Time.deltaTime;

            foreach (var col in hits)
            {
                var sol = col.GetComponent<ChemicalSolution>()
                       ?? col.GetComponentInParent<ChemicalSolution>();
                if (sol != null)
                {
                    sol.Receive(ChemicalType.Water, mlThisFrame);
                    break;
                }
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void StartFlow()
        {
            isFlowing = true;
            waterStreamParticles?.Play();

            if (audioSource != null && waterLoopClip != null)
            {
                audioSource.clip = waterLoopClip;
                audioSource.loop = true;
                audioSource.Play();
            }
        }

        private void StopFlow()
        {
            isFlowing = false;
            waterStreamParticles?.Stop();
            audioSource?.Stop();
        }

        private void SetParticleRate(float value)
        {
            if (waterStreamParticles == null) return;
            var emission = waterStreamParticles.emission;
            emission.rateOverTimeMultiplier = value * 80f;
        }
    }
}
