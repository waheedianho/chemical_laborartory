using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab.Scripts
{
    /// <summary>
    /// Microcentrifuge controller.
    ///   – Lid opens/closes via XRSimpleInteractable hover + grab
    ///   – Test tube snaps into the rotor slot
    ///   – Start button triggers spin animation + audio
    ///   – After spin, solution shows separated layer via ChemicalSolution tag
    /// </summary>
    public class CentrifugeController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Lid")]
        [SerializeField] private Transform      lidTransform;
        [SerializeField] private Vector3        lidOpenEulers  = new(80f, 0f, 0f);
        [SerializeField] private Vector3        lidCloseEulers = Vector3.zero;
        [SerializeField] private float          lidAnimTime    = 0.4f;

        [Header("Rotor")]
        [SerializeField] private Transform      rotorTransform;
        [Tooltip("Snap point where the tube locks in")]
        [SerializeField] private Transform      tubeSnapPoint;
        [SerializeField] private float          snapRadius     = 0.06f;

        [Header("Spin Settings")]
        [SerializeField] private float          maxRPM         = 12000f;
        [SerializeField] private float          spinDuration   = 8f;    // seconds
        [SerializeField] private float          rampUpTime     = 2f;
        [SerializeField] private float          rampDownTime   = 2f;

        [Header("Start / Stop Buttons")]
        [SerializeField] private XRSimpleInteractable startButton;
        [SerializeField] private XRSimpleInteractable stopButton;

        [Header("Audio")]
        [SerializeField] private AudioSource    audioSource;
        [SerializeField] private AudioClip      motorStartClip;
        [SerializeField] private AudioClip      motorLoopClip;
        [SerializeField] private AudioClip      motorStopClip;
        [SerializeField] private AudioClip      lidOpenClip;
        [SerializeField] private AudioClip      lidCloseClip;

        [Header("Events")]
        public UnityEvent onSpinComplete;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private bool     lidIsOpen      = false;
        private bool     isSpinning     = false;
        private float    currentRPM     = 0f;
        private GameObject tubeInSlot   = null;
        private Coroutine spinCoroutine;
        private Coroutine lidCoroutine;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (startButton != null)
                startButton.selectEntered.AddListener(_ => TryStartSpin());
            if (stopButton != null)
                stopButton.selectEntered.AddListener(_ => StopSpin());
        }

        private void Update()
        {
            // Rotate the rotor mesh
            if (rotorTransform != null && currentRPM > 0.1f)
            {
                rotorTransform.Rotate(0f, currentRPM / 60f * 360f * Time.deltaTime, 0f,
                                      Space.Self);
            }

            // Auto-snap loose tubes into the slot
            if (!isSpinning && lidIsOpen && tubeSnapPoint != null)
                TrySnapTube();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void ToggleLid()
        {
            if (isSpinning) return;   // can't open while spinning

            lidIsOpen = !lidIsOpen;
            if (lidCoroutine != null) StopCoroutine(lidCoroutine);
            lidCoroutine = StartCoroutine(AnimateLid(
                lidIsOpen ? lidOpenEulers : lidCloseEulers));

            audioSource?.PlayOneShot(lidIsOpen ? lidOpenClip : lidCloseClip);
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void TryStartSpin()
        {
            if (isSpinning || lidIsOpen) return;
            spinCoroutine = StartCoroutine(SpinCycle());
        }

        private void StopSpin()
        {
            if (!isSpinning) return;
            if (spinCoroutine != null) StopCoroutine(spinCoroutine);
            StartCoroutine(RampDown());
        }

        private IEnumerator SpinCycle()
        {
            isSpinning = true;
            audioSource?.PlayOneShot(motorStartClip);

            // Ramp up
            float t = 0f;
            while (t < rampUpTime)
            {
                t += Time.deltaTime;
                currentRPM = Mathf.Lerp(0f, maxRPM, t / rampUpTime);
                if (audioSource != null && motorLoopClip != null && !audioSource.isPlaying)
                {
                    audioSource.clip = motorLoopClip;
                    audioSource.loop = true;
                    audioSource.Play();
                }
                yield return null;
            }
            currentRPM = maxRPM;

            // Hold at speed
            yield return new WaitForSeconds(spinDuration - rampUpTime - rampDownTime);

            // Ramp down
            yield return RampDown();

            // Mark tube as centrifuged
            if (tubeInSlot != null)
            {
                var sol = tubeInSlot.GetComponent<ChemicalSolution>()
                       ?? tubeInSlot.GetComponentInParent<ChemicalSolution>();
                // Could set a "centrifuged" bool on a custom component here
            }

            onSpinComplete?.Invoke();
        }

        private IEnumerator RampDown()
        {
            float startRPM = currentRPM;
            float t = 0f;
            while (t < rampDownTime)
            {
                t += Time.deltaTime;
                currentRPM = Mathf.Lerp(startRPM, 0f, t / rampDownTime);
                yield return null;
            }
            currentRPM = 0f;
            isSpinning = false;

            audioSource?.Stop();
            audioSource?.PlayOneShot(motorStopClip);
        }

        private void TrySnapTube()
        {
            Collider[] hits = Physics.OverlapSphere(tubeSnapPoint.position, snapRadius);
            foreach (var col in hits)
            {
                var sol = col.GetComponent<ChemicalSolution>()
                       ?? col.GetComponentInParent<ChemicalSolution>();
                if (sol != null && col.gameObject != gameObject)
                {
                    tubeInSlot = col.gameObject;
                    // Snap position/rotation
                    var rb = col.GetComponent<Rigidbody>();
                    if (rb != null) rb.isKinematic = true;
                    col.transform.position = tubeSnapPoint.position;
                    col.transform.rotation = tubeSnapPoint.rotation;
                    break;
                }
            }
        }

        private IEnumerator AnimateLid(Vector3 targetEulers)
        {
            Quaternion start  = lidTransform.localRotation;
            Quaternion target = Quaternion.Euler(targetEulers);
            float t = 0f;
            while (t < lidAnimTime)
            {
                t += Time.deltaTime;
                lidTransform.localRotation = Quaternion.Slerp(start, target,
                                                               Mathf.SmoothStep(0f, 1f, t / lidAnimTime));
                yield return null;
            }
            lidTransform.localRotation = target;
        }
    }
}
