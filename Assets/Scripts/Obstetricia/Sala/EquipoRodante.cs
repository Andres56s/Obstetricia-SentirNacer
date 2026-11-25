using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Mobiliario clínico con ruedas (la cama): se toma y se desliza por el piso con física real (choca con muros
    /// y otros equipos, nunca se levanta ni se vuelca). La altura se ajusta con una deformación de la malla ("Altura").
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class EquipoRodante : MonoBehaviour
    {
        [SerializeField] private SkinnedMeshRenderer malla;
        [SerializeField, Tooltip("Colliders que suben junto con la altura.")] private Transform partesMoviles;
        [SerializeField, Tooltip("Metros que sube al 100 %.")] private float recorrido = 0.22f;
        [SerializeField] private float velocidadMaxima = 0.9f;

        private Rigidbody _rb;
        private XRSimpleInteractable _grab;
        private Transform _hand;
        private Vector3 _handStart, _bodyStart;
        private float _height, _target, _movilY;
        private Vector3 _homePos;
        private Quaternion _homeRot;

        /// <summary>Altura actual (0..1).</summary>
        public float Altura => _target;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.useGravity = false;
            _rb.linearDamping = 6f;
            _rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotation;
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
            if (partesMoviles != null) _movilY = partesMoviles.localPosition.y;
            _grab = GetComponent<XRSimpleInteractable>();
            if (_grab == null) _grab = gameObject.AddComponent<XRSimpleInteractable>();
            _grab.selectEntered.AddListener(OnGrab);
            _grab.selectExited.AddListener(OnRelease);
        }

        private void OnGrab(SelectEnterEventArgs args)
        {
            _hand = args.interactorObject.GetAttachTransform(_grab);
            _handStart = _hand.position;
            _bodyStart = _rb.position;
            HoloFeedback.Play(HoloFeedback.Sound.Click, 0.5f);
        }

        private void OnRelease(SelectExitEventArgs args) => _hand = null;

        /// <summary>Deja de seguir la mano (por ejemplo, al cambiar de punto en la sala).</summary>
        public void Soltar()
        {
            _hand = null;
            if (_rb != null) _rb.linearVelocity = Vector3.zero;
        }

        /// <summary>Sube (+) o baja (-) la altura en la fracción indicada.</summary>
        public void Ajustar(float delta) => _target = Mathf.Clamp01(_target + delta);

        /// <summary>Vuelve a su sitio y altura originales.</summary>
        public void Restablecer()
        {
            transform.localPosition = _homePos;
            transform.localRotation = _homeRot;
            _rb.linearVelocity = Vector3.zero;
            _target = 0f;
        }

        private void FixedUpdate()
        {
            if (_hand == null) return;
            Vector3 delta = _bodyStart + (_hand.position - _handStart) - _rb.position;
            delta.y = 0f;
            _rb.linearVelocity = Vector3.ClampMagnitude(delta * 5f, velocidadMaxima);
        }

        private void Update()
        {
            if (Mathf.Approximately(_height, _target)) return;
            _height = Mathf.MoveTowards(_height, _target, Time.deltaTime * 0.35f); // velocidad de motor eléctrico
            if (malla != null) malla.SetBlendShapeWeight(0, _height * 100f);
            if (partesMoviles != null)
            {
                Vector3 p = partesMoviles.localPosition;
                p.y = _movilY + _height * recorrido;
                partesMoviles.localPosition = p;
            }
        }
    }
}
