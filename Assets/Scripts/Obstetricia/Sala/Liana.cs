using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>Tela colgante para el trabajo de parto: cuelga del techo, se mece al soltarla y sigue la mano al tirarla.</summary>
    public class Liana : MonoBehaviour
    {
        [SerializeField] private float anguloMaximo = 22f;

        private XRSimpleInteractable _grab;
        private Transform _hand;
        private Vector3 _handStart;
        private Vector2 _a, _v, _start; // inclinación (grados) en X y Z y su velocidad

        private void Start()
        {
            _grab = GetComponent<XRSimpleInteractable>();
            if (_grab == null) _grab = gameObject.AddComponent<XRSimpleInteractable>();
            _grab.selectEntered.AddListener(e =>
            {
                _hand = e.interactorObject.GetAttachTransform(_grab);
                _handStart = _hand.position;
                _start = _a;
            });
            _grab.selectExited.AddListener(_ => _hand = null);
        }

        private void OnDisable()
        {
            _hand = null;
            _a = _v = Vector2.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.03f);
            Vector2 target = Vector2.zero;
            float k = 9f, damp = 0.9f;
            if (_hand != null)
            {
                Vector3 d = transform.parent.InverseTransformVector(_hand.position - _handStart);
                target = Vector2.ClampMagnitude(_start + new Vector2(-d.z, d.x) / 1.5f * Mathf.Rad2Deg, anguloMaximo);
                k = 90f;
                damp = 16f;
            }
            _v += ((target - _a) * k - _v * damp) * dt;
            _a += _v * dt;
            if (_hand == null && _a.sqrMagnitude < 0.0004f && _v.sqrMagnitude < 0.0004f) return;
            transform.localRotation = Quaternion.Euler(_a.x, 0f, _a.y);
        }
    }
}
