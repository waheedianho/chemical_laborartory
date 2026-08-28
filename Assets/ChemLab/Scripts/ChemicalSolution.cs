using System.Collections.Generic;
using ChemLab.Scripts;
using UnityEngine;
using UnityEngine.Events;

namespace ChemLab
{
    /// <summary>
    /// Attached to any vessel (flask, beaker, test tube, bottle).
    /// Tracks the liquid contents, volume, pH, temperature and notifies
    /// the ReactionSystem when contents change.
    /// </summary>
    public class ChemicalSolution : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Initial Contents")]
        [SerializeField] private ChemicalType initialChemical = ChemicalType.Empty;
        [SerializeField, Range(0f, 1f)] private float initialVolume = 0f;

        [Header("Vessel Capacity")]
        [SerializeField] private float maxVolumeMl = 250f;   // millilitres

        [Header("Fill Visual")]
        [Tooltip("Child object that is scaled on Y to represent liquid level")]
        [SerializeField] private Transform liquidLevelTransform;
        [Tooltip("Renderer whose material color will be tinted to the liquid color")]
        [SerializeField] private Renderer liquidRenderer;
        [SerializeField] private string liquidColorProperty = "_BaseColor";
        [Tooltip("If the shader uses a property for fill level instead of scaling the mesh, specify it here.")]
        [SerializeField] private string liquidFillProperty = "_FillAmount";

        [Header("Events")]
        public UnityEvent<ChemicalType, float> onContentsChanged;  // (chemical, volume)
        public UnityEvent<ReactionRule>         onReactionOccurred;

        // ─── Runtime state ───────────────────────────────────────────────────

        private List<(ChemicalType chemical, float volumeMl)> contents = new();
        private float currentPH   = 7f;
        private float temperature = 20f;  // degrees C
        private ChemicalDatabase database;
        private float initialLiquidScaleY = 1f;
        private MaterialPropertyBlock propBlock;
        private global::Liquid customLiquidShader;

        // ─── Properties ──────────────────────────────────────────────────────

        public float TotalVolumeMl
        {
            get
            {
                float total = 0f;
                foreach (var c in contents) total += c.volumeMl;
                return total;
            }
        }

        public float FillFraction => Mathf.Clamp01(TotalVolumeMl / maxVolumeMl);
        public float PH           => currentPH;
        public float Temperature  => temperature;
        public bool  IsEmpty      => TotalVolumeMl <= 0.01f;
        public float MaxVolumeMl  => maxVolumeMl;
        
        /// <summary>Returns the collider attached to the liquid level marker (if any).</summary>
        public Collider LiquidTrigger => liquidLevelTransform != null ? liquidLevelTransform.GetComponent<Collider>() : null;

        /// <summary>Returns the dominant chemical (by volume).</summary>
        public ChemicalType DominantChemical
        {
            get
            {
                ChemicalType dominant = ChemicalType.Empty;
                float max = 0f;
                foreach (var (chem, vol) in contents)
                    if (vol > max) { max = vol; dominant = chem; }
                return dominant;
            }
        }

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            database = FindAnyObjectByType<ReactionSystem>()?.Database;
            if (liquidRenderer == null)
                liquidRenderer = GetComponentInChildren<Renderer>();

            if (liquidRenderer != null)
                customLiquidShader = liquidRenderer.GetComponent<global::Liquid>();

            if (liquidLevelTransform != null)
                initialLiquidScaleY = liquidLevelTransform.localScale.y;

            if (initialChemical != ChemicalType.Empty && initialVolume > 0f)
            {
                float ml = initialVolume * maxVolumeMl;
                contents.Add((initialChemical, ml));
                RecalculatePH();
            }

            RefreshVisuals();
        }

        // ─── Public API ───────────────────────────────────────────────────────

        /// <summary>
        /// Pour volumeMl of chemical into this vessel. Automatically checks for
        /// reactions and updates all visuals.
        /// </summary>
        public void Receive(ChemicalType chemical, float volumeMl)
        {
            if (TotalVolumeMl + volumeMl > maxVolumeMl)
                volumeMl = maxVolumeMl - TotalVolumeMl;

            if (volumeMl <= 0f) return;

            // Check for reaction with existing contents
            ReactionRule reactionFired = null;
            if (database != null)
            {
                ChemicalType dominant = DominantChemical;
                if (dominant != ChemicalType.Empty)
                {
                    var rule = database.FindReaction(dominant, chemical);
                    if (rule != null)
                    {
                        reactionFired = rule;
                        // Replace contents with product
                        float totalVol = TotalVolumeMl + volumeMl;
                        contents.Clear();
                        contents.Add((rule.productChemical, totalVol));
                        currentPH   = rule.resultPH;
                        temperature += rule.deltaTemperature;
                        onReactionOccurred?.Invoke(rule);
                        ReactionSystem.Instance?.HandleReaction(rule, transform.position);
                        RefreshVisuals();
                        onContentsChanged?.Invoke(DominantChemical, TotalVolumeMl);
                        return;
                    }
                }
            }

            // No reaction — just add
            bool found = false;
            for (int i = 0; i < contents.Count; i++)
            {
                if (contents[i].chemical == chemical)
                {
                    contents[i] = (chemical, contents[i].volumeMl + volumeMl);
                    found = true;
                    break;
                }
            }
            if (!found) contents.Add((chemical, volumeMl));

            RecalculatePH();
            RefreshVisuals();
            onContentsChanged?.Invoke(DominantChemical, TotalVolumeMl);
        }

        /// <summary>Removes up to volumeMl of liquid and returns how much was removed.</summary>
        public float Pour(float volumeMl)
        {
            float available = TotalVolumeMl;
            float removed   = Mathf.Min(volumeMl, available);
            float fraction  = available > 0f ? removed / available : 0f;

            for (int i = 0; i < contents.Count; i++)
                contents[i] = (contents[i].chemical, contents[i].volumeMl * (1f - fraction));

            contents.RemoveAll(c => c.volumeMl < 0.1f);
            RefreshVisuals();
            onContentsChanged?.Invoke(DominantChemical, TotalVolumeMl);
            return removed;
        }

        public void AddTemperature(float delta) => temperature = Mathf.Clamp(temperature + delta, -20f, 300f);

        // ─── Private helpers ──────────────────────────────────────────────────

        private void RecalculatePH()
        {
            if (IsEmpty) { currentPH = 7f; return; }
            float totalVol = TotalVolumeMl;
            float weightedPH = 0f;
            foreach (var (chem, vol) in contents)
            {
                var desc = database?.GetDescriptor(chem);
                float ph = desc != null ? desc.defaultPH : 7f;
                weightedPH += ph * (vol / totalVol);
            }
            currentPH = weightedPH;
        }

        private void RefreshVisuals()
        {
            // Position or scale the liquid-level marker
            if (liquidLevelTransform != null)
            {
                bool showLiquid = !IsEmpty;
                if (liquidLevelTransform.gameObject.activeSelf != showLiquid)
                    liquidLevelTransform.gameObject.SetActive(showLiquid);

                if (showLiquid)
                {
                    if (customLiquidShader != null && liquidRenderer != null)
                    {
                        // The custom shader handles the visual fill without scaling the mesh.
                        // Repurpose LevelTransform to move to the exact world-Y surface of the liquid.
                        float surfaceY = Mathf.Lerp(liquidRenderer.bounds.min.y, liquidRenderer.bounds.max.y, FillFraction);
                        Vector3 wPos = liquidLevelTransform.position;
                        wPos.y = surfaceY;
                        liquidLevelTransform.position = wPos;
                    }
                    else
                    {
                        // Legacy fallback: scale the transform to visually represent the liquid
                        Vector3 s = liquidLevelTransform.localScale;
                        s.y = Mathf.Max(0.001f, FillFraction * initialLiquidScaleY);
                        liquidLevelTransform.localScale = s;
                    }
                }
            }

            // Tint color
            if (liquidRenderer != null && database != null && !IsEmpty)
            {
                Color target = Color.clear;
                float totalVol = TotalVolumeMl;
                if (totalVol > 0f)
                {
                    foreach (var (chem, vol) in contents)
                    {
                        var desc = database.GetDescriptor(chem);

                        if (desc != null)
                            target += desc.liquidColor * (vol / totalVol);
                    }
                }

                if (propBlock == null)
                    propBlock = new MaterialPropertyBlock();

                liquidRenderer.GetPropertyBlock(propBlock);
                propBlock.SetColor(liquidColorProperty, target);

                if (liquidRenderer.sharedMaterial != null && liquidRenderer.sharedMaterial.HasProperty(liquidFillProperty))
                {
                    propBlock.SetFloat(liquidFillProperty, FillFraction);
                }

                // If using the Liquid shader component, set its planePosition.
                // In Liquid.cs the plane sits at: bounds.center + (objHeight * planePosition.y)
                // So planePosition.y = -0.5 → bottom, 0.0 → center, +0.5 → top.
                if (customLiquidShader != null)
                {
                    Vector3 p = customLiquidShader.planePosition;
                    float newY = Mathf.Lerp(-0.5f, 0.5f, FillFraction);
                    p.y = newY;
                    customLiquidShader.planePosition = p;
                    Debug.Log($"[ChemLab] Set planePosition.y = {newY} (FillFraction = {FillFraction}, TotalVol = {TotalVolumeMl})");
                }
                else
                {
                    Debug.Log($"[ChemLab] customLiquidShader is NULL! Liquid component not found on {liquidRenderer.gameObject.name}");
                }

                liquidRenderer.SetPropertyBlock(propBlock);
            }
        }

        // ─── Debug ───────────────────────────────────────────────────────────
        #if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            UnityEditor.Handles.Label(transform.position + Vector3.up * 0.15f,
                $"{gameObject.name}\nChemical: {DominantChemical}\nVol: {TotalVolumeMl:F0} mL\npH: {currentPH:F1}\nTemp: {temperature:F0}°C");
        }
        #endif
    }
}
