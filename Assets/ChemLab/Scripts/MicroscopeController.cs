using TMPro;
using UnityEngine;

namespace ChemLab
{
    /// <summary>
    /// Microscope eyepiece interaction.
    /// When the player looks into the eyepiece (head enters trigger zone),
    /// a render texture camera activates showing a magnified slide view.
    /// Slides can be placed/swapped on the stage.
    /// </summary>
    public class MicroscopeController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Eyepiece")]
        [Tooltip("Trigger collider at the eyepiece that detects the player's head")]
        [SerializeField] private Collider   eyepieceTrigger;
        [Tooltip("Camera that renders the magnified view")]
        [SerializeField] private Camera     microscopeCamera;
        [Tooltip("RenderTexture the camera writes to")]
        [SerializeField] private RenderTexture viewRenderTexture;
        [Tooltip("Quad inside the eyepiece that shows the render texture")]
        [SerializeField] private Renderer   eyepieceQuad;

        [Header("Stage")]
        [Tooltip("Snap point for placing slides on the stage")]
        [SerializeField] private Transform  slideSnapPoint;
        [SerializeField] private float      slideSnapRadius = 0.05f;

        [Header("Focus Knob")]
        [Tooltip("XRKnob controlling the camera's focal length (blur simulation)")]
        [SerializeField] private Unity.VRTemplate.XRKnob focusKnob;
        [SerializeField] private float minFocalLength = 2f;
        [SerializeField] private float maxFocalLength = 8f;

        [Header("Slide Info")]
        [SerializeField] private TextMeshProUGUI slideNameText;
        [SerializeField] private TextMeshProUGUI slideMagnificationText;
        [SerializeField] private float           defaultMagnification = 400f;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   slideInsertClip;
        [SerializeField] private AudioClip   focusClickClip;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private bool isViewing     = false;
        private LabSlide currentSlide = null;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (microscopeCamera != null)
            {
                microscopeCamera.targetTexture = viewRenderTexture;
                microscopeCamera.enabled       = false;
            }
        }

        private void Update()
        {
            if (isViewing && focusKnob != null && microscopeCamera != null)
            {
                // Adjust focal length with focus knob
                float focalLen = Mathf.Lerp(minFocalLength, maxFocalLength, focusKnob.value);
                microscopeCamera.focalLength = focalLen;
            }

            // Try to snap slides
            TrySnapSlide();
        }

        // ─── Trigger (head enters eyepiece zone) ─────────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            // Only react to the XR camera / head
            if (!other.CompareTag("MainCamera") && !other.CompareTag("Player")) return;
            StartViewing();
        }

        private void OnTriggerExit(Collider other)
        {
            if (!other.CompareTag("MainCamera") && !other.CompareTag("Player")) return;
            StopViewing();
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void StartViewing()
        {
            isViewing = true;
            if (microscopeCamera != null) microscopeCamera.enabled = true;
            UpdateSlideInfo();
        }

        private void StopViewing()
        {
            isViewing = false;
            if (microscopeCamera != null) microscopeCamera.enabled = false;
        }

        private void TrySnapSlide()
        {
            if (slideSnapPoint == null) return;
            Collider[] hits = Physics.OverlapSphere(slideSnapPoint.position, slideSnapRadius);
            foreach (var col in hits)
            {
                var slide = col.GetComponent<LabSlide>();
                if (slide != null && slide != currentSlide)
                {
                    PlaceSlide(slide);
                }
            }
        }

        private void PlaceSlide(LabSlide slide)
        {
            // Eject previous slide
            if (currentSlide != null)
            {
                var prevRb = currentSlide.GetComponent<Rigidbody>();
                if (prevRb != null) prevRb.isKinematic = false;
            }

            currentSlide = slide;
            audioSource?.PlayOneShot(slideInsertClip);

            // Lock slide to stage
            var rb = slide.GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            slide.transform.position = slideSnapPoint.position;
            slide.transform.rotation = slideSnapPoint.rotation;

            // Switch render texture to this slide's texture
            if (microscopeCamera != null && slide.SlideTexture != null)
                microscopeCamera.backgroundColor = Color.black; // slide texture handled separately

            UpdateSlideInfo();
        }

        private void UpdateSlideInfo()
        {
            if (slideNameText != null)
                slideNameText.text = currentSlide != null ? currentSlide.SlideName : "No Slide";
            if (slideMagnificationText != null)
                slideMagnificationText.text = $"{defaultMagnification:F0}×";
        }
    }

    // ─── LabSlide data component ──────────────────────────────────────────────

    /// <summary>
    /// Simple data holder placed on microscope slide prefabs.
    /// </summary>
    public class LabSlide : MonoBehaviour
    {
        [SerializeField] private string      slideName    = "Unnamed Slide";
        [SerializeField] private Texture2D   slideTexture;
        [TextArea(2, 4)]
        [SerializeField] private string      slideNotes;

        public string    SlideName    => slideName;
        public Texture2D SlideTexture => slideTexture;
        public string    SlideNotes   => slideNotes;
    }
}
