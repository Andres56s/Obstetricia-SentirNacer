using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Obstetricia.UI
{
    /// <summary>
    /// Animación holográfica de aparición: el elemento se materializa (fundido + escala con rebote suave)
    /// y una franja de luz lo recorre de arriba abajo. Se reproduce sola cada vez que el objeto se activa.
    /// También ofrece una salida animada antes de desactivar.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class HoloAppear : MonoBehaviour
    {
        [Tooltip("Segundos de espera antes de aparecer (para escalonar paneles).")]
        public float delay;
        [Tooltip("Mostrar la franja de escaneo al aparecer.")]
        public bool scan = true;
        public float duration = 0.38f;

        private CanvasGroup _group;
        private Vector3 _baseScale;
        private bool _hasBase;
        private RectTransform _scanBar;
        private Coroutine _running;

        private void Awake() => Init();

        private void Init()
        {
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (!_hasBase)
            {
                _baseScale = transform.localScale;
                _hasBase = true;
            }
        }

        private void OnEnable() => Play(delay);

        private void OnDisable()
        {
            _running = null;
            if (_hasBase) transform.localScale = _baseScale;
            if (_group != null) _group.alpha = 1f;
            if (_scanBar != null) _scanBar.gameObject.SetActive(false);
        }

        /// <summary>Reproduce la aparición (útil para "refrescar" un panel cuyo contenido cambió).</summary>
        public void Play(float wait = 0f)
        {
            Init();
            if (!isActiveAndEnabled) return;
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(AppearRoutine(wait));
        }

        /// <summary>Desvanece y luego desactiva el objeto.</summary>
        public void Hide(Action done = null)
        {
            Init();
            if (!isActiveAndEnabled)
            {
                gameObject.SetActive(false);
                done?.Invoke();
                return;
            }
            if (_running != null) StopCoroutine(_running);
            _running = StartCoroutine(HideRoutine(done));
        }

        /// <summary>Desactiva con animación cualquier objeto (si tiene HoloAppear) o al instante si no.</summary>
        public static void HideObject(GameObject go, Action done = null)
        {
            if (go == null) return;
            var appear = go.GetComponent<HoloAppear>();
            if (appear != null) appear.Hide(done);
            else
            {
                go.SetActive(false);
                done?.Invoke();
            }
        }

        private IEnumerator AppearRoutine(float wait)
        {
            _group.alpha = 0f;
            transform.localScale = _baseScale * 0.9f;
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            if (scan) StartScan();

            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / duration;
                float e = Ease.OutBack(Mathf.Clamp01(t), 1.2f);
                transform.localScale = _baseScale * Mathf.LerpUnclamped(0.9f, 1f, e);
                // Parpadeo holográfico breve al encender.
                float flicker = t < 0.35f ? (Mathf.Sin(t * 90f) * 0.15f + 0.85f) : 1f;
                _group.alpha = Mathf.Clamp01(Ease.OutCubic(Mathf.Clamp01(t * 1.6f))) * flicker;
                yield return null;
            }
            transform.localScale = _baseScale;
            _group.alpha = 1f;
            _running = null;
        }

        private IEnumerator HideRoutine(Action done)
        {
            float t = 0f;
            const float d = 0.2f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime / d;
                float e = Ease.InCubic(Mathf.Clamp01(t));
                transform.localScale = _baseScale * Mathf.Lerp(1f, 0.92f, e);
                _group.alpha = 1f - e;
                yield return null;
            }
            _running = null;
            gameObject.SetActive(false);
            done?.Invoke();
        }

        private void StartScan()
        {
            var rt = transform as RectTransform;
            if (rt == null || rt.rect.height < 120f) return;
            if (_scanBar == null)
            {
                var go = new GameObject("Escaneo", typeof(RectTransform));
                go.layer = gameObject.layer;
                go.transform.SetParent(transform, false);
                _scanBar = (RectTransform)go.transform;
                _scanBar.anchorMin = new Vector2(0f, 0.5f);
                _scanBar.anchorMax = new Vector2(1f, 0.5f);
                _scanBar.sizeDelta = new Vector2(0f, 36f);
                var img = go.AddComponent<Image>();
                img.sprite = HoloSprites.Scan;
                img.color = new Color(0.55f, 0.9f, 1f, 0.45f);
                img.raycastTarget = false;
            }
            _scanBar.SetAsLastSibling();
            StartCoroutine(ScanRoutine(rt.rect.height));
        }

        private IEnumerator ScanRoutine(float height)
        {
            _scanBar.gameObject.SetActive(true);
            float t = 0f;
            const float d = 0.55f;
            while (t < 1f && _scanBar != null)
            {
                t += Time.unscaledDeltaTime / d;
                _scanBar.anchoredPosition = new Vector2(0f, Mathf.Lerp(height / 2f, -height / 2f, Ease.InOutSine(t)));
                yield return null;
            }
            if (_scanBar != null) _scanBar.gameObject.SetActive(false);
        }
    }

    /// <summary>Curvas de suavizado para animaciones.</summary>
    public static class Ease
    {
        public static float OutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
        public static float InCubic(float t) => t * t * t;
        public static float InOutSine(float t) => -(Mathf.Cos(Mathf.PI * Mathf.Clamp01(t)) - 1f) / 2f;

        public static float OutBack(float t, float overshoot = 1.70158f)
        {
            float c3 = overshoot + 1f;
            return 1f + c3 * Mathf.Pow(t - 1f, 3f) + overshoot * Mathf.Pow(t - 1f, 2f);
        }
    }
}
