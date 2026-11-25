using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>Pequeñas animaciones de ambiente: respiración del recién nacido, calor que late y goteo del suero.</summary>
    public class VidaSuave : MonoBehaviour
    {
        public enum Tipo { Respiracion, Calor, Goteo }

        [SerializeField] private Tipo tipo;
        [SerializeField, Tooltip("Objeto que respira o gota que cae.")] private Transform objetivo;
        [SerializeField, Tooltip("Recorrido de la gota (m).")] private float caida = 0.1f;

        private Light _luz;
        private float _base;
        private Vector3 _pos, _escala;

        private void Awake()
        {
            _luz = GetComponent<Light>();
            if (_luz != null) _base = _luz.intensity;
            if (objetivo == null) return;
            _pos = objetivo.localPosition;
            _escala = objetivo.localScale;
        }

        private void Update()
        {
            float t = Time.time;
            switch (tipo)
            {
                case Tipo.Respiracion: // unas 42 respiraciones por minuto, rango normal del recién nacido (40 a 60)
                    float r = Mathf.Sin(t * 42f / 60f * Mathf.PI * 2f);
                    objetivo.localScale = Vector3.Scale(_escala, new Vector3(1f + 0.004f * r, 1f + 0.016f * r, 1f));
                    break;
                case Tipo.Calor:
                    if (_luz != null) _luz.intensity = _base * (1f + 0.12f * Mathf.Sin(t * 1.3f));
                    break;
                case Tipo.Goteo:
                    float k = Mathf.Repeat(t, 1.4f) / 1.4f;
                    float grow = Mathf.Clamp01(k / 0.55f), fall = Mathf.Clamp01((k - 0.55f) / 0.25f);
                    objetivo.localPosition = _pos + Vector3.down * (caida * fall * fall);
                    objetivo.localScale = _escala * (k < 0.8f ? Mathf.Max(0.01f, grow) : 0.01f);
                    break;
            }
        }
    }
}
