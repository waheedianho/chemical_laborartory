using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ChemLab
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public class DropOnRelease : MonoBehaviour
    {
        private void Awake()
        {
            var interactable = GetComponent<XRGrabInteractable>();
            var rb = GetComponent<Rigidbody>();
            
            // The moment it gets grabbed for the first time, permanently enable physics
            interactable.selectEntered.AddListener(_ => rb.isKinematic = false);
        }
    }
}
