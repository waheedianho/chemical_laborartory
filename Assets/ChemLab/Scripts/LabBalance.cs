using System.Collections;
using TMPro;
using UnityEngine;

namespace ChemLab.Scripts
{
    /// <summary>
    /// Attach to the weighing pan's trigger zone on any WeightMeter prop.
    /// When an object enters the pan trigger, the balance reads its mass,
    /// plays a settle animation and shows a TMP readout.
    /// </summary>
    public class LabBalance : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The pan object that wobbles on placement")]
        [SerializeField] private Transform panTransform;
        [Tooltip("Text element showing the mass reading")]
        [SerializeField] private TextMeshProUGUI massReadoutText;
        [Tooltip("Text element showing status (READY / TARE / etc.)")]
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Settle Animation")]
        [SerializeField] private float settleAmplitude  = 0.008f;
        [SerializeField] private float settleFrequency  = 12f;
        [SerializeField] private float settleDampening  = 4f;

        [Header("Display")]
        [Tooltip("Drift in reading ± this value to simulate imprecision")]
        [SerializeField] private float readingNoise     = 0.02f;
        [SerializeField] private string readingFormat   = "F2";   // "0.00 g"

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   placeSfx;
        [SerializeField] private AudioClip   removeSfx;
        [SerializeField] private AudioClip   beepSfx;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private float  totalMassOnPan   = 0f;
        private int    objectsOnPan     = 0;
        private Vector3 panRestPosition;
        private Coroutine settleCoroutine;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (panTransform != null) panRestPosition = panTransform.localPosition;
            SetDisplay(0f, "READY");
        }

        // ─── Trigger detection ────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // Ignore if the collider belongs to the balance itself
            if (other.transform.IsChildOf(transform)) return;

            float mass = EstimateMass(other.gameObject);
            totalMassOnPan += mass;
            objectsOnPan++;

            audioSource?.PlayOneShot(placeSfx);
            TriggerSettle();
            StartCoroutine(ShowReadingAfterSettle(totalMassOnPan));
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.transform.IsChildOf(transform)) return;

            float mass = EstimateMass(other.gameObject);
            totalMassOnPan = Mathf.Max(0f, totalMassOnPan - mass);
            objectsOnPan   = Mathf.Max(0, objectsOnPan - 1);

            audioSource?.PlayOneShot(removeSfx);

            if (objectsOnPan == 0)
            {
                SetDisplay(0f, "READY");
                TriggerSettle();
            }
            else
            {
                TriggerSettle();
                StartCoroutine(ShowReadingAfterSettle(totalMassOnPan));
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private float EstimateMass(GameObject go)
        {
            // Try to get mass from Rigidbody
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null && !rb.isKinematic) return rb.mass * 1000f; // kg → g

            // Estimate from collider volume × a density factor
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Vector3 size   = col.bounds.size;
                float   volume = size.x * size.y * size.z; // m³ (rough)
                return volume * 800000f; // ~800 kg/m³ density → grams
            }

            return 10f; // default fallback 10g
        }

        private IEnumerator ShowReadingAfterSettle(float mass)
        {
            SetDisplay(-1f, "MEASURING…");
            yield return new WaitForSeconds(1.8f);   // settle time

            // Add tiny noise for realism
            float display = mass + Random.Range(-readingNoise, readingNoise);
            SetDisplay(display, "OK");
            audioSource?.PlayOneShot(beepSfx);
        }

        private void SetDisplay(float grams, string status)
        {
            if (massReadoutText != null)
                massReadoutText.text = grams >= 0f ? $"{grams.ToString(readingFormat)} g" : "- - - -";
            if (statusText != null)
                statusText.text = status;
        }

        private void TriggerSettle()
        {
            if (settleCoroutine != null) StopCoroutine(settleCoroutine);
            if (panTransform != null)
                settleCoroutine = StartCoroutine(SettleAnimation());
        }

        private IEnumerator SettleAnimation()
        {
            float elapsed = 0f;
            float duration = 2f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float dampFactor = Mathf.Exp(-settleDampening * elapsed);
                float offset     = Mathf.Sin(settleFrequency * elapsed * Mathf.PI * 2f)
                                   * settleAmplitude * dampFactor;
                panTransform.localPosition = panRestPosition + new Vector3(0f, offset, 0f);
                yield return null;
            }

            panTransform.localPosition = panRestPosition;
        }
    }
}
