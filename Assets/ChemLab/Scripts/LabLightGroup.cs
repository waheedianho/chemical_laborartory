using UnityEngine;

namespace ChemLab
{
    /// <summary>
    /// Attach this to any light parent object in the lab (e.g. RoofLight, AreaLight).
    /// SwitchController references this component — giving a type-safe, clearly named
    /// field in the Inspector that only accepts real light group objects.
    ///
    /// On Awake it automatically finds all Light components on itself and its children,
    /// so you never have to wire up individual bulbs manually.
    /// </summary>
    public class LabLightGroup : MonoBehaviour
    {
        [Header("Override (optional)")]
        [Tooltip("Leave empty to auto-find all Lights on this object and its children")]
        [SerializeField] private Light[] lights;

        [Header("Default State")]
        [SerializeField] private bool onByDefault = true;

        // ─── Runtime ─────────────────────────────────────────────────────────

        private void Awake()
        {
            // Auto-populate if not manually set
            if (lights == null || lights.Length == 0)
                lights = GetComponentsInChildren<Light>(includeInactive: true);

            SetEnabled(onByDefault);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public void TurnOn()  => SetEnabled(true);
        public void TurnOff() => SetEnabled(false);
        public void Toggle()  => SetEnabled(lights.Length > 0 && !lights[0].enabled);

        public bool IsOn => lights != null && lights.Length > 0 && lights[0].enabled;

        // ─── Private ──────────────────────────────────────────────────────────

        private void SetEnabled(bool state)
        {
            if (lights == null) return;
            foreach (var l in lights)
                if (l != null) l.enabled = state;
        }
    }
}
