using Obstetricia.Data;
using UnityEngine;
using UnityEngine.Video;

namespace Obstetricia.Media
{
    /// <summary>
    /// Reproduce videos 360° (equirectangulares) en una esfera invertida centrada en la cabeza del usuario.
    /// No agrega interfaz propia: lo controlan los módulos (menú lateral y barra de herramientas).
    /// </summary>
    public class Video360Player : MonoBehaviour
    {
        [SerializeField, Tooltip("Material URP/Unlit con Render Face = Back (lo asigna el setup).")]
        private Material sphereMaterial;
        [SerializeField] private Transform head;
        [SerializeField, Tooltip("Radio de la esfera (m).")]
        private float radius = 12f;

        private VideoPlayer _player;
        private AudioSource _audio;
        private RenderTexture _texture;
        private GameObject _sphere;
        private Material _material;

        /// <summary>True mientras hay un video visible.</summary>
        public bool IsActive => _sphere != null && _sphere.activeSelf;

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
            _player = gameObject.AddComponent<VideoPlayer>();
            _player.playOnAwake = false;
            _player.renderMode = VideoRenderMode.RenderTexture;
            _player.audioOutputMode = VideoAudioOutputMode.AudioSource;
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _player.SetTargetAudioSource(0, _audio);
            _player.prepareCompleted += OnPrepared;
            _player.errorReceived += (vp, msg) =>
            {
                Debug.LogError($"[Obstetricia] Error de video 360: {msg}");
                // Sin video (archivo faltante o formato no soportado) se vuelve al entorno en vez de dejar la esfera negra.
                Stop();
            };
            BuildSphere();
            _sphere.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_texture != null) _texture.Release();
            if (_material != null) Destroy(_material);
        }

        /// <summary>Reproduce un video 360°. Devuelve false si la fuente no está asignada.</summary>
        public bool Play(Video360Source source, bool loop)
        {
            if (source == null || !source.IsValid)
            {
                Debug.LogWarning("[Obstetricia] Video 360 sin asignar.");
                return false;
            }
            _player.Stop();
            _player.isLooping = loop;
            string url = source.ResolveUrl();
#if !UNITY_ANDROID || UNITY_EDITOR
            if (url != null && !url.Contains("://") && !System.IO.File.Exists(url))
            {
                Debug.LogWarning($"[Obstetricia] No existe el video 360: {url}");
                return false;
            }
#endif
            if (url != null)
            {
                _player.source = VideoSource.Url;
                _player.url = url;
            }
            else
            {
                _player.source = VideoSource.VideoClip;
                _player.clip = source.clip;
            }
            _sphere.transform.rotation = Quaternion.Euler(0f, source.yawOffset + (head != null ? head.eulerAngles.y : 0f), 0f);
            _sphere.SetActive(true);
            _material.SetColor("_BaseColor", Color.black);
            _player.Prepare();
            return true;
        }

        /// <summary>Detiene el video y vuelve al passthrough.</summary>
        public void Stop()
        {
            _player.Stop();
            _sphere.SetActive(false);
        }

        private void OnPrepared(VideoPlayer vp)
        {
            int w = (int)Mathf.Max(256, vp.width);
            int h = (int)Mathf.Max(128, vp.height);
            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) _texture.Release();
                _texture = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32) { name = "Video360", useMipMap = false };
                _texture.Create();
            }
            vp.targetTexture = _texture;
            _material.SetTexture("_BaseMap", _texture);
            _material.SetColor("_BaseColor", Color.white);
            vp.Play();
        }

        private void LateUpdate()
        {
            if (_sphere != null && _sphere.activeSelf && head != null)
                _sphere.transform.position = head.position;
        }

        private void BuildSphere()
        {
            _sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sphere.name = "Video360Sphere";
            Destroy(_sphere.GetComponent<Collider>());
            _sphere.transform.SetParent(transform, false);
            _sphere.transform.localScale = Vector3.one * radius * 2f;

            if (sphereMaterial != null) _material = new Material(sphereMaterial);
            else
            {
                _material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                _material.SetFloat("_Cull", 1f); // Front → se ve el interior
            }
            // La esfera se ve por dentro: invertir U para que el video no quede en espejo.
            _material.SetTextureScale("_BaseMap", new Vector2(-1f, 1f));
            _material.SetTextureOffset("_BaseMap", new Vector2(1f, 0f));
            var renderer = _sphere.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
    }
}
