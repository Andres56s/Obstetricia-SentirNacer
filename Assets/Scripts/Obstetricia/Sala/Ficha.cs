using Obstetricia.UI;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Ficha de estudio: al apuntar un momento a un equipo (o al tomarlo) aparece una tarjeta pequeña con su nombre y
    /// para qué se usa. Solo se muestra una a la vez.
    /// </summary>
    public class Ficha : MonoBehaviour
    {
        [SerializeField] private string titulo;
        [SerializeField, TextArea] private string texto;
        [SerializeField, Tooltip("Altura (m, local) sobre el origen donde aparece la tarjeta.")] private float altura = 1f;

        private static Canvas _card;
        private static TextMeshProUGUI _title, _body;
        private static Ficha _owner;

        private XRBaseInteractable _target;
        private float _over, _away;

        private void Start()
        {
            _target = GetComponent<XRBaseInteractable>();
            if (_target == null) _target = gameObject.AddComponent<XRSimpleInteractable>();
        }

        private void OnDisable()
        {
            _over = 0f;
            if (_owner == this) Ocultar();
        }

        private void Update()
        {
            if (_target == null) return;
            bool on = _target.isHovered || _target.isSelected;
            _over = on ? _over + Time.deltaTime : 0f;
            if (on)
            {
                _away = 0f;
                if (_over > 0.45f && _owner != this) Mostrar();
            }
            else if (_owner == this && (_away += Time.deltaTime) > 0.8f) Ocultar();
            if (_owner == this) Ubicar(false);
        }

        private void Mostrar()
        {
            if (_card == null) Construir(transform);
            _owner = this;
            _away = 0f;
            _title.text = titulo;
            _body.text = texto;
            _card.gameObject.SetActive(false);
            Ubicar(true);
            _card.gameObject.SetActive(true);
        }

        private static void Ocultar()
        {
            _owner = null;
            if (_card != null) HoloAppear.HideObject(_card.gameObject);
        }

        private void Ubicar(bool now)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 eye = cam.transform.position;
            Vector3 p = transform.position + Vector3.up * altura;
            p.y = Mathf.Clamp(p.y, eye.y - 0.55f, eye.y + 0.25f);
            Vector3 to = p - eye;
            float d = to.magnitude;
            p = eye + to * (Mathf.Clamp(d - 0.2f, 0.55f, 0.85f) / Mathf.Max(d, 0.01f)); // cerca para leer bien
            Transform t = _card.transform;
            t.position = now ? p : Vector3.Lerp(t.position, p, 1f - Mathf.Exp(-Time.deltaTime * 6f));
            t.rotation = Quaternion.LookRotation(t.position - eye, Vector3.up);
        }

        private static void Construir(Transform near)
        {
            var sala = near.GetComponentInParent<SalaVirtual>();
            _card = HoloUI.CreateCanvas(sala != null ? sala.transform : null, "Ficha", new Vector2(360, 160));
            HoloUI.BringToFront(_card, 40);
            _card.gameObject.SetActive(false);
            HoloPanel bg = HoloUI.Panel(_card.transform, "Fondo", Vector2.zero, new Vector2(360, 160), new Color(0.03f, 0.09f, 0.2f, 0.94f));
            bg.Background.raycastTarget = false; // no estorba al rayo
            _title = HoloUI.Text(_card.transform, "Titulo", "", 19, new Vector2(0, 55), new Vector2(324, 30), TextAlignmentOptions.Left, HoloColors.Accent, FontStyles.Bold);
            _title.enableAutoSizing = true;
            _title.fontSizeMin = 13f;
            _title.fontSizeMax = 19f;
            _body = HoloUI.Text(_card.transform, "Texto", "", 15, new Vector2(0, -15), new Vector2(324, 104), TextAlignmentOptions.TopLeft, HoloColors.Text);
            _body.enableAutoSizing = true;
            _body.fontSizeMin = 11f;
            _body.fontSizeMax = 15f;
        }
    }
}
