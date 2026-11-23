using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Modules
{
    /// <summary>Hace girar y flotar suavemente un objeto mientras nadie lo toma (llama la atención en el tutorial).</summary>
    public class TutorialSpin : MonoBehaviour
    {
        private XRGrabInteractable _grab;
        private Vector3 _origin;

        private void Start()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _origin = transform.position;
        }

        private void Update()
        {
            if (_grab != null && (_grab.isSelected || _grab.firstInteractorSelecting != null)) return;
            if (_grab != null && _grab.interactorsSelecting.Count == 0 && (transform.position - _origin).sqrMagnitude > 0.0004f) return;
            transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
            transform.position = _origin + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.015f;
        }
    }
}
