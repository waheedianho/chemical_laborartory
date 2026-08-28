using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab
{
    /// <summary>
    /// Attach to any grabbable vessel (flask, beaker, bottle).
    /// When tilted past pourAngleThreshold, liquid begins pouring into any
    /// ChemicalSolution within range that is positioned below the vessel.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    [RequireComponent(typeof(ChemicalSolution))]
    public class LiquidPourController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Pour Settings")]
        [Tooltip("Tilt angle (degrees from upright) at which pouring starts")]
        [SerializeField, Range(20f, 80f)] private float pourAngleThreshold = 40f;
        [Tooltip("mL per second transferred at maximum tilt (90°)")]
        [SerializeField] private float maxPourRateMlPerSec = 30f;
        [Tooltip("Radius to search for target vessels below the spout")]
        [SerializeField] private float pourDetectionRadius = 0.12f;

        [Header("Spout")]
        [Tooltip("Transform at the mouth of the vessel — liquid originates here")]
        [SerializeField] private Transform spoutTransform;

        [Header("VFX")]
        [SerializeField] private ParticleSystem pourParticlesPrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   pourLoopClip;
        [SerializeField] private AudioClip   dropSfx;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private ChemicalSolution myContents;
        private XRGrabInteractable grabInteractable;
        private ParticleSystem pourParticlesInstance;

        private bool  isPourActive  = false;
        private float pourAngle     = 0f;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            myContents        = GetComponent<ChemicalSolution>();
            grabInteractable  = GetComponent<XRGrabInteractable>();
        }

        private void Update()
        {
            // Only pour when grabbed
            if (!grabInteractable.isSelected)
            {
                if (isPourActive) StopPour();
                return;
            }

            // Measure tilt: angle between vessel's local up and world up
            pourAngle = Vector3.Angle(transform.up, Vector3.up);

            if (pourAngle >= pourAngleThreshold && !myContents.IsEmpty)
            {
                StartOrContinuePour();
            }
            else
            {
                if (isPourActive) StopPour();
            }
        }

        // ─── Pour logic ───────────────────────────────────────────────────────

        private void StartOrContinuePour()
        {
            if (!isPourActive)
            {
                isPourActive = true;
                StartPourVFX();
                StartPourAudio();
            }

            // Rate scales with tilt beyond threshold
            float tiltFraction = Mathf.InverseLerp(pourAngleThreshold, 90f, pourAngle);
            float mlThisFrame  = maxPourRateMlPerSec * tiltFraction * Time.deltaTime;

            // Find target vessel below the spout
            Vector3 spoutPos = spoutTransform != null ? spoutTransform.position : transform.position;
            ChemicalSolution target = FindTargetVessel(spoutPos);

            if (target != null && target != myContents)
            {
                ChemicalType chem = myContents.DominantChemical;
                float removed     = myContents.Pour(mlThisFrame);
                target.Receive(chem, removed);
            }
            else
            {
                // Pouring on the floor / into nothing — just drain
                myContents.Pour(mlThisFrame * 0.5f);
            }
        }

        private void StopPour()
        {
            isPourActive = false;
            StopPourVFX();
            StopPourAudio();
        }

        private ChemicalSolution FindTargetVessel(Vector3 origin)
        {
            Collider[] hits = Physics.OverlapSphere(origin, pourDetectionRadius);
            ChemicalSolution best   = null;
            float            bestDot = -1f;

            foreach (var col in hits)
            {
                if (col.gameObject == gameObject) continue;
                var sol = col.GetComponent<ChemicalSolution>();
                if (sol == null) sol = col.GetComponentInParent<ChemicalSolution>();
                if (sol == null) continue;

                // Prefer vessel that is below the spout (dot product with down)
                Vector3 dir = (col.transform.position - origin).normalized;
                float dot   = Vector3.Dot(dir, Vector3.down);
                if (dot > bestDot)
                {
                    bestDot = dot;
                    best    = sol;
                }
            }

            return best;
        }

        // ─── VFX & Audio ──────────────────────────────────────────────────────

        private void StartPourVFX()
        {
            if (pourParticlesPrefab == null) return;
            Vector3 pos = spoutTransform != null ? spoutTransform.position : transform.position;
            pourParticlesInstance = Instantiate(pourParticlesPrefab, pos, Quaternion.identity, transform);
            pourParticlesInstance.Play();
        }

        private void StopPourVFX()
        {
            if (pourParticlesInstance == null) return;
            pourParticlesInstance.Stop();
            Destroy(pourParticlesInstance.gameObject, 2f);
            pourParticlesInstance = null;
        }

        private void StartPourAudio()
        {
            if (audioSource == null || pourLoopClip == null) return;
            audioSource.clip   = pourLoopClip;
            audioSource.loop   = true;
            audioSource.Play();
        }

        private void StopPourAudio()
        {
            if (audioSource == null) return;
            audioSource.Stop();
            if (dropSfx != null) audioSource.PlayOneShot(dropSfx);
        }
    }
}
