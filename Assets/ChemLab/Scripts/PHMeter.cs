using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab
{
    /// <summary>
    /// pH meter probe. When grabbed and dipped into a vessel (trigger overlap),
    /// reads the solution's pH, shows it on the TMP display, and changes the
    /// probe tip's material color to match the pH color scale.
    /// </summary>
    // [RequireComponent(typeof(XRGrabInteractable))]
    public class PHMeter : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Display")]
        [SerializeField] private TextMeshProUGUI phReadoutText;
        [SerializeField] private TextMeshProUGUI descriptionText;

        [Header("Probe Tip")]
        [SerializeField] private Renderer probeTipRenderer;
        [SerializeField] private string   tipColorProperty  = "_BaseColor";

        [Header("pH Color Gradient")]
        [Tooltip("Gradient from pH 0 (left) to pH 14 (right)")]
        [SerializeField] private Gradient phColorGradient;

        [Header("Detection")]
        [Tooltip("Trigger collider at the probe tip")]
        [SerializeField] private Collider probeTipCollider;
        [SerializeField] private float    readingInterval   = 0.5f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   dipSfx;
        [SerializeField] private AudioClip   beepSfx;

        // ─── Runtime ─────────────────────────────────────────────────────────

        // private XRGrabInteractable grabInteractable;
        private ChemicalSolution   currentSolution;
        private float              lastReadingTime;
        private float              displayedPH = 7f;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            // grabInteractable = GetComponent<XRGrabInteractable>();

            // Default gradient if not set
            if (phColorGradient == null || phColorGradient.colorKeys.Length == 0)
            {
                phColorGradient = new Gradient();
                phColorGradient.SetKeys(new GradientColorKey[]
                {
                    new(new Color(1f, 0f, 0f),    0.00f),  // pH  0 – red
                    new(new Color(1f, 0.5f, 0f),  0.14f),  // pH  2 – orange
                    new(new Color(1f, 1f, 0f),    0.29f),  // pH  4 – yellow
                    new(new Color(0.5f, 1f, 0f),  0.43f),  // pH  6 – yellow-green
                    new(new Color(0f, 0.8f, 0f),  0.50f),  // pH  7 – green
                    new(new Color(0f, 0.6f, 1f),  0.64f),  // pH  9 – teal
                    new(new Color(0f, 0f, 1f),    0.79f),  // pH 11 – blue
                    new(new Color(0.5f, 0f, 0.5f),1.00f),  // pH 14 – violet
                }, new GradientAlphaKey[] {
                    new(1f, 0f), new(1f, 1f)
                });
            }

            SetDisplay(7f, "Not in solution");
        }

        private void Update()
        {
            // Debug.Log("The current solution" + currentSolution);
            if (currentSolution == null) return;
            if (Time.time - lastReadingTime < readingInterval) return;

            lastReadingTime = Time.time;
            float ph = currentSolution.PH;

            if (Mathf.Abs(ph - displayedPH) > 0.05f)
            {
                displayedPH = Mathf.MoveTowards(displayedPH, ph, 0.3f);
                SetDisplay(displayedPH, GetPHDescription(displayedPH));
                audioSource?.PlayOneShot(beepSfx);
            }
        }

        // ─── Trigger ─────────────────────────────────────────────────────────

        private void OnTriggerEnter(Collider other)
        {   
            Debug.Log($"Trigger collider identify {other.gameObject.name}");
            var sol = other.GetComponent<ChemicalSolution>()
                   ?? other.GetComponentInParent<ChemicalSolution>();
            if (sol == null) return;

            // Only trigger on the dedicated liquid surface trigger.
            // If one isn't set up yet, fallback to any trigger, but NEVER the solid glass collider.
            if (sol.LiquidTrigger != null)
            {
                if (other != sol.LiquidTrigger) return;
            }
            else if (!other.isTrigger)
            {
                return;
            }

            currentSolution = sol;
            audioSource?.PlayOneShot(dipSfx);
        }

        private void OnTriggerExit(Collider other)
        {
            Debug.Log($"Trigger collider existing {other.gameObject.name}");
            var sol = other.GetComponent<ChemicalSolution>()
                   ?? other.GetComponentInParent<ChemicalSolution>();
            if (sol == null) return;

            if (sol.LiquidTrigger != null)
            {
                if (other != sol.LiquidTrigger) return;
            }
            else if (!other.isTrigger)
            {
                return;
            }

            if (sol == currentSolution)
            {
                currentSolution = null;
                SetDisplay(displayedPH, "Not in solution");
            }
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private void SetDisplay(float ph, string description)
        {
            // Clamp and display
            ph = Mathf.Clamp(ph, 0f, 14f);
            if (phReadoutText != null)    phReadoutText.text    = $"pH {ph:F1}";
            if (descriptionText != null)  descriptionText.text  = description;

            // Tip color
            float t = ph / 14f;
            Color tipColor = phColorGradient.Evaluate(t);
            if (probeTipRenderer != null && probeTipRenderer.material.HasProperty(tipColorProperty))
                probeTipRenderer.material.SetColor(tipColorProperty, tipColor);
        }

        private static string GetPHDescription(float ph)
        {
            if (ph < 2f)  return "Strongly Acidic";
            if (ph < 5f)  return "Acidic";
            if (ph < 6.5f)return "Weakly Acidic";
            if (ph < 7.5f)return "Neutral";
            if (ph < 9f)  return "Weakly Basic";
            if (ph < 12f) return "Basic";
            return "Strongly Basic";
        }
    }
}
