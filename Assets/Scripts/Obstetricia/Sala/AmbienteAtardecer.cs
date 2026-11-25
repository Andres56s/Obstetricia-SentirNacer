using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Atardecer de la sala: orienta el sol hacia el ventanal, da volumen a los objetos con luz ambiente direccional y
    /// captura los reflejos de la sala. Al salir restaura la iluminación de la realidad mixta.
    /// </summary>
    public class AmbienteAtardecer : MonoBehaviour
    {
        [SerializeField] private Color colorSol = new Color(1f, 0.81f, 0.62f);
        [SerializeField] private float intensidadSol = 1.9f;
        [SerializeField, Tooltip("Hacia dónde viaja la luz, en el espacio de la sala.")] private Vector3 direccion = new Vector3(0.332f, -0.242f, 0.912f);
        [SerializeField] private Color ambiente = new Color(0.42f, 0.33f, 0.25f);
        [SerializeField] private Color luzVentanal = new Color(1f, 0.80f, 0.62f);
        [SerializeField] private Color rebotePiso = new Color(0.75f, 0.52f, 0.36f);
        [SerializeField] private float distanciaSombras = 7f;
        [SerializeField, Tooltip("Hueco del ventanal: x0, x1, y0, y1.")] private Vector4 ventana = new Vector4(-2.38f, 2.42f, 0.65f, 2.25f);
        [SerializeField, Tooltip("X de los tres montantes y altura del travesaño.")] private Vector4 marcos = new Vector4(-1.18f, 0.02f, 1.22f, 1.82f);
        [SerializeField] private float zVentanal = -2f;
        [SerializeField] private ReflectionProbe reflejos;

        private static readonly int IdLocal = Shader.PropertyToID("_SalaLocal"), IdSol = Shader.PropertyToID("_SalaSol"),
            IdVentana = Shader.PropertyToID("_SalaVentana"), IdMarcos = Shader.PropertyToID("_SalaMarcos");
        private Light _sun;
        private bool _on;
        private float _intensity, _shadowDistance, _shadowStrength, _reflection;
        private Color _color, _ambientColor;
        private Quaternion _rotation;
        private AmbientMode _mode;
        private SphericalHarmonicsL2 _probe;
        private UniversalRenderPipelineAsset _rp;

        public void Activar()
        {
            if (_on) return;
            if (_sun == null)
                foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type == LightType.Directional) { _sun = l; break; }
            _on = true;
            _mode = RenderSettings.ambientMode;
            _ambientColor = RenderSettings.ambientLight;
            _probe = RenderSettings.ambientProbe;
            _reflection = RenderSettings.reflectionIntensity;
            _rp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_rp != null)
            {
                _shadowDistance = _rp.shadowDistance;
                _rp.shadowDistance = distanciaSombras;
            }
            if (_sun != null)
            {
                _intensity = _sun.intensity;
                _color = _sun.color;
                _rotation = _sun.transform.rotation;
                _shadowStrength = _sun.shadowStrength;
                _sun.intensity = intensidadSol;
                _sun.color = colorSol;
                _sun.shadowStrength = 1f;
            }
            Orientar();
        }

        /// <summary>Alinea el sol, la luz ambiente y los reflejos con la orientación actual de la sala.</summary>
        public void Orientar()
        {
            if (!_on) return;
            Vector3 dir = transform.TransformDirection(direccion.normalized);
            if (_sun != null) _sun.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

            Vector3 haciaSol = -direccion.normalized;
            Shader.SetGlobalMatrix(IdLocal, transform.worldToLocalMatrix);
            Shader.SetGlobalVector(IdSol, new Vector4(haciaSol.x, haciaSol.y, haciaSol.z, zVentanal));
            Shader.SetGlobalVector(IdVentana, ventana);
            Shader.SetGlobalVector(IdMarcos, marcos);

            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(ambiente);
            // Luz amplia desde el ventanal, rebote cálido del piso y relleno frío y tenue desde el fondo.
            sh.AddDirectionalLight(transform.TransformDirection(new Vector3(-0.1f, 0.25f, -1f).normalized), luzVentanal, 0.42f);
            sh.AddDirectionalLight(Vector3.down, rebotePiso, 0.20f);
            sh.AddDirectionalLight(transform.TransformDirection(new Vector3(0f, 0.6f, 1f).normalized), new Color(0.6f, 0.55f, 0.58f), 0.14f);
            RenderSettings.ambientMode = AmbientMode.Custom;
            RenderSettings.ambientProbe = sh;
        }

        /// <summary>Captura los reflejos (llamar con la sala ya visible y la vista cubierta por el velo).</summary>
        public void CapturarReflejos()
        {
            if (_on && reflejos != null) reflejos.RenderProbe();
        }

        public void Desactivar()
        {
            if (!_on) return;
            _on = false;
            RenderSettings.ambientMode = _mode;
            RenderSettings.ambientLight = _ambientColor;
            RenderSettings.ambientProbe = _probe;
            RenderSettings.reflectionIntensity = _reflection;
            if (_rp != null) _rp.shadowDistance = _shadowDistance;
            if (_sun == null) return;
            _sun.intensity = _intensity;
            _sun.color = _color;
            _sun.transform.rotation = _rotation;
            _sun.shadowStrength = _shadowStrength;
        }

        private void OnDestroy() => Desactivar(); // no dejar cambiado el asset de URP al detener Play
    }
}
