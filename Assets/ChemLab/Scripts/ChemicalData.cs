using UnityEngine;

namespace ChemLab
{
    // ─── Chemical Identity ────────────────────────────────────────────────────

    public enum ChemicalType
    {
        Water,
        HydrochloricAcid,   // HCl  – strong acid
        SulfuricAcid,        // H2SO4
        NitricAcid,          // HNO3
        AceticAcid,          // CH3COOH – weak acid
        SodiumHydroxide,     // NaOH – strong base
        PotassiumHydroxide,  // KOH
        SodiumChloride,      // NaCl – neutral salt
        CopperSulfate,       // CuSO4 – blue solution
        PotassiumPermanganate,// KMnO4 – deep purple
        HydrogenPeroxide,    // H2O2
        Ethanol,             // C2H5OH
        Phenolphthalein,     // pH indicator
        LitmusSolution,      // pH indicator
        UnknownSolution,
        Empty
    }

    public enum HazardLevel
    {
        None,       // water, NaCl
        Low,        // ethanol, acetic acid
        Medium,     // copper sulfate, H2O2
        High,       // HCl, NaOH, KOH
        Extreme     // H2SO4, HNO3, KMnO4
    }

    public enum PhysicalState
    {
        Liquid,
        Gas,
        Solid,
        Aqueous
    }

    // ─── Chemical Descriptor ─────────────────────────────────────────────────

    [System.Serializable]
    public class ChemicalDescriptor
    {
        public ChemicalType type;
        [TextArea(1, 2)]
        public string displayName;
        [TextArea(1, 2)]
        public string formula;
        [Range(0f, 14f)]
        public float defaultPH = 7f;
        public HazardLevel hazard;
        public PhysicalState state;
        public Color liquidColor = Color.white;
        [TextArea(2, 4)]
        public string hazardWarning;
        [TextArea(2, 4)]
        public string safetyInstructions;
    }

    // ─── Reaction Rule ────────────────────────────────────────────────────────

    [System.Serializable]
    public class ReactionRule
    {
        [Header("Reactants")]
        public ChemicalType reactantA;
        public ChemicalType reactantB;

        [Header("Products")]
        public ChemicalType productChemical;
        [TextArea(1, 3)]
        public string reactionEquation;   // e.g. "HCl + NaOH → NaCl + H₂O"
        [TextArea(1, 3)]
        public string educationalNote;    // shown to player

        [Header("Visual Effects")]
        public Color resultColor;
        public bool producesBubbles;
        public bool producesSteam;
        public bool producesGlow;
        public bool temperatureChange;    // exothermic / endothermic
        public float deltaTemperature;    // +/- degrees C

        [Header("Result State")]
        [Range(0f, 14f)]
        public float resultPH = 7f;
    }
}
