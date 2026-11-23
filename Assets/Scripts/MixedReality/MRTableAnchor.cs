using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace MRGame
{
    /// <summary>
    /// Coloca el tablero de juego sobre una mesa real detectada (planos horizontales de Meta Quest)
    /// y lo ancla al espacio con un AR Anchor. Si no hay mesa, usa una mesa virtual frente al usuario.
    /// </summary>
    public class MRTableAnchor : MonoBehaviour
    {
        [Header("Referencias (asignadas por Tools > MR Setup > 4)")]
        [SerializeField] private ARPlaneManager planeManager;
        [SerializeField] private ARAnchorManager anchorManager;
        [SerializeField] private XROrigin xrOrigin;
        [Tooltip("Patas de la mesa virtual; solo se activan si no se encuentra una mesa real.")]
        [SerializeField] private GameObject legs;

        [Header("Ajustes")]
        [SerializeField] private float searchTimeoutSeconds = 8f;
        [SerializeField] private float minPlaneArea = 0.35f;
        [SerializeField] private float minTableHeight = 0.45f;
        [SerializeField] private float maxTableHeight = 1.2f;
        [SerializeField] private float fallbackDistance = 0.8f;
        [SerializeField] private float fallbackHeight = 0.85f;

        private const string ScenePermission = "com.oculus.permission.USE_SCENE";
        private ARAnchor _anchor;

        /// <summary>Se dispara al terminar la colocación: pose final y si se usó una mesa real.</summary>
        public event Action<Pose, bool> Placed;

        /// <summary>True cuando el tablero ya fue colocado.</summary>
        public bool IsPlaced { get; private set; }

        private async void Start()
        {
            try
            {
                await RequestScenePermissionAsync();
                Pose? tablePose = await FindTablePoseAsync();
                bool isRealTable = tablePose.HasValue;
                Pose pose = tablePose ?? ComputeFallbackPose();

                if (legs != null) legs.SetActive(!isRealTable);
                transform.SetPositionAndRotation(pose.position, pose.rotation);
                await CreateAnchorAsync(pose);

                IsPlaced = true;
                Debug.Log($"[MRGame] Tablero colocado ({(isRealTable ? "mesa real" : "mesa virtual de respaldo")}) en {pose.position}");
                Placed?.Invoke(pose, isRealTable);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRGame] Error al colocar el tablero: {ex}");
            }
        }

        private void LateUpdate()
        {
            // El anclaje corrige la deriva del tracking: el tablero (y sus hijos) siguen al ancla.
            if (_anchor != null && _anchor.trackingState != TrackingState.None)
                transform.SetPositionAndRotation(_anchor.transform.position, _anchor.transform.rotation);
        }

        /// <summary>Pide el permiso de datos de escena (planos) en Quest.</summary>
        private static async Awaitable RequestScenePermissionAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(ScenePermission))
            {
                Permission.RequestUserPermission(ScenePermission);
                float waited = 0f;
                while (!Permission.HasUserAuthorizedPermission(ScenePermission) && waited < 15f)
                {
                    await Awaitable.NextFrameAsync();
                    waited += Time.unscaledDeltaTime;
                }
            }
#endif
            await Awaitable.NextFrameAsync();
        }

        /// <summary>Espera a que aparezca una mesa (o un plano horizontal con altura de mesa).</summary>
        private async Awaitable<Pose?> FindTablePoseAsync()
        {
            if (planeManager == null) return null;
            planeManager.enabled = true;

            float elapsed = 0f;
            float firstFound = -1f;
            while (elapsed < searchTimeoutSeconds)
            {
                ARPlane best = PickBestPlane();
                if (best != null)
                {
                    if (firstFound < 0f) firstFound = elapsed;
                    if (elapsed - firstFound > 0.5f) return PoseFromPlane(best);
                }
                await Awaitable.NextFrameAsync();
                elapsed += Time.unscaledDeltaTime;
            }
            return null;
        }

        /// <summary>Elige primero planos clasificados como mesa y, si no, el horizontal más grande a altura de mesa.</summary>
        private ARPlane PickBestPlane()
        {
            float floorY = xrOrigin != null ? xrOrigin.transform.position.y : 0f;
            ARPlane best = null;
            float bestScore = 0f;

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane.alignment != PlaneAlignment.HorizontalUp) continue;
                float area = plane.size.x * plane.size.y;
                float height = plane.transform.position.y - floorY;
                bool isTable = (plane.classifications & PlaneClassifications.Table) != 0;
                bool plausible = area >= minPlaneArea && height >= minTableHeight && height <= maxTableHeight;
                if (!isTable && !plausible) continue;

                float score = area + (isTable ? 100f : 0f);
                if (score > bestScore) { bestScore = score; best = plane; }
            }
            return best;
        }

        /// <summary>Pose del tablero: centro del plano, mirando hacia el usuario.</summary>
        private Pose PoseFromPlane(ARPlane plane)
        {
            Vector3 center = plane.transform.TransformPoint(new Vector3(plane.center.x, 0f, plane.center.y));
            return new Pose(center, FacingAwayFromUser(center));
        }

        /// <summary>Mesa virtual a fallbackDistance metros frente al usuario, a fallbackHeight sobre el suelo.</summary>
        private Pose ComputeFallbackPose()
        {
            Transform head = HeadTransform();
            Vector3 flatForward = Vector3.ProjectOnPlane(head.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < 0.001f) flatForward = Vector3.forward;
            float floorY = xrOrigin != null ? xrOrigin.transform.position.y : 0f;
            Vector3 position = head.position + flatForward * fallbackDistance;
            position.y = floorY + fallbackHeight;
            return new Pose(position, Quaternion.LookRotation(flatForward, Vector3.up));
        }

        private Quaternion FacingAwayFromUser(Vector3 tableCenter)
        {
            Vector3 toTable = Vector3.ProjectOnPlane(tableCenter - HeadTransform().position, Vector3.up);
            return toTable.sqrMagnitude < 0.001f ? Quaternion.identity : Quaternion.LookRotation(toTable.normalized, Vector3.up);
        }

        private Transform HeadTransform()
        {
            if (xrOrigin != null && xrOrigin.Camera != null) return xrOrigin.Camera.transform;
            return Camera.main != null ? Camera.main.transform : transform;
        }

        /// <summary>Crea un AR Anchor en la pose indicada; si falla, el tablero queda fijo en el mundo.</summary>
        private async Awaitable CreateAnchorAsync(Pose pose)
        {
            if (anchorManager == null)
            {
                Debug.LogWarning("[MRGame] Sin ARAnchorManager: el tablero no queda anclado.");
                return;
            }

            var result = await anchorManager.TryAddAnchorAsync(pose);
            if (result.status.IsSuccess() && result.value != null)
            {
                _anchor = result.value;
                Debug.Log("[MRGame] AR Anchor creado para el tablero.");
            }
            else
            {
                Debug.LogWarning("[MRGame] No se pudo crear el AR Anchor; el tablero queda fijo en el mundo.");
            }
        }
    }
}
