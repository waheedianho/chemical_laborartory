using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using Unity.VRTemplate;   // Rotator

namespace ChemLab
{
    /// <summary>
    /// Toggle switch for lab electrical systems.
    /// Flipping the switch (via XRSimpleInteractable grab/poke) toggles a
    /// pivot animation and fires events consumed by connected equipment:
    /// ventilators, lights, Bunsen burners, etc.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SwitchController : MonoBehaviour
    {
        // ─── Inspector ───────────────────────────────────────────────────────

        [Header("Pivot Animation")]
        [SerializeField] private Transform switchPivot;
        [SerializeField] private Vector3   onRotation  = new(20f,  0f, 0f);
        [SerializeField] private Vector3   offRotation = new(-20f, 0f, 0f);
        [SerializeField] private float     animTime    = 0.15f;

        [Header("State")]
        [SerializeField] private bool startOn = false;

        [Header("Connected Equipment")]
        [Tooltip("Ventilator objects that spin when on")]
        [SerializeField] private Rotator[]       ventilators;
        [Tooltip("Add a LabLightGroup component to each light parent, then drag it here")]
        [SerializeField] private LabLightGroup[] lightGroups;
        [Tooltip("Bunsen burners toggled by this switch")]
        [SerializeField] private BunsenBurner[]  burners;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip   switchOnClip;
        [SerializeField] private AudioClip   switchOffClip;

        [Header("Events")]
        public UnityEvent onSwitchedOn;
        public UnityEvent onSwitchedOff;
        public UnityEvent<bool> onToggled;   // bool = new state

        // ─── Runtime ─────────────────────────────────────────────────────────

        private bool      isOn = false;
        private Coroutine animCoroutine;
        private XRSimpleInteractable interactable;

        // ─── Unity lifecycle ──────────────────────────────────────────────────

        private void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(_ => Toggle());

            SetState(startOn, silent: true);
        }

        // ─── Public API ───────────────────────────────────────────────────────

        public bool IsOn => isOn;

        public void Toggle()                => SetState(!isOn);
        public void ForceOn()               => SetState(true);
        public void ForceOff()              => SetState(false);

        // ─── Private ──────────────────────────────────────────────────────────

        private void SetState(bool on, bool silent = false)
        {
            isOn = on;

            // Animate pivot
            if (switchPivot != null)
            {
                if (animCoroutine != null) StopCoroutine(animCoroutine);
                animCoroutine = StartCoroutine(AnimatePivot(on ? onRotation : offRotation));
            }

            // Ventilators
            foreach (var v in ventilators)
            {
                if (v != null) v.enabled = on;
            }

            // Light groups
            foreach (var lg in lightGroups)
            {
                if (lg == null) continue;
                if (on) lg.TurnOn();
                else    lg.TurnOff();
            }

            // Burners
            foreach (var b in burners)
            {
                if (b != null)
                {
                    if (on) b.TurnOn();
                    else    b.TurnOff();
                }
            }

            // Audio
            if (!silent && audioSource != null)
            {
                audioSource.PlayOneShot(on ? switchOnClip : switchOffClip);
            }

            // Events
            if (!silent)
            {
                if (on) onSwitchedOn?.Invoke();
                else    onSwitchedOff?.Invoke();
                onToggled?.Invoke(on);
            }
        }

        private IEnumerator AnimatePivot(Vector3 targetEulers)
        {
            Quaternion start  = switchPivot.localRotation;
            Quaternion target = Quaternion.Euler(targetEulers);
            float t = 0f;
            while (t < animTime)
            {
                t += Time.deltaTime;
                switchPivot.localRotation = Quaternion.Lerp(start, target, t / animTime);
                yield return null;
            }
            switchPivot.localRotation = target;
        }
    }
}
