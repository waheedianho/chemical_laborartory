using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace ChemLab
{
    /// <summary>
    /// ScriptableObject that defines every chemical's properties and all possible
    /// reactions. Create one instance via Assets > Create > ChemLab > Chemical Database.
    /// </summary>
    [CreateAssetMenu(fileName = "ChemicalDatabase", menuName = "ChemLab/Chemical Database")]
    public class ChemicalDatabase : ScriptableObject
    {
        [Header("Chemical Properties")]
        [SerializeField] private List<ChemicalDescriptor> chemicals = new();

        [Header("Reaction Rules")]
        [SerializeField] private List<ReactionRule> reactions = new();

        // ─── Lookup helpers ──────────────────────────────────────────────────

        public ChemicalDescriptor GetDescriptor(ChemicalType type)
        {
            return chemicals.FirstOrDefault(c => c.type == type);
        }

        /// <summary>
        /// Returns a matching reaction rule if reactantA + reactantB produce something.
        /// Order-independent (A+B == B+A).
        /// </summary>
        public ReactionRule FindReaction(ChemicalType a, ChemicalType b)
        {
            return reactions.FirstOrDefault(r =>
                (r.reactantA == a && r.reactantB == b) ||
                (r.reactantA == b && r.reactantB == a));
        }

        public bool HasReaction(ChemicalType a, ChemicalType b) => FindReaction(a, b) != null;

        // ─── Default data (called by editor helper, not at runtime) ──────────

        #if UNITY_EDITOR
        [ContextMenu("Populate Default Chemistry Data")]
        private void PopulateDefaults()
        {
            chemicals = new List<ChemicalDescriptor>
            {
                new() { type = ChemicalType.Water,              displayName = "Water",                 formula = "H₂O",    defaultPH = 7f,  hazard = HazardLevel.None,   state = PhysicalState.Liquid, liquidColor = new Color(0.53f, 0.81f, 0.98f, 0.6f), hazardWarning = "",                                           safetyInstructions = "No special precautions needed." },
                new() { type = ChemicalType.HydrochloricAcid,   displayName = "Hydrochloric Acid",    formula = "HCl",    defaultPH = 1f,  hazard = HazardLevel.High,   state = PhysicalState.Aqueous, liquidColor = new Color(0.9f, 0.95f, 1f, 0.7f),       hazardWarning = "⚠ Strong Acid! Corrosive to skin and eyes.", safetyInstructions = "Wear gloves and eye protection. Avoid inhalation of fumes." },
                new() { type = ChemicalType.SulfuricAcid,        displayName = "Sulfuric Acid",        formula = "H₂SO₄", defaultPH = 0.5f,hazard = HazardLevel.Extreme, state = PhysicalState.Liquid, liquidColor = new Color(1f, 0.98f, 0.8f, 0.7f),       hazardWarning = "☠ Highly Corrosive! Causes severe burns.",   safetyInstructions = "Full PPE required. Never add water to acid." },
                new() { type = ChemicalType.AceticAcid,          displayName = "Acetic Acid",          formula = "CH₃COOH",defaultPH = 3f, hazard = HazardLevel.Low,    state = PhysicalState.Liquid, liquidColor = new Color(0.95f, 0.95f, 0.9f, 0.6f),    hazardWarning = "⚠ Weak acid. Pungent odour.",                safetyInstructions = "Avoid direct contact. Work in ventilated area." },
                new() { type = ChemicalType.SodiumHydroxide,     displayName = "Sodium Hydroxide",    formula = "NaOH",   defaultPH = 14f, hazard = HazardLevel.High,   state = PhysicalState.Aqueous, liquidColor = new Color(0.9f, 0.9f, 0.95f, 0.6f),    hazardWarning = "⚠ Strong Base! Causes chemical burns.",      safetyInstructions = "Wear gloves and goggles. Highly exothermic when dissolved." },
                new() { type = ChemicalType.SodiumChloride,      displayName = "Sodium Chloride (Salt)",formula = "NaCl",  defaultPH = 7f, hazard = HazardLevel.None,   state = PhysicalState.Aqueous, liquidColor = new Color(0.95f, 0.97f, 1f, 0.5f),     hazardWarning = "",                                           safetyInstructions = "Common table salt. No special precautions." },
                new() { type = ChemicalType.CopperSulfate,       displayName = "Copper Sulfate",      formula = "CuSO₄", defaultPH = 4f,  hazard = HazardLevel.Medium, state = PhysicalState.Aqueous, liquidColor = new Color(0.08f, 0.45f, 0.86f, 0.8f),  hazardWarning = "⚠ Irritant. Toxic to aquatic life.",        safetyInstructions = "Avoid skin contact. Dispose of properly." },
                new() { type = ChemicalType.PotassiumPermanganate,displayName="Potassium Permanganate",formula = "KMnO₄",defaultPH = 7f,  hazard = HazardLevel.Extreme,state = PhysicalState.Aqueous, liquidColor = new Color(0.5f, 0f, 0.5f, 0.9f),       hazardWarning = "☠ Strong oxidiser. Stains and burns skin.",  safetyInstructions = "Full PPE. Store away from flammables." },
                new() { type = ChemicalType.HydrogenPeroxide,    displayName = "Hydrogen Peroxide",   formula = "H₂O₂",  defaultPH = 6f,  hazard = HazardLevel.Medium, state = PhysicalState.Liquid, liquidColor = new Color(0.9f, 0.97f, 1f, 0.6f),      hazardWarning = "⚠ Oxidiser. Can bleach skin and clothing.",  safetyInstructions = "Wear gloves. Keep away from heat." },
                new() { type = ChemicalType.Ethanol,             displayName = "Ethanol",             formula = "C₂H₅OH",defaultPH = 7f,  hazard = HazardLevel.Low,    state = PhysicalState.Liquid, liquidColor = new Color(0.97f, 0.97f, 0.97f, 0.5f),  hazardWarning = "⚠ Flammable liquid and vapour.",             safetyInstructions = "Keep away from open flames. Ensure ventilation." },
                new() { type = ChemicalType.Phenolphthalein,     displayName = "Phenolphthalein",     formula = "C₂₀H₁₄O₄",defaultPH=7f, hazard = HazardLevel.Low,    state = PhysicalState.Liquid, liquidColor = new Color(0.95f, 0.9f, 0.95f, 0.5f),  hazardWarning = "⚠ pH indicator. Mildly irritant.",           safetyInstructions = "Avoid skin contact." },
                new() { type = ChemicalType.LitmusSolution,      displayName = "Litmus Solution",     formula = "Litmus", defaultPH = 7f, hazard = HazardLevel.None,   state = PhysicalState.Liquid, liquidColor = new Color(0.55f, 0.4f, 0.65f, 0.6f),  hazardWarning = "",                                           safetyInstructions = "Safe pH indicator. No special precautions." },
                new() { type = ChemicalType.Empty,               displayName = "Empty",               formula = "",       defaultPH = 7f, hazard = HazardLevel.None,   state = PhysicalState.Liquid, liquidColor = Color.clear,                            hazardWarning = "",                                           safetyInstructions = "" },
            };

            reactions = new List<ReactionRule>
            {
                // Acid + Base neutralisations
                new() { reactantA = ChemicalType.HydrochloricAcid,  reactantB = ChemicalType.SodiumHydroxide, productChemical = ChemicalType.SodiumChloride,  reactionEquation = "HCl + NaOH → NaCl + H₂O",   educationalNote = "Neutralisation! A strong acid and base produce a neutral salt solution and water. pH rises toward 7.", resultColor = new Color(0.95f,0.97f,1f,0.5f), producesBubbles = false, producesSteam = true,  producesGlow = false, temperatureChange = true, deltaTemperature = 12f, resultPH = 7f },
                new() { reactantA = ChemicalType.SulfuricAcid,       reactantB = ChemicalType.SodiumHydroxide, productChemical = ChemicalType.SodiumChloride,  reactionEquation = "H₂SO₄ + 2NaOH → Na₂SO₄ + 2H₂O", educationalNote = "Exothermic neutralisation. Sulfuric acid is diprotic — needs twice as much base!", resultColor = new Color(0.9f,0.95f,1f,0.5f), producesBubbles = false, producesSteam = true,  producesGlow = false, temperatureChange = true, deltaTemperature = 18f, resultPH = 7f },
                new() { reactantA = ChemicalType.AceticAcid,         reactantB = ChemicalType.SodiumHydroxide, productChemical = ChemicalType.SodiumChloride,  reactionEquation = "CH₃COOH + NaOH → CH₃COONa + H₂O", educationalNote = "Weak acid + strong base. The resulting solution is slightly basic (pH > 7) due to salt hydrolysis.", resultColor = new Color(0.95f,0.97f,0.95f,0.5f), producesBubbles = false, producesSteam = false, producesGlow = false, temperatureChange = true, deltaTemperature = 6f, resultPH = 8.9f },
                // Indicator reactions
                new() { reactantA = ChemicalType.Phenolphthalein,    reactantB = ChemicalType.SodiumHydroxide, productChemical = ChemicalType.Phenolphthalein, reactionEquation = "Phenolphthalein turns pink/magenta in base", educationalNote = "Phenolphthalein is colourless in acid, pink in base. This is used in titrations to detect the endpoint!", resultColor = new Color(0.93f,0.1f,0.55f,0.7f), producesBubbles = false, producesSteam = false, producesGlow = true,  temperatureChange = false, deltaTemperature = 0f, resultPH = 9f },
                new() { reactantA = ChemicalType.Phenolphthalein,    reactantB = ChemicalType.HydrochloricAcid,productChemical = ChemicalType.Phenolphthalein, reactionEquation = "Phenolphthalein remains colourless in acid", educationalNote = "Phenolphthalein loses its colour in acidic conditions. Add a base to see it turn pink!", resultColor = new Color(0.95f,0.9f,0.95f,0.3f), producesBubbles = false, producesSteam = false, producesGlow = false, temperatureChange = false, deltaTemperature = 0f, resultPH = 3f },
                new() { reactantA = ChemicalType.LitmusSolution,     reactantB = ChemicalType.HydrochloricAcid,productChemical = ChemicalType.LitmusSolution,  reactionEquation = "Litmus turns red in acid",            educationalNote = "Litmus is a natural pH indicator. It turns red in acidic solutions (pH < 7).",   resultColor = new Color(0.85f,0.1f,0.1f,0.7f), producesBubbles = false, producesSteam = false, producesGlow = false, temperatureChange = false, deltaTemperature = 0f, resultPH = 2f },
                new() { reactantA = ChemicalType.LitmusSolution,     reactantB = ChemicalType.SodiumHydroxide, productChemical = ChemicalType.LitmusSolution,  reactionEquation = "Litmus turns blue in base",           educationalNote = "Litmus turns blue in basic solutions (pH > 7). Used widely as a simple indicator.",resultColor = new Color(0.1f,0.2f,0.85f,0.7f), producesBubbles = false, producesSteam = false, producesGlow = false, temperatureChange = false, deltaTemperature = 0f, resultPH = 12f },
                // Oxidation / decomposition
                new() { reactantA = ChemicalType.HydrogenPeroxide,   reactantB = ChemicalType.PotassiumPermanganate, productChemical = ChemicalType.Water, reactionEquation = "2KMnO₄ + 5H₂O₂ + 3H₂SO₄ → 2MnSO₄ + ...", educationalNote = "KMnO₄ catalyses the decomposition of H₂O₂. Vigorous bubbling of oxygen gas is observed!", resultColor = new Color(0.8f,0.95f,0.8f,0.5f), producesBubbles = true, producesSteam = false, producesGlow = false, temperatureChange = true, deltaTemperature = 5f, resultPH = 6f },
                // Dissolution
                new() { reactantA = ChemicalType.CopperSulfate,      reactantB = ChemicalType.Water,            productChemical = ChemicalType.CopperSulfate,   reactionEquation = "CuSO₄ + H₂O → Cu²⁺ + SO₄²⁻ (aq)",  educationalNote = "Copper sulfate dissolves to form a bright blue solution. Cu²⁺ ions are responsible for the colour!", resultColor = new Color(0.08f,0.45f,0.86f,0.75f), producesBubbles = false, producesSteam = false, producesGlow = false, temperatureChange = false, deltaTemperature = 0f, resultPH = 4f },
            };

            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log("[ChemicalDatabase] Populated with default data.");
        }
        #endif
    }
}
