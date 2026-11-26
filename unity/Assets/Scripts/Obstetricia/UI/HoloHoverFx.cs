using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Obstetricia.UI
{
    /// <summary>
    /// Respuesta al apuntar y presionar: el elemento crece levemente, su borde se ilumina, suena un "tic" y el
    /// control vibra. Al presionar se hunde un instante. Hace evidente qué se está señalando (clave para
    /// quienes recién usan VR).
    /// </summary>
    public class HoloHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
    {
        [Tooltip("Escala al apuntar (1 = sin cambio).")]
        public float hoverScale = 1.05f;
        [Tooltip("Agregar un halo luminoso sobre el borde al apuntar.")]
        public bool glow = true;
        public bool sounds = true;
        [Tooltip("Texto de ayuda que aparece sobre el elemento al apuntarlo (vacío = sin ayuda).")]
        public string tooltip;
        public Vector2 tooltipOffset = new Vector2(0f, 52f);

        private Selectable _selectable;
        private Vector3 _baseScale;
        private bool _hover, _pressed;
        private float _current = 1f;
        private Image _halo;
        private CanvasGroup _tip;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _baseScale = transform.localScale;
            if (glow && transform is RectTransform rt && rt.rect.width > 0f)
            {
                var go = new GameObject("Resalte", typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                go.transform.SetSiblingIndex(Mathf.Min(1, transform.childCount - 1));
                int m = HoloSprites.OutlineMargin;
                HoloUI.Stretch((RectTransform)go.transform, -m, -m, -m, -m);
                _halo = go.AddComponent<Image>();
                _halo.sprite = HoloSprites.Outline;
                _halo.type = Image.Type.Sliced;
                _halo.raycastTarget = false;
                _halo.color = new Color(0.75f, 0.95f, 1f, 0f);
            }
        }

        /// <summary>Crea la burbuja de ayuda (se llama al asignar tooltip después de Awake).</summary>
        public void BuildTooltip()
        {
            if (string.IsNullOrEmpty(tooltip) || _tip != null) return;
            HoloPanel bubble = HoloUI.Panel(transform, "Ayuda", tooltipOffset, new Vector2(Mathf.Max(90f, tooltip.Length * 10.5f + 30f), 36f),
                new Color(0.02f, 0.08f, 0.18f, 0.95f), HoloColors.Accent);
            bubble.Background.raycastTarget = false;
            HoloUI.Text(bubble.transform, "Texto", tooltip, 16, Vector2.zero, ((RectTransform)bubble.transform).sizeDelta - new Vector2(12f, 4f),
                TMPro.TextAlignmentOptions.Center, HoloColors.Text, TMPro.FontStyles.Bold);
            _tip = bubble.gameObject.AddComponent<CanvasGroup>();
            _tip.alpha = 0f;
            _tip.blocksRaycasts = false;
            _tip.interactable = false;
        }

        private bool Interactable => _selectable == null || _selectable.IsInteractable();

        public void OnPointerEnter(PointerEventData e)
        {
            if (!Interactable) return;
            _hover = true;
            if (sounds) HoloFeedback.Play(HoloFeedback.Sound.Hover);
            HoloFeedback.Haptic(e, 0.12f, 0.03f);
        }

        public void OnPointerExit(PointerEventData e)
        {
            _hover = false;
            _pressed = false;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (Interactable) _pressed = true;
        }

        public void OnPointerUp(PointerEventData e) => _pressed = false;

        public void OnPointerClick(PointerEventData e)
        {
            if (!Interactable) return;
            if (sounds) HoloFeedback.Play(HoloFeedback.Sound.Click);
            HoloFeedback.Haptic(e, 0.35f, 0.06f);
        }

        private void OnDisable()
        {
            _hover = _pressed = false;
            _current = 1f;
            transform.localScale = _baseScale;
            if (_halo != null) _halo.color = new Color(0.75f, 0.95f, 1f, 0f);
            if (_tip != null) _tip.alpha = 0f;
        }

        private void Update()
        {
            float target = !Interactable ? 1f : _pressed ? 0.97f : _hover ? hoverScale : 1f;
            _current = Mathf.Lerp(_current, target, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 18f));
            transform.localScale = _baseScale * _current;

            if (_halo != null)
            {
                float k = Mathf.Clamp01(Mathf.InverseLerp(1f, hoverScale, _current));
                _halo.color = new Color(0.75f, 0.95f, 1f, k * 0.8f);
            }
            if (_tip != null)
                _tip.alpha = Mathf.MoveTowards(_tip.alpha, _hover ? 1f : 0f, Time.unscaledDeltaTime * 6f);
        }
    }
}
