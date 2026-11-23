using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace Obstetricia.UI
{
    /// <summary>Paleta del estilo holográfico (azul UCN + cian).</summary>
    public static class HoloColors
    {
        public static readonly Color PanelFill = new Color(0.03f, 0.09f, 0.2f, 0.86f);
        public static readonly Color PanelFillLight = new Color(0.08f, 0.2f, 0.38f, 0.82f);
        public static readonly Color Glass = new Color(0.55f, 0.75f, 0.95f, 0.16f);
        public static readonly Color Border = new Color(0.25f, 0.72f, 1f, 1f);
        public static readonly Color Accent = new Color(0.45f, 0.85f, 1f, 1f);
        public static readonly Color Text = new Color(0.95f, 0.98f, 1f, 1f);
        public static readonly Color TextDim = new Color(0.72f, 0.82f, 0.92f, 1f);
        public static readonly Color Correct = new Color(0.3f, 0.9f, 0.45f, 1f);
        public static readonly Color Wrong = new Color(1f, 0.35f, 0.3f, 1f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.3f, 1f);
        public static readonly Color ButtonFill = new Color(0.07f, 0.22f, 0.42f, 0.95f);
        public static readonly Color Selected = new Color(0.12f, 0.42f, 0.75f, 0.95f);
    }

    /// <summary>
    /// Fábrica de UI uGUI en world space para XR: canvas, paneles con borde luminoso, textos TMP,
    /// botones e iconos. Unidades: 1 unidad de canvas = 1 mm (escala 0.001) salvo que se indique otra escala.
    /// </summary>
    public static class HoloUI
    {
        public const float MillimeterScale = 0.001f;

        /// <summary>Crea un canvas world-space interactuable con rayos y poke de XRI.</summary>
        public static Canvas CreateCanvas(Transform parent, string name, Vector2 size, float scale = MillimeterScale)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI") >= 0 ? LayerMask.NameToLayer("UI") : 0;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.localScale = Vector3.one * scale;

            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = Camera.main;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 2f;
            scaler.referencePixelsPerUnit = 100f;
            go.AddComponent<TrackedDeviceGraphicRaycaster>();
            go.AddComponent<CanvasGroup>();
            var appear = go.AddComponent<HoloAppear>();
            // Paneles hermanos aparecen escalonados, uno tras otro.
            appear.delay = Mathf.Min(0.3f, (go.transform.GetSiblingIndex()) * 0.06f);
            return canvas;
        }

        /// <summary>RectTransform hijo con ancla y pivote centrados.</summary>
        public static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Estira un RectTransform para cubrir a su padre con márgenes (izq, der, arriba, abajo).</summary>
        public static RectTransform Stretch(RectTransform rt, float left = 0, float right = 0, float top = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        /// <summary>Panel holográfico: relleno translúcido + borde con brillo. Devuelve el panel (bloquea rayos).</summary>
        public static HoloPanel Panel(Transform parent, string name, Vector2 position, Vector2 size,
            Color? fill = null, Color? border = null)
        {
            RectTransform rt = Rect(parent, name, position, size);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = HoloSprites.Fill;
            bg.type = Image.Type.Sliced;
            bg.color = fill ?? HoloColors.PanelFill;
            bg.raycastTarget = true;

            RectTransform outlineRt = Rect(rt, "Outline", Vector2.zero, Vector2.zero);
            int m = HoloSprites.OutlineMargin;
            Stretch(outlineRt, -m, -m, -m, -m);
            var outline = outlineRt.gameObject.AddComponent<Image>();
            outline.sprite = HoloSprites.Outline;
            outline.type = Image.Type.Sliced;
            outline.color = border ?? HoloColors.Border;
            outline.raycastTarget = false;

            var panel = rt.gameObject.AddComponent<HoloPanel>();
            panel.Init(bg, outline);
            return panel;
        }

        /// <summary>Texto TMP con caja fija.</summary>
        public static TextMeshProUGUI Text(Transform parent, string name, string text, float fontSize, Vector2 position,
            Vector2 size, TextAlignmentOptions align = TextAlignmentOptions.Center, Color? color = null,
            FontStyles style = FontStyles.Normal)
        {
            RectTransform rt = Rect(parent, name, position, size);
            var tmp = rt.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.color = color ?? HoloColors.Text;
            tmp.fontStyle = style;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Truncate;
            tmp.raycastTarget = false;
            return tmp;
        }

        /// <summary>Imagen de icono tintada.</summary>
        public static Image Icon(Transform parent, string name, string iconId, Vector2 position, float size, Color? color = null)
        {
            RectTransform rt = Rect(parent, name, position, new Vector2(size, size));
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = HoloSprites.Icon(iconId);
            img.color = color ?? HoloColors.Accent;
            img.preserveAspect = true;
            img.raycastTarget = false;
            return img;
        }

        /// <summary>
        /// Foto con marco redondeado. La foto llena el marco sin deformarse (recorta los bordes que sobran).
        /// </summary>
        public static Image Picture(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
        {
            HoloPanel frame = Panel(parent, name, position, size, new Color(0.02f, 0.06f, 0.14f, 0.95f), HoloColors.Border * 0.8f);
            frame.Background.raycastTarget = false;
            RectTransform viewport = Stretch(Rect(frame.transform, "Viewport", Vector2.zero, Vector2.zero), 6, 6, 6, 6);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform picRt = Rect(viewport, "Picture", Vector2.zero, viewport.rect.size);
            var pic = picRt.gameObject.AddComponent<Image>();
            pic.raycastTarget = false;
            pic.preserveAspect = false;
            var fitter = picRt.gameObject.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            SetPicture(pic, sprite);
            return pic;
        }

        /// <summary>Cambia la foto de un Picture (null = marco vacío).</summary>
        public static void SetPicture(Image pic, Sprite sprite)
        {
            pic.sprite = sprite;
            pic.enabled = sprite != null;
            var fitter = pic.GetComponent<AspectRatioFitter>();
            if (fitter != null && sprite != null && sprite.rect.height > 0f)
                fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
        }

        /// <summary>Botón holográfico con texto y/o icono.</summary>
        public static HoloButton Button(Transform parent, string name, string label, Vector2 position, Vector2 size,
            UnityAction onClick, string iconId = null, float fontSize = 22f, Color? accent = null)
        {
            HoloPanel panel = Panel(parent, name, position, size, HoloColors.ButtonFill, accent ?? HoloColors.Border);
            var button = panel.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = panel.Background;
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            colors.highlightedColor = new Color(1.6f, 1.6f, 1.6f, 1f);
            colors.pressedColor = new Color(2.2f, 2.2f, 2.2f, 1f);
            colors.selectedColor = colors.normalColor;
            colors.colorMultiplier = 1.5f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            var nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
            if (onClick != null) button.onClick.AddListener(onClick);

            Image icon = null;
            TextMeshProUGUI text = null;
            bool hasLabel = !string.IsNullOrEmpty(label);
            float iconSize = Mathf.Min(size.y * 0.6f, 48f);
            float textLeft = -size.x / 2f + 12f;
            if (!string.IsNullOrEmpty(iconId))
            {
                float x = hasLabel ? -size.x / 2f + iconSize * 0.5f + 14f : 0f;
                icon = Icon(panel.transform, "Icon", iconId, new Vector2(x, 0f), iconSize, accent ?? HoloColors.Accent);
                textLeft = x + iconSize * 0.5f + 8f;
            }
            if (hasLabel)
            {
                float textRight = size.x / 2f - 12f;
                text = Text(panel.transform, "Label", label, fontSize, new Vector2((textLeft + textRight) / 2f, 0f),
                    new Vector2(textRight - textLeft, size.y - 8f), TextAlignmentOptions.Center,
                    accent ?? HoloColors.Text, FontStyles.Bold);
                text.enableAutoSizing = true;
                text.fontSizeMax = fontSize;
                text.fontSizeMin = fontSize * 0.6f;
            }

            var fx = panel.gameObject.AddComponent<HoloHoverFx>();
            fx.hoverScale = Mathf.Max(size.x, size.y) > 250f ? 1.025f : 1.06f;

            var hb = panel.gameObject.AddComponent<HoloButton>();
            hb.Init(button, panel, text, icon);
            return hb;
        }

        /// <summary>Barra de progreso horizontal. Devuelve la imagen de relleno (fillAmount 0..1).</summary>
        public static Image ProgressBar(Transform parent, string name, Vector2 position, Vector2 size)
        {
            RectTransform rt = Rect(parent, name, position, size);
            var back = rt.gameObject.AddComponent<Image>();
            back.sprite = HoloSprites.Fill;
            back.type = Image.Type.Sliced;
            back.pixelsPerUnitMultiplier = 3f;
            back.color = new Color(1f, 1f, 1f, 0.12f);
            back.raycastTarget = false;

            RectTransform fillRt = Stretch(Rect(rt, "Fill", Vector2.zero, Vector2.zero));
            var fill = fillRt.gameObject.AddComponent<Image>();
            fill.sprite = HoloSprites.Fill;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            fill.fillAmount = 0f;
            fill.color = HoloColors.Accent;
            fill.raycastTarget = false;
            return fill;
        }

        /// <summary>Dibuja un canvas por encima de los demás paneles (ventanas emergentes).</summary>
        public static void BringToFront(Canvas canvas, int order = 50)
        {
            canvas.overrideSorting = true;
            canvas.sortingOrder = order;
        }

        /// <summary>
        /// Coloca un objeto frente a la cabeza del usuario (distancia en m, desplazamiento vertical en m e
        /// inclinación hacia atrás en grados), mirando al usuario.
        /// </summary>
        public static void PlaceInFront(Transform t, float distance, float down = 0f, float tilt = 0f)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Transform head = cam.transform;
            Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
            t.position = head.position + forward * distance + Vector3.up * down;
            t.rotation = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(tilt, 0f, 0f);
        }

        /// <summary>Orienta un transform para que su cara visible mire al punto del usuario (solo giro en Y).</summary>
        public static void FaceTowards(Transform t, Vector3 viewerWorldPosition)
        {
            Vector3 dir = t.position - viewerWorldPosition;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
        }

        /// <summary>Destruye todos los hijos (válido en Play y en Edit Mode).</summary>
        public static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                GameObject child = t.GetChild(i).gameObject;
                if (Application.isPlaying) UnityEngine.Object.Destroy(child);
                else UnityEngine.Object.DestroyImmediate(child);
            }
        }
    }
}
