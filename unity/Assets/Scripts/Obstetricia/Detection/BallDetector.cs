using System;
using Unity.Collections;
using Unity.InferenceEngine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace Obstetricia.Detection
{
    /// <summary>
    /// Detecta una pelota de ejercicio (balón kinésico / fitball) en la imagen de la cámara passthrough
    /// de Meta Quest 3/3S usando un detector YOLO (COCO, clase 32 "sports ball") con Unity Inference.
    /// Estima la posición 3D de la pelota a partir de su tamaño aparente y de los intrínsecos de la cámara.
    ///
    /// Requisitos en el visor: OpenXR > Meta Quest: Camera (Passthrough) con "Camera Image Support",
    /// permiso horizonos.permission.HEADSET_CAMERA (lo agrega el paquete) y un modelo YOLO en ONNX.
    /// En el Editor (sin cámara) se puede simular la pelota con la tecla B.
    /// </summary>
    public class BallDetector : MonoBehaviour
    {
        [Header("Cámara")]
        [SerializeField] private ARCameraManager cameraManager;
        [SerializeField, Tooltip("Pose aproximada de la cámara passthrough (la cámara principal / centro de los ojos).")]
        private Transform head;

        [Header("Modelo YOLO (ONNX)")]
        [SerializeField, Tooltip("yolo11n / yolov8n exportado a ONNX (opset ≤ 15), entrada 640x640.")]
        private ModelAsset model;
        [SerializeField] private BackendType backend = BackendType.GPUCompute;
        [SerializeField, Tooltip("Clase COCO 'sports ball'.")]
        private int ballClassId = 32;
        [SerializeField, Range(0.05f, 0.95f)] private float scoreThreshold = 0.3f;
        [SerializeField, Tooltip("Ancho mínimo de la caja (fracción de la imagen) para descartar pelotas pequeñas.")]
        private float minBoxFraction = 0.06f;
        [SerializeField] private float inferenceInterval = 0.4f;
        [SerializeField, Tooltip("Transformación de la imagen CPU para que quede derecha (Quest: MirrorY).")]
        private XRCpuImage.Transformation imageTransformation = XRCpuImage.Transformation.MirrorY;

        [Header("Pelota")]
        [SerializeField, Tooltip("Diámetro real de la pelota (m). Balón kinésico típico: 0.55–0.75.")]
        private float ballDiameter = 0.65f;
        [SerializeField] private int detectionsToConfirm = 2;
        [SerializeField] private float lostAfterSeconds = 3f;

        [Header("Pruebas")]
        [SerializeField, Tooltip("En el Editor, la tecla B simula/quita una pelota frente al usuario.")]
        private bool simulateInEditor = true;

        private Worker _worker;
        private Tensor<float> _input;
        private Texture2D _frame;
        private int _inputSize = 640;
        private bool _busy;
        private float _nextInference;
        private int _consecutive;
        private float _lastSeen = -999f;
        private bool _simulated;

        /// <summary>True mientras la pelota está detectada (o simulada).</summary>
        public bool IsBallDetected { get; private set; }

        /// <summary>Posición estimada (mundo) del centro de la pelota.</summary>
        public Vector3 BallPosition { get; private set; }

        /// <summary>True si la detección automática puede funcionar (modelo y cámara disponibles).</summary>
        public bool CanDetect => model != null && cameraManager != null;

        /// <summary>Texto de estado legible para mostrar en la UI.</summary>
        public string StatusText { get; private set; } = "Detector inactivo";

        /// <summary>Mayor confianza de "pelota" en la última imagen analizada (aunque no supere el umbral). Para pruebas.</summary>
        public float LastBallScore { get; private set; }

        /// <summary>Se invoca cuando cambia IsBallDetected.</summary>
        public event Action<bool> DetectionChanged;

        /// <summary>True mientras el detector está buscando.</summary>
        public bool Running { get; private set; }

        private void Awake()
        {
            if (head == null && Camera.main != null) head = Camera.main.transform;
        }

        /// <summary>Empieza a buscar la pelota (carga el modelo la primera vez).</summary>
        public void Begin()
        {
            Running = true;
            if (Desktop.PlatformMode.IsDesktop)
            {
                // Sin visor no hay cámara del entorno: el balón kinésico se simula y la pelvis aparece de inmediato.
                SetSimulated(true);
                StatusText = "Modo escritorio: balón simulado";
                return;
            }
            if (model == null)
            {
                StatusText = Application.isEditor && simulateInEditor
                    ? "Sin modelo YOLO: presiona B en el Editor para simular la pelota"
                    : "Falta el modelo de detección (YOLO ONNX)";
                return;
            }
            if (_worker == null)
            {
                try
                {
                    Model runtime = ModelLoader.Load(model);
                    DynamicTensorShape shape = runtime.inputs[0].shape;
                    if (shape.rank == 4 && shape.Get(3) > 0) _inputSize = shape.Get(3);
                    _worker = new Worker(runtime, backend);
                    _input = new Tensor<float>(new TensorShape(1, 3, _inputSize, _inputSize));
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[Obstetricia] No se pudo cargar el modelo YOLO: {ex}");
                    StatusText = "Error al cargar el modelo de detección";
                    model = null;
                    return;
                }
            }
            StatusText = "Buscando pelota… mira hacia el balón";
        }

        /// <summary>Deja de buscar y olvida la detección.</summary>
        public void End()
        {
            Running = false;
            _simulated = false;
            _consecutive = 0;
            SetDetected(false);
            StatusText = "Detector inactivo";
        }

        /// <summary>Fuerza una pelota simulada (pruebas en el Editor o demostraciones).</summary>
        public void SetSimulated(bool on)
        {
            _simulated = on;
            if (on && head != null)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
                if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
                Vector3 p = head.position + fwd * 1.3f;
                p.y = Mathf.Max(0f, head.position.y - 1.5f) + ballDiameter / 2f;
                BallPosition = p;
            }
            SetDetected(on);
            StatusText = on ? "Pelota simulada" : "Buscando pelota…";
        }

        private void Update()
        {
            if (!Running) return;

            if (Application.isEditor && simulateInEditor && Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame)
                SetSimulated(!_simulated);
            if (_simulated) return;

            if (IsBallDetected && Time.time - _lastSeen > lostAfterSeconds)
            {
                _consecutive = 0;
                SetDetected(false);
                StatusText = "Pelota perdida: vuelve a mirarla";
            }

            if (_worker == null || _busy || Time.time < _nextInference) return;
            _nextInference = Time.time + inferenceInterval;
            RunDetection();
        }

        private async void RunDetection()
        {
            if (!cameraManager.TryAcquireLatestCpuImage(out XRCpuImage image))
            {
                StatusText = "Esperando imagen de la cámara (¿permiso de cámara concedido?)";
                return;
            }

            _busy = true;
            int imgW, imgH;
            XRCameraIntrinsics intrinsics;
            bool hasIntrinsics = cameraManager.TryGetIntrinsics(out intrinsics);
            Pose cameraPose = head != null ? new Pose(head.position, head.rotation) : Pose.identity;
            try
            {
                using (image)
                {
                    // Reducimos a ~640 px de ancho: suficiente para YOLO y barato de convertir.
                    int factor = Mathf.Max(1, image.width / _inputSize);
                    imgW = image.width / factor;
                    imgH = image.height / factor;
                    if (_frame == null || _frame.width != imgW || _frame.height != imgH)
                    {
                        if (_frame != null) Destroy(_frame);
                        _frame = new Texture2D(imgW, imgH, TextureFormat.RGBA32, false) { name = "PassthroughFrame" };
                    }
                    var conversion = new XRCpuImage.ConversionParams(image, TextureFormat.RGBA32, imageTransformation)
                    {
                        outputDimensions = new Vector2Int(imgW, imgH),
                    };
                    NativeArray<byte> raw = _frame.GetRawTextureData<byte>();
                    image.Convert(conversion, raw);
                    _frame.Apply(false);
                }

                TextureConverter.ToTensor(_frame, _input, new TextureTransform().SetCoordOrigin(CoordOrigin.TopLeft));
                _worker.Schedule(_input);
                var output = _worker.PeekOutput() as Tensor<float>;
                if (output == null) return;
                using (Tensor<float> result = await output.ReadbackAndCloneAsync())
                {
                    if (!Running) return;
                    if (TryFindBall(result, out Rect box, out float score))
                    {
                        // Caja en píxeles de entrada → píxeles de la imagen convertida.
                        float sx = imgW / (float)_inputSize, sy = imgH / (float)_inputSize;
                        Rect imgBox = new Rect(box.x * sx, box.y * sy, box.width * sx, box.height * sy);
                        if (imgBox.width / imgW >= minBoxFraction)
                            OnBallSeen(imgBox, imgW, imgH, hasIntrinsics ? intrinsics : (XRCameraIntrinsics?)null, cameraPose, score);
                    }
                    else
                    {
                        _consecutive = 0;
                        if (!IsBallDetected) StatusText = $"Buscando pelota… (confianza {LastBallScore:P0})";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Obstetricia] Error en la detección de la pelota: {ex}");
            }
            finally
            {
                _busy = false;
            }
        }

        /// <summary>
        /// Interpreta la salida YOLO. Soporta [1, 4+C, N] (YOLOv8/11), [1, N, 4+C] y [1, N, 6]
        /// (end-to-end: x1,y1,x2,y2,score,clase). Devuelve la caja (x,y = esquina superior izquierda) más probable.
        /// </summary>
        private bool TryFindBall(Tensor<float> tensor, out Rect best, out float bestScore)
        {
            best = default;
            bestScore = scoreThreshold;
            LastBallScore = 0f;
            bool found = false;
            TensorShape s = tensor.shape;
            if (s.rank != 3) return false;
            int a = s[1], b = s[2];
            float[] t = tensor.DownloadToArray();

            if (b == 6 || a > b)
            {
                // [1, N, 6] end-to-end o [1, N, 4+C]
                int n = a, stride = b;
                bool endToEnd = stride == 6;
                if (!endToEnd && 4 + ballClassId >= stride) return false;
                for (int i = 0; i < n; i++)
                {
                    int o = i * stride;
                    float score;
                    Rect r;
                    if (endToEnd)
                    {
                        if ((int)t[o + 5] != ballClassId) continue;
                        score = t[o + 4];
                        r = Rect.MinMaxRect(t[o], t[o + 1], t[o + 2], t[o + 3]);
                    }
                    else
                    {
                        score = t[o + 4 + ballClassId];
                        r = FromCenter(t[o], t[o + 1], t[o + 2], t[o + 3]);
                    }
                    if (score > LastBallScore) LastBallScore = score;
                    if (score > bestScore) { bestScore = score; best = r; found = true; }
                }
                return found;
            }

            // [1, 4+C, N] (YOLOv8 / YOLO11 / YOLO-World)
            int channels = a, count = b;
            int classes = channels - 4;
            // Modelo especializado (pocas clases, p. ej. "exercise ball", "yoga ball"): vale cualquiera de ellas.
            bool anyClass = classes <= 5;
            if (!anyClass && 4 + ballClassId >= channels) return false;
            for (int i = 0; i < count; i++)
            {
                float score = 0f;
                if (anyClass) { for (int c = 0; c < classes; c++) score = Mathf.Max(score, t[(4 + c) * count + i]); }
                else score = t[(4 + ballClassId) * count + i];
                if (score > LastBallScore) LastBallScore = score;
                if (score <= bestScore) continue;
                bestScore = score;
                best = FromCenter(t[i], t[count + i], t[2 * count + i], t[3 * count + i]);
                found = true;
            }
            return found;
        }

        private static Rect FromCenter(float cx, float cy, float w, float h) => new Rect(cx - w / 2f, cy - h / 2f, w, h);

        private void OnBallSeen(Rect box, int imgW, int imgH, XRCameraIntrinsics? intrinsics, Pose cameraPose, float score)
        {
            // Intrínsecos escalados a la imagen convertida; si no hay, se asume ~90° de FOV horizontal.
            float fx, fy, cx, cy;
            if (intrinsics.HasValue && intrinsics.Value.resolution.x > 0)
            {
                XRCameraIntrinsics k = intrinsics.Value;
                float kx = imgW / (float)k.resolution.x, ky = imgH / (float)k.resolution.y;
                fx = k.focalLength.x * kx; fy = k.focalLength.y * ky;
                cx = k.principalPoint.x * kx; cy = k.principalPoint.y * ky;
            }
            else
            {
                fx = fy = imgW / 2f;
                cx = imgW / 2f; cy = imgH / 2f;
            }

            float u = box.center.x, v = box.center.y; // v medido desde arriba
            Vector3 dir = new Vector3((u - cx) / fx, -(v - cy) / fy, 1f);
            float pixelDiameter = Mathf.Max(box.width, box.height);
            float distance = fx * ballDiameter / Mathf.Max(1f, pixelDiameter) * dir.magnitude;
            Vector3 world = cameraPose.position + cameraPose.rotation * dir.normalized * distance;

            BallPosition = IsBallDetected ? Vector3.Lerp(BallPosition, world, 0.35f) : world;
            _lastSeen = Time.time;
            _consecutive++;
            StatusText = $"Pelota detectada ({score:P0})";
            if (_consecutive >= detectionsToConfirm) SetDetected(true);
        }

        private void SetDetected(bool detected)
        {
            if (detected == IsBallDetected) return;
            IsBallDetected = detected;
            Debug.Log($"[Obstetricia] Pelota {(detected ? "detectada" : "no detectada")} en {BallPosition}");
            DetectionChanged?.Invoke(detected);
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
            _input?.Dispose();
            if (_frame != null) Destroy(_frame);
        }
    }
}
