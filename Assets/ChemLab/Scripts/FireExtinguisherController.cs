using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab.Scripts
{
    /// <summary>
    /// Fire extinguisher — grab it, point at fire, squeeze the trigger handle.
    /// Emits a CO₂ discharge particle stream. Proximity-triggered: if the
    /// stream hits a BunsenBurner, it extinguishes it.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class FireExtinguisherController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Discharge")]
        [SerializeField] private ParticleSystem co2StreamParticles;
        [SerializeField] private Transform      nozzleTransform;
        [Tooltip("Range of the discharge stream (m)")]
        [SerializeField] private float          streamRange    = 1.5f;
        [SerializeField] private float          streamRadius   = 0.08f;
        [Tooltip("How long the extinguisher lasts at full discharge (seconds)")]
        [SerializeField] private float          dischargeTime  = 30f;

        [Header("Handle / Trigger")]
        [Tooltip("The squeeze handle that the player grabs to activate")]
        [SerializeField] private XRSimpleInteractable handleInteractable;
        [SerializeField] private Transform            handlePivot;
        [SerializeField] private Vector3              handlePressedEulers = new(25f, 0f, 0f);
        [SerializeField] private Vector3              handleRestEulers    = Vector3.zero;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   dischargeClip;
        [SerializeField] private AudioClip   pinPullClip;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private XRGrabInteractable grabInteractable;
        private bool   isGrabbed      = false;
        private bool   isDischarging  = false;
        private float  remainingCharge;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            grabInteractable.selectEntered.AddListener(_ => { isGrabbed = true; });
            grabInteractable.selectExited.AddListener(_ =>  { isGrabbed = false; StopDischarge(); });

            if (handleInteractable != null)
            {
                handleInteractable.selectEntered.AddListener(_ => StartDischarge());
                handleInteractable.selectExited.AddListener(_ =>  StopDischarge());
            }

            remainingCharge = dischargeTime;
            if (co2StreamParticles != null) co2StreamParticles.Stop();
        }

        private void Update()
        {
            if (!isDischarging) return;

            remainingCharge -= Time.deltaTime;
            if (remainingCharge <= 0f)
            {
                StopDischarge();
                return;
            }

            // Raycast the stream to find and extinguish burners
            Vector3 origin    = nozzleTransform != null ? nozzleTransform.position : transform.position;
            Vector3 direction = nozzleTransform != null ? nozzleTransform.forward  : transform.forward;

            Collider[] hits = Physics.OverlapCapsule(origin,
                                                     origin + direction * streamRange,
                                                     streamRadius);
            foreach (var col in hits)
            {
                var burner = col.GetComponent<BunsenBurner>()
                          ?? col.GetComponentInParent<BunsenBurner>();
                if (burner != null && burner.IsOn)
                    burner.TurnOff();
            }
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void StartDischarge()
        {
            if (!isGrabbed || remainingCharge <= 0f) return;

            isDischarging = true;
            co2StreamParticles?.Play();

            if (audioSource != null && dischargeClip != null)
            {
                audioSource.clip = dischargeClip;
                audioSource.loop = true;
                audioSource.Play();
            }

            if (handlePivot != null)
                handlePivot.localEulerAngles = handlePressedEulers;
        }

        private void StopDischarge()
        {
            isDischarging = false;
            co2StreamParticles?.Stop();
            audioSource?.Stop();

            if (handlePivot != null)
                handlePivot.localEulerAngles = handleRestEulers;
        }
    }
}
