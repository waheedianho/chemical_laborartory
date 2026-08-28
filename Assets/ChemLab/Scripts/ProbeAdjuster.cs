using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ChemLab
{
    /// <summary>
    /// Attach to the clamp/adjuster that holds the pH probe on the stand rod.
    /// Lets the player grab the clamp and slide it vertically (Y-axis only)
    /// within a defined range, dragging the probe up and down with it.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class ProbeAdjuster : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Slide Limits (Local Y, relative to rod base)")]
        [SerializeField] private float minY =  0.02f;   // lowest position
        [SerializeField] private float maxY =  0.25f;   // highest position

        [Header("References")]
        [Tooltip("The rod/stand transform — slide is along its local Y axis")]
        [SerializeField] private Transform rodTransform;
        [Tooltip("The probe (and clamp) that moves with the adjuster")]
        [SerializeField] private Transform probeAndClamp;

        [Header("Feel")]
        [SerializeField] private float snapIncrement = 0f;   // 0 = smooth, >0 = snaps in steps
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   slideClip;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private XRSimpleInteractable interactable;
        private IXRSelectInteractor  currentInteractor;
        private bool                 isHeld;
        private float                grabOffsetY;       // world-Y difference at grab time
        private float                lastPlayedY;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
        }

        private void Update()
        {
            if (!isHeld || currentInteractor == null) return;

            // Get the interactor's world Y position
            float interactorWorldY = currentInteractor.GetAttachTransform(interactable).position.y;

            // Convert to rod local space Y (handles rotated stands)
            Vector3 localInteractor = rodTransform != null
                ? rodTransform.InverseTransformPoint(
                    new Vector3(transform.position.x,
                                interactorWorldY - grabOffsetY,
                                transform.position.z))
                : new Vector3(0, interactorWorldY - grabOffsetY, 0);

            float targetLocalY = Mathf.Clamp(localInteractor.y, minY, maxY);

            // Optional stepped snapping (e.g. 0.01 = 1 cm steps)
            if (snapIncrement > 0f)
                targetLocalY = Mathf.Round(targetLocalY / snapIncrement) * snapIncrement;

            // Apply to the probe & clamp group
            if (probeAndClamp != null)
            {
                Vector3 pos = probeAndClamp.localPosition;
                pos.y = targetLocalY;
                probeAndClamp.localPosition = pos;
            }

            // Slide sound when moving
            if (audioSource != null && slideClip != null
                && Mathf.Abs(targetLocalY - lastPlayedY) > 0.005f
                && !audioSource.isPlaying)
            {
                audioSource.PlayOneShot(slideClip, 0.4f);
                lastPlayedY = targetLocalY;
            }
        }

        // ─── Grab / Release ───────────────────────────────────────────────────

        private void OnGrab(SelectEnterEventArgs args)
        {
            isHeld           = true;
            currentInteractor = args.interactorObject;

            // Record offset so the clamp doesn't jump to hand position
            float interactorWorldY = currentInteractor.GetAttachTransform(interactable).position.y;
            grabOffsetY = interactorWorldY - transform.position.y;
            lastPlayedY = probeAndClamp != null ? probeAndClamp.localPosition.y : 0f;
        }

        private void OnRelease(SelectExitEventArgs args)
        {
            isHeld            = false;
            currentInteractor = null;
        }

        // ─── Editor helper ────────────────────────────────────────────────────

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (rodTransform == null) return;

            // Draw the slide range on the rod in Scene view
            Vector3 low  = rodTransform.TransformPoint(new Vector3(0, minY, 0));
            Vector3 high = rodTransform.TransformPoint(new Vector3(0, maxY, 0));

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(low,  0.01f);
            Gizmos.DrawWireSphere(high, 0.01f);
            Gizmos.DrawLine(low, high);
        }
#endif
    }
}
