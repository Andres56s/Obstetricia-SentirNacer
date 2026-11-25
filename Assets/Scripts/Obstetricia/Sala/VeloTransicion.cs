using System;
using System.Collections;
using Obstetricia.UI;
using UnityEngine;

namespace Obstetricia.Sala
{
    /// <summary>
    /// Transición cómoda entre realidad mixta y sala virtual: un velo claro cubre la vista, ocurre el cambio y el velo
    /// se abre. La cámara nunca se mueve, así que no produce mareo.
    /// </summary>
    public class VeloTransicion : MonoBehaviour
    {
        [SerializeField] private Material material;

        private Transform _sphere;
        private Material _mat;

        public bool Busy { get; private set; }

        private void Build()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Velo";
            Destroy(go.GetComponent<Collider>());
            _sphere = go.transform;
            _sphere.SetParent(transform, false);
            _sphere.localScale = Vector3.one;
            var r = go.GetComponent<MeshRenderer>();
            _mat = new Material(material != null ? material.shader : Shader.Find("MR/VeloTransicion"));
            if (material != null) _mat.CopyPropertiesFromMaterial(material);
            r.sharedMaterial = _mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.SetActive(false);
        }

        /// <summary>Cubre la vista, ejecuta "middle" y vuelve a abrirla. Devuelve false si ya hay una transición en curso.</summary>
        public bool Run(float cover, float open, Action middle, Action done = null)
        {
            if (Busy) return false;
            if (_sphere == null) Build();
            Busy = true;
            StartCoroutine(Routine(cover, open, middle, done));
            return true;
        }

        private IEnumerator Routine(float cover, float open, Action middle, Action done)
        {
            _mat.SetFloat("_Abre", 0f);
            _mat.SetFloat("_Cubre", 0f);
            _sphere.gameObject.SetActive(true);
            yield return Animate("_Cubre", cover);
            try { middle?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[Obstetricia] Error en la transición: {ex}"); }
            // Un momento quieto: deja cargar la sala y da tiempo a los ojos a adaptarse.
            yield return new WaitForSecondsRealtime(0.25f);
            yield return Animate("_Abre", open);
            _sphere.gameObject.SetActive(false);
            Busy = false;
            done?.Invoke();
        }

        private IEnumerator Animate(string property, float seconds)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Mathf.Min(Time.unscaledDeltaTime, 0.05f) / seconds;
                _mat.SetFloat(property, Ease.InOutSine(t));
                yield return null;
            }
        }

        private void LateUpdate()
        {
            if (_sphere == null || !_sphere.gameObject.activeSelf || Camera.main == null) return;
            _sphere.SetPositionAndRotation(Camera.main.transform.position, Quaternion.identity);
        }
    }
}
