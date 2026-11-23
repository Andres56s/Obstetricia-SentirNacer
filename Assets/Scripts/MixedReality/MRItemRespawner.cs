using System;
using System.Collections.Generic;
using UnityEngine;

namespace MRGame
{
    /// <summary>
    /// Devuelve a su posición inicial (relativa a este objeto) los Rigidbody hijos que caen fuera de la mesa
    /// o se alejan demasiado, para que el usuario nunca pierda los objetos lanzados.
    /// </summary>
    public class MRItemRespawner : MonoBehaviour
    {
        [SerializeField, Tooltip("Distancia vertical (m) bajo este objeto a partir de la cual se reaparece.")]
        private float fallThreshold = 1.2f;

        [SerializeField, Tooltip("Distancia horizontal máxima (m) antes de reaparecer.")]
        private float maxDistance = 6f;

        private sealed class Snapshot
        {
            public Rigidbody Body;
            public Vector3 LocalPosition;
            public Quaternion LocalRotation;
        }

        private readonly List<Snapshot> _items = new List<Snapshot>();

        private void Start()
        {
            try
            {
                foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>(true))
                {
                    _items.Add(new Snapshot
                    {
                        Body = rb,
                        LocalPosition = transform.InverseTransformPoint(rb.position),
                        LocalRotation = Quaternion.Inverse(transform.rotation) * rb.rotation,
                    });
                }
                Debug.Log($"[MRGame] Respawner registró {_items.Count} objetos.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRGame] Respawner: error al registrar objetos: {ex}");
            }
        }

        private void FixedUpdate()
        {
            foreach (Snapshot s in _items)
            {
                if (s.Body == null) continue;
                try
                {
                    Vector3 offset = s.Body.position - transform.position;
                    bool fell = offset.y < -fallThreshold;
                    bool far = new Vector2(offset.x, offset.z).magnitude > maxDistance;
                    if (fell || far) Respawn(s);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[MRGame] Respawner: error en {s.Body.name}: {ex}");
                }
            }
        }

        private void Respawn(Snapshot s)
        {
            s.Body.linearVelocity = Vector3.zero;
            s.Body.angularVelocity = Vector3.zero;
            s.Body.position = transform.TransformPoint(s.LocalPosition);
            s.Body.rotation = transform.rotation * s.LocalRotation;
            Debug.Log($"[MRGame] Reaparecido: {s.Body.name}");
        }
    }
}
