using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactors.Casters;
using UnityEngine.XR.Interaction.Toolkit;

namespace Obstetricia.Core
{
    /// <summary>
    /// Decide si la mano está cerrada a partir de medidas reales: la separación entre las yemas (pellizco) y cuánto se
    /// doblan los dedos (puño). Cierra solo con contacto y suelta apenas los dedos se separan. Sin dependencias de XR.
    /// </summary>
    public class DetectorAgarre
    {
        /// <summary>Separación entre yemas (m, descontado el grosor de los dedos) para considerar que se tocan.</summary>
        public float Contacto = 0.004f;
        /// <summary>Margen extra si el visor también asegura que el pellizco está completo (dedos tapados).</summary>
        public float ContactoConfirmado = 0.007f;
        /// <summary>Separación con la que se suelta.</summary>
        public float Separacion = 0.014f;
        /// <summary>Largo del dedo doblado / estirado para contar como puño cerrado y para soltarlo.</summary>
        public float PunoCerrado = 0.55f, PunoAbierto = 0.68f;
        /// <summary>Tiempo mínimo en contacto (cierra) y separados (abre), contra parpadeos del seguimiento.</summary>
        public float TiempoCerrar = 0.035f, TiempoAbrir = 0.03f;

        public bool Pellizco { get; private set; }
        public bool Puno { get; private set; }
        /// <summary>Qué tan cerca están las yemas: 0 = mano abierta, 1 = contacto.</summary>
        public float Cierre { get; private set; }

        private float _tPellizco, _tPuno;

        /// <param name="gap">Separación entre yemas de índice y pulgar (m).</param>
        /// <param name="fuerza">Fuerza de pellizco que informa el visor (0..1; 0 si no existe).</param>
        /// <param name="dobleces">Largo relativo de índice, medio, anular y meñique (1 = estirado).</param>
        public void Actualizar(float gap, float fuerza, Vector4 dobleces, float dt)
        {
            Cierre = 1f - Mathf.Clamp01((gap - Contacto) / 0.045f);
            bool toca = gap <= Contacto || (fuerza >= 0.99f && gap <= ContactoConfirmado);
            bool pellizco = Pellizco ? gap < Separacion : toca;
            Pellizco = Filtrar(Pellizco, pellizco, ref _tPellizco, dt);

            bool puno;
            if (Puno)
            {
                int abiertos = 0;
                for (int i = 1; i < 4; i++) if (dobleces[i] > PunoAbierto) abiertos++;
                puno = abiertos < 2;
            }
            else
            {
                puno = true;
                for (int i = 0; i < 4; i++) if (dobleces[i] > PunoCerrado + (i == 0 ? 0.05f : 0f)) puno = false;
            }
            Puno = Filtrar(Puno, puno, ref _tPuno, dt);
        }

        public void Reiniciar()
        {
            Pellizco = Puno = false;
            Cierre = _tPellizco = _tPuno = 0f;
        }

        private bool Filtrar(bool actual, bool medido, ref float t, float dt)
        {
            if (medido == actual) { t = 0f; return actual; }
            t += dt;
            if (t < (medido ? TiempoCerrar : TiempoAbrir)) return actual;
            t = 0f;
            return medido;
        }
    }

    /// <summary>
    /// Botón de la mano para XRI (reemplaza al de la muestra, que tomaba una mano a medio cerrar como agarre). Lee las
    /// articulaciones: los botones responden al pellizco; los objetos cercanos, al pellizco o al puño cerrado.
    /// </summary>
    [DefaultExecutionOrder(XRInteractionUpdateOrder.k_XRInputDeviceButtonReader)]
    public class LectorMano : MonoBehaviour, IXRInputButtonReader
    {
        [SerializeField] private Handedness mano = Handedness.Right;
        [SerializeField, Tooltip("También acepta el puño cerrado sobre un objeto cercano.")] private bool aceptaPuno;
        [SerializeField] private XRBaseInputInteractor interactor;
        [SerializeField, Tooltip("Distancia (m) a la que un objeto cuenta como cercano para tomarlo con el puño.")] private float alcancePuno = 0.12f;
        [Header("Precisión")]
        [SerializeField, Tooltip("Separación entre yemas (m) que cuenta como contacto. Menor = hay que cerrar más.")] private float contacto = 0.004f;
        [SerializeField, Tooltip("Separación (m) con la que se suelta. Menor = suelta antes.")] private float separacion = 0.014f;

        private static readonly List<XRHandSubsystem> Subsystems = new List<XRHandSubsystem>();
        private static readonly LectorMano[] Activos = new LectorMano[2];
        private readonly DetectorAgarre _d = new DetectorAgarre();
        private bool _pressed, _down, _up;
        private float _lost;

        /// <summary>True mientras el visor ve esa mano.</summary>
        public bool Vista { get; private set; }

        /// <summary>Lector de la mano pedida si el visor la está siguiendo; si no, null.</summary>
        public static LectorMano De(bool izquierda)
        {
            LectorMano l = Activos[izquierda ? 0 : 1];
            return l != null && l.isActiveAndEnabled && l.Vista ? l : null;
        }

        public bool Pellizco => _d.Pellizco;
        public float Cierre => _d.Cierre;

        private void OnEnable()
        {
            if (!aceptaPuno) Activos[mano == Handedness.Left ? 0 : 1] = this;
            _d.Contacto = contacto;
            _d.ContactoConfirmado = contacto + 0.003f;
            _d.Separacion = separacion;
        }

        private void OnDisable()
        {
            _d.Reiniciar();
            Vista = false;
            _pressed = _down = _up = false;
        }

        private void Update()
        {
            bool before = _pressed;
            Vista = Medir(out float gap, out float fuerza, out Vector4 dobleces);
            if (Vista)
            {
                _lost = 0f;
                _d.Actualizar(gap, fuerza, dobleces, Time.unscaledDeltaTime);
            }
            else if ((_lost += Time.unscaledDeltaTime) > 0.25f) _d.Reiniciar(); // perdió la mano: soltar

            bool puno = aceptaPuno && _d.Puno && (before || ObjetoCerca());
            _pressed = _d.Pellizco || puno;
            _down = _pressed && !before;
            _up = before && !_pressed;
        }

        private bool ObjetoCerca()
        {
            if (interactor == null) return false;
            Vector3 p = interactor.transform.position;
            if (interactor is NearFarInteractor nf && nf.nearInteractionCaster is InteractionCasterBase c && c.castOrigin != null) p = c.castOrigin.position;
            foreach (IXRHoverInteractable h in interactor.interactablesHovered)
                if (h is XRBaseInteractable b && b.GetDistance(p).distanceSqr < alcancePuno * alcancePuno) return true;
            return false;
        }

        private bool Medir(out float gap, out float fuerza, out Vector4 dobleces)
        {
            gap = 1f;
            fuerza = 0f;
            dobleces = Vector4.one;
            XRHandSubsystem sub = null;
            SubsystemManager.GetSubsystems(Subsystems);
            foreach (XRHandSubsystem s in Subsystems) if (s.running) { sub = s; break; }
            if (sub == null) return false;
            XRHand h = mano == Handedness.Left ? sub.leftHand : sub.rightHand;
            if (!h.isTracked) return false;

            XRHandJoint thumb = h.GetJoint(XRHandJointID.ThumbTip), index = h.GetJoint(XRHandJointID.IndexTip);
            if (!thumb.TryGetPose(out Pose pt) || !index.TryGetPose(out Pose pi)) return false;
            if (!thumb.TryGetRadius(out float rt) || rt < 0.003f) rt = 0.0085f;
            if (!index.TryGetRadius(out float ri) || ri < 0.003f) ri = 0.0075f;
            gap = Vector3.Distance(pt.position, pi.position) - (rt + ri);

            MetaAimHand aim = mano == Handedness.Left ? MetaAimHand.left : MetaAimHand.right;
            if (aim != null && aim.added)
            {
                fuerza = aim.pinchStrengthIndex.ReadValue();
                // Gesto del sistema (palma hacia la cara): no es un agarre.
                if (((MetaAimFlags)aim.aimFlags.ReadValue() & MetaAimFlags.SystemGesture) != 0 && !_d.Pellizco) gap = 1f;
            }

            dobleces = new Vector4(
                Doblez(h, XRHandJointID.IndexProximal, XRHandJointID.IndexIntermediate, XRHandJointID.IndexDistal, XRHandJointID.IndexTip),
                Doblez(h, XRHandJointID.MiddleProximal, XRHandJointID.MiddleIntermediate, XRHandJointID.MiddleDistal, XRHandJointID.MiddleTip),
                Doblez(h, XRHandJointID.RingProximal, XRHandJointID.RingIntermediate, XRHandJointID.RingDistal, XRHandJointID.RingTip),
                Doblez(h, XRHandJointID.LittleProximal, XRHandJointID.LittleIntermediate, XRHandJointID.LittleDistal, XRHandJointID.LittleTip));
            return true;
        }

        private static float Doblez(XRHand h, XRHandJointID a, XRHandJointID b, XRHandJointID c, XRHandJointID d)
        {
            if (!h.GetJoint(a).TryGetPose(out Pose pa) || !h.GetJoint(b).TryGetPose(out Pose pb)
                || !h.GetJoint(c).TryGetPose(out Pose pc) || !h.GetJoint(d).TryGetPose(out Pose pd)) return 1f;
            float largo = Vector3.Distance(pa.position, pb.position) + Vector3.Distance(pb.position, pc.position) + Vector3.Distance(pc.position, pd.position);
            return largo > 0.01f ? Vector3.Distance(pa.position, pd.position) / largo : 1f;
        }

        public bool ReadIsPerformed() => _pressed;
        public bool ReadWasPerformedThisFrame() => _down;
        public bool ReadWasCompletedThisFrame() => _up;
        public float ReadValue() => _pressed ? 1f : _d.Cierre * 0.95f;

        public bool TryReadValue(out float value)
        {
            value = ReadValue();
            return Vista;
        }
    }
}
