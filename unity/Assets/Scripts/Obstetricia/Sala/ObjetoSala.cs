using System.Collections.Generic;
using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Objeto que se puede tomar y soltar con física (balón, Pinard, doppler). Vuelve a su sitio si se cae fuera de la sala
    /// o al ordenarla. Con "vuelveSolo" regresa flotando con suavidad en vez de caer.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ObjetoSala : MonoBehaviour
    {
        [SerializeField] private bool vuelveSolo;
        [SerializeField, Tooltip("Sonido en bucle mientras se sostiene (opcional).")] private AudioSource alTomar;

        private static readonly List<ObjetoSala> Todos = new List<ObjetoSala>();
        private Rigidbody _rb;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private bool _wasHeld;

        public XRGrabInteractable Grab { get; private set; }
        public bool Tomado => Grab != null && Grab.isSelected;
        /// <summary>Lugar distinto de su sitio donde debe quedarse (solo con "vuelveSolo").</summary>
        public Transform Destino { get; set; }

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
            Grab = GetComponent<XRGrabInteractable>();
            if (Grab == null) Grab = gameObject.AddComponent<XRGrabInteractable>();
            Grab.movementType = vuelveSolo ? XRBaseInteractable.MovementType.Instantaneous : XRBaseInteractable.MovementType.VelocityTracking;
            Grab.useDynamicAttach = true;
            Grab.throwOnDetach = !vuelveSolo;
            Grab.throwVelocityScale = 0.5f;
            if (vuelveSolo) _rb.isKinematic = true;
            Todos.Add(this);
        }

        protected virtual void OnDestroy() => Todos.Remove(this);

        public virtual void Restablecer()
        {
            Destino = null;
            transform.localPosition = _homePos;
            transform.localRotation = _homeRot;
            _rb.position = transform.position; // con interpolación no basta mover el transform
            _rb.rotation = transform.rotation;
            if (_rb.isKinematic) return;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Devuelve todos los objetos de la sala a su sitio.</summary>
        public static void RestablecerTodos()
        {
            foreach (ObjetoSala o in Todos) if (o != null && !o.Tomado) o.Restablecer();
        }

        protected virtual void Update()
        {
            bool held = Tomado;
            if (held != _wasHeld)
            {
                HoloFeedback.Play(HoloFeedback.Sound.Click, 0.35f);
                if (alTomar != null)
                {
                    if (held) alTomar.Play();
                    else alTomar.Stop();
                }
                if (held) Destino = null;
                _wasHeld = held;
            }
            if (held) return;
            if (vuelveSolo)
            {
                float k = 1f - Mathf.Exp(-Time.deltaTime * 4f);
                Vector3 p = Destino != null ? Destino.position : transform.parent.TransformPoint(_homePos);
                Quaternion r = Destino != null ? Destino.rotation : transform.parent.rotation * _homeRot;
                transform.SetPositionAndRotation(Vector3.Lerp(transform.position, p, k), Quaternion.Slerp(transform.rotation, r, k));
            }
            else if (transform.localPosition.y < -0.6f) Restablecer();
        }
    }
}
