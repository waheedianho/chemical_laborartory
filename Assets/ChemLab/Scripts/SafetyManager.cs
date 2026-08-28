using System.Collections;
using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab
{
    /// <summary>
    /// Safety manager — enforces PPE (gloves) before allowing players to handle
    /// hazardous chemicals. Shows a warning overlay when a high-hazard item is
    /// grabbed without gloves.
    /// </summary>
    public class SafetyManager : MonoBehaviour
    {
        public static SafetyManager Instance { get; private set; }

        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("PPE State")]
        [SerializeField] private bool glovesWorn     = false;
        [SerializeField] private bool gogglesWorn    = false;

        [Header("Warning UI")]
        [Tooltip("UIDocument for the warning overlay")]
        [SerializeField] private UIDocument warningPanelDoc;
        [Tooltip("Name of the Label element for the title in the UXML")]
        [SerializeField] private string titleLabelName = "TitleLabel";
        [Tooltip("Name of the Label element for the body in the UXML")]
        [SerializeField] private string bodyLabelName = "BodyLabel";
        [SerializeField] private float warningDuration = 4f;

        [Header("PPE Objects")]
        [Tooltip("Gloves prefab/object that player can grab to put on")]
        [SerializeField] private XRSimpleInteractable glovesInteractable;
        [Tooltip("Goggles prefab/object that player can grab to put on")]
        [SerializeField] private XRSimpleInteractable gogglesInteractable;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   warningChimeClip;
        [SerializeField] private AudioClip   ppeEquipClip;

        [Header("Events")]
        public UnityEvent onGlovesEquipped;
        public UnityEvent onGogglesEquipped;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private Coroutine warningCoroutine;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (warningPanelDoc != null && warningPanelDoc.rootVisualElement != null)
                warningPanelDoc.rootVisualElement.style.display = DisplayStyle.None;

            if (glovesInteractable != null)
                glovesInteractable.selectEntered.AddListener(_ => EquipGloves());
            if (gogglesInteractable != null)
                gogglesInteractable.selectEntered.AddListener(_ => EquipGoggles());
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public bool GlovesWorn  => glovesWorn;
        public bool GogglesWorn => gogglesWorn;

        /// <summary>
        /// Called by HazardousItem.cs when the player grabs a chemical.
        /// Returns true if the player is safe to handle it; false shows a warning.
        /// </summary>
        public bool CheckSafeToHandle(HazardLevel hazard, ChemicalDescriptor descriptor)
        {
            bool needsGloves  = hazard >= HazardLevel.Medium;
            bool needsGoggles = hazard >= HazardLevel.High;

            bool safe = true;

            if (needsGloves && !glovesWorn)
            {
                ShowWarning("⚠ SAFETY WARNING",
                    $"{descriptor?.displayName ?? "This chemical"} is hazardous!\n" +
                    $"{descriptor?.hazardWarning ?? ""}\n\n" +
                    "Put on GLOVES before handling.\n" +
                    $"Safety tip: {descriptor?.safetyInstructions ?? ""}");
                safe = false;
            }
            else if (needsGoggles && !gogglesWorn)
            {
                ShowWarning("⚠ EYE PROTECTION REQUIRED",
                    $"{descriptor?.displayName ?? "This chemical"} can splash!\n" +
                    "Put on SAFETY GOGGLES before handling.");
                safe = false;
            }

            return safe;
        }

        public void EquipGloves()
        {
            glovesWorn = true;
            audioSource?.PlayOneShot(ppeEquipClip);
            onGlovesEquipped?.Invoke();
            Debug.Log("[SafetyManager] Gloves equipped.");
        }

        public void EquipGoggles()
        {
            gogglesWorn = true;
            audioSource?.PlayOneShot(ppeEquipClip);
            onGogglesEquipped?.Invoke();
            Debug.Log("[SafetyManager] Goggles equipped.");
        }

        // ─── Private ──────────────────────────────────────────────────────────

        private void ShowWarning(string title, string body)
        {
            if (warningPanelDoc == null || warningPanelDoc.rootVisualElement == null) return;

            var root = warningPanelDoc.rootVisualElement;
            var titleLabel = root.Q<Label>(titleLabelName);
            var bodyLabel = root.Q<Label>(bodyLabelName);

            if (titleLabel != null) titleLabel.text = title;
            if (bodyLabel != null) bodyLabel.text = body;

            root.style.display = DisplayStyle.Flex;
            audioSource?.PlayOneShot(warningChimeClip);

            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(HideWarningAfterDelay(warningDuration));
        }

        private IEnumerator HideWarningAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (warningPanelDoc != null && warningPanelDoc.rootVisualElement != null)
                warningPanelDoc.rootVisualElement.style.display = DisplayStyle.None;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Attach to any chemical bottle or flask that has a hazard level.
    /// Checks with SafetyManager when grabbed.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class HazardousItem : MonoBehaviour
    {
        [Header("Chemical Info")]
        [SerializeField] private ChemicalType chemicalType;
        [SerializeField] private HazardLevel  hazardLevel;
        [Tooltip("Optional override — leave null to pull from ChemicalDatabase")]
        [SerializeField] private string       customWarning;

        private XRGrabInteractable grabInteractable;
        private ChemicalDatabase   database;

        private void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            grabInteractable.selectEntered.AddListener(OnGrab);

            // Find database at runtime
            var rs = FindAnyObjectByType<ReactionSystem>();
            if (rs != null) database = rs.Database;
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            if (SafetyManager.Instance == null) return;

            ChemicalDescriptor desc = database?.GetDescriptor(chemicalType);
            SafetyManager.Instance.CheckSafeToHandle(hazardLevel, desc);
        }
    }
}
