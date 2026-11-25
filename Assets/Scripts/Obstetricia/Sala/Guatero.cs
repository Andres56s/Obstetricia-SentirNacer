using System;
using Obstetricia.UI;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Guatero (bolsa de agua caliente): se toma, se suelta con gravedad y se calienta en el lavamanos. Al estar tibio
    /// suelta vapor, vibra suavemente en la mano y se aplica sobre las zonas de calor de la pelvis.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Guatero : MonoBehaviour
    {
        [SerializeField] private Renderer cuerpo;
        [SerializeField] private ParticleSystem vapor;
        [SerializeField] private Color frio = new Color(0.25f, 0.42f, 0.62f);
        [SerializeField] private Color tibio = new Color(0.85f, 0.28f, 0.22f);

        private Rigidbody _rb;
        private XRGrabInteractable _grab;
        private MaterialPropertyBlock _block;
        private Vector3 _homePos;
        private Quaternion _homeRot;
        private float _nextPulse;
        private Transform _zone;
        private bool _wasHeld;

        /// <summary>Temperatura relativa: 0 frío, 1 tibio (listo para usar).</summary>
        public float Calor { get; private set; }

        public bool Tomado => _grab != null && _grab.isSelected;

        /// <summary>Se invoca una vez al quedar tibio.</summary>
        public event Action Listo;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _homePos = transform.localPosition;
            _homeRot = transform.localRotation;
            _block = new MaterialPropertyBlock();
            _grab = GetComponent<XRGrabInteractable>();
            if (_grab == null) _grab = gameObject.AddComponent<XRGrabInteractable>();
            _grab.movementType = XRBaseInteractable.MovementType.VelocityTracking;
            _grab.useDynamicAttach = true;
            _grab.throwOnDetach = true;
            _grab.throwVelocityScale = 0.6f;
            Pintar();
        }

        /// <summary>Suma calor (en la práctica, llenarlo con agua caliente).</summary>
        public void Calentar(float cantidad)
        {
            bool antes = Calor >= 1f;
            Calor = Mathf.Clamp01(Calor + cantidad);
            Pintar();
            if (antes || Calor < 1f) return;
            HoloFeedback.Play(HoloFeedback.Sound.Success, 0.7f);
            Listo?.Invoke();
        }

        /// <summary>Lo apoya sobre una zona del cuerpo y lo deja siguiéndola.</summary>
        public void Aplicar(Transform zona) => _zone = zona;

        public bool Aplicado => _zone != null;

        public void Restablecer(bool enfriar)
        {
            Soltar();
            transform.localPosition = _homePos;
            transform.localRotation = _homeRot;
            _rb.position = transform.position;
            _rb.rotation = transform.rotation;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            if (enfriar) { Calor = 0f; Pintar(); }
        }

        /// <summary>Deja de seguir la zona de calor.</summary>
        public void Soltar()
        {
            if (_zone == null) return;
            _zone = null;
            if (!Tomado) _rb.isKinematic = false;
        }

        private void Update()
        {
            bool held = Tomado;
            if (held && !_wasHeld) _zone = null;
            _wasHeld = held;

            // Al sacarlo de una zona el agarre recuerda que estaba fijo: devolverle la gravedad.
            if (_zone == null && !held && _rb.isKinematic) _rb.isKinematic = false;

            if (_zone != null && !held)
            {
                if (!_zone.gameObject.activeInHierarchy) Soltar();
                else
                {
                    if (!_rb.isKinematic) _rb.isKinematic = true;
                    float k = 1f - Mathf.Exp(-Time.deltaTime * 8f);
                    transform.SetPositionAndRotation(Vector3.Lerp(transform.position, _zone.position, k),
                        Quaternion.Slerp(transform.rotation, _zone.rotation, k));
                }
            }

            if (held && Calor > 0.5f && Time.time >= _nextPulse)
            {
                _nextPulse = Time.time + 1.6f;
                HoloFeedback.Haptic(_grab, 0.12f * Calor, 0.25f);
            }

            // Si se cae fuera de la sala, vuelve a la mesa.
            if (transform.localPosition.y < -0.6f) Restablecer(false);
        }

        private void Pintar()
        {
            if (cuerpo != null)
            {
                cuerpo.GetPropertyBlock(_block, 0);
                _block.SetColor("_BaseColor", Color.Lerp(frio, tibio, Calor));
                cuerpo.SetPropertyBlock(_block, 0);
            }
            if (vapor == null) return;
            var emission = vapor.emission;
            emission.rateOverTime = Calor * 9f;
        }
    }
}
