using UnityEngine.UIElements;
using UnityEngine;
using UnityEngine.Events;

namespace ChemLab
{
    /// <summary>
    /// Singleton-style manager. Listens for reaction events fired by
    /// ChemicalSolution components, spawns VFX, plays audio, and shows
    /// the educational reaction panel to the player.
    /// </summary>
    public class ReactionSystem : MonoBehaviour
    {
        public static ReactionSystem Instance { get; private set; }

        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Database")]
        [SerializeField] private ChemicalDatabase database;

        [Header("Reaction UI Panel")]
        [Tooltip("UIDocument that shows the reaction result")]
        [SerializeField] private UIDocument reactionPanelDoc;
        [Tooltip("Name of the Label element for the equation in the UXML")]
        [SerializeField] private string equationLabelName = "EquationLabel";
        [Tooltip("Name of the Label element for the note in the UXML")]
        [SerializeField] private string noteLabelName = "NoteLabel";
        [SerializeField] private float panelDisplayDuration = 6f;

        [Header("VFX Prefabs")]
        [SerializeField] private ParticleSystem bubblesPrefab;
        [SerializeField] private ParticleSystem steamPrefab;
        [SerializeField] private ParticleSystem glowPrefab;

        [Header("Audio")]
        [SerializeField] private AudioSource reactionAudioSource;
        [SerializeField] private AudioClip bubblingSfx;
        [SerializeField] private AudioClip neutralizationSfx;
        [SerializeField] private AudioClip sizzleSfx;

        [Header("Events")]
        public UnityEvent<ReactionRule> onReaction;

        // ─── Runtime ─────────────────────────────────────────────────────────

        public ChemicalDatabase Database => database;

        private float panelTimer;
        private Coroutine panelCoroutine;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;

            if (reactionPanelDoc != null && reactionPanelDoc.rootVisualElement != null)
                reactionPanelDoc.rootVisualElement.style.display = DisplayStyle.None;
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Called by ChemicalSolution.onReactionOccurred. Drives all side effects.
        /// </summary>
        public void HandleReaction(ReactionRule rule, Vector3 worldPosition)
        {
            if (rule == null) return;

            // VFX
            if (rule.producesBubbles && bubblesPrefab != null)
                SpawnVFX(bubblesPrefab, worldPosition);
            if (rule.producesSteam && steamPrefab != null)
                SpawnVFX(steamPrefab, worldPosition);
            if (rule.producesGlow && glowPrefab != null)
                SpawnVFX(glowPrefab, worldPosition);

            // SFX
            PlayReactionSfx(rule);

            // UI Panel
            ShowReactionPanel(rule);

            onReaction?.Invoke(rule);

            Debug.Log($"[ReactionSystem] {rule.reactionEquation}");
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        private void SpawnVFX(ParticleSystem prefab, Vector3 position)
        {
            var ps = Instantiate(prefab, position, Quaternion.identity);
            ps.Play();
            Destroy(ps.gameObject, ps.main.duration + ps.main.startLifetime.constantMax + 1f);
        }

        private void PlayReactionSfx(ReactionRule rule)
        {
            if (reactionAudioSource == null) return;

            AudioClip clip = null;
            if (rule.producesBubbles)         clip = bubblingSfx;
            else if (rule.producesSteam)      clip = sizzleSfx;
            else                              clip = neutralizationSfx;

            if (clip != null)
                reactionAudioSource.PlayOneShot(clip);
        }

        private void ShowReactionPanel(ReactionRule rule)
        {
            if (reactionPanelDoc == null || reactionPanelDoc.rootVisualElement == null) return;

            var root = reactionPanelDoc.rootVisualElement;
            var equationLabel = root.Q<Label>(equationLabelName) ?? root.Q<Label>("EquationText");
            var noteLabel = root.Q<Label>(noteLabelName) ?? root.Q<Label>("NoteText");

            if (equationLabel != null) equationLabel.text = rule.reactionEquation;
            if (noteLabel != null) noteLabel.text = rule.educationalNote;

            root.style.display = DisplayStyle.Flex;

            if (panelCoroutine != null) StopCoroutine(panelCoroutine);
            panelCoroutine = StartCoroutine(HidePanelAfterDelay(panelDisplayDuration));
        }

        private System.Collections.IEnumerator HidePanelAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (reactionPanelDoc != null && reactionPanelDoc.rootVisualElement != null)
                reactionPanelDoc.rootVisualElement.style.display = DisplayStyle.None;
        }
    }
}
