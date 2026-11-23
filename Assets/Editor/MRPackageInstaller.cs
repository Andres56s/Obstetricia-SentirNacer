#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MRSetup
{
    /// <summary>
    /// Módulo 1 del setup de Realidad Mixta para Meta Quest 3/3S.
    /// Instala los paquetes XR con la API de Package Manager (resuelve la última versión
    /// compatible con la versión de Unity del proyecto). Uso: menú Tools > MR Setup > 1 - Instalar paquetes XR.
    /// </summary>
    public static class MRPackageInstaller
    {
        /// <summary>Paquetes a instalar. Sin @versión: Unity elige la última compatible.</summary>
        private static readonly string[] PackagesToAdd =
        {
            "com.unity.xr.management",
            "com.unity.xr.openxr",
            "com.unity.xr.interaction.toolkit",
            "com.unity.xr.hands",
            "com.unity.xr.arfoundation",
            "com.unity.xr.meta-openxr",
        };

        private const double TimeoutSeconds = 900;
        private static AddAndRemoveRequest _request;
        private static double _deadline;

        /// <summary>Lanza la instalación de todos los paquetes en una sola pasada de resolución.</summary>
        [MenuItem("Tools/MR Setup/1 - Instalar paquetes XR")]
        public static void Install()
        {
            try
            {
                if (_request != null && !_request.IsCompleted)
                {
                    Debug.LogWarning("[MRSetup] Ya hay una instalación en curso.");
                    return;
                }

                Debug.Log($"[MRSetup] Instalando: {string.Join(", ", PackagesToAdd)}");
                _request = Client.AddAndRemove(packagesToAdd: PackagesToAdd);
                _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
                EditorApplication.update += Poll;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MRSetup] No se pudo iniciar la instalación: {ex}");
            }
        }

        /// <summary>Sondea el estado de la solicitud hasta que termine o expire.</summary>
        private static void Poll()
        {
            try
            {
                if (_request == null) { EditorApplication.update -= Poll; return; }

                if (!_request.IsCompleted)
                {
                    if (EditorApplication.timeSinceStartup > _deadline)
                    {
                        EditorApplication.update -= Poll;
                        Debug.LogError("[MRSetup] Tiempo de espera agotado esperando a Package Manager.");
                    }
                    return;
                }

                EditorApplication.update -= Poll;
                if (_request.Status == StatusCode.Success)
                {
                    string lista = string.Join(", ", _request.Result.Select(p => $"{p.name}@{p.version}"));
                    Debug.Log($"[MRSetup] Paquetes resueltos: {lista}");
                }
                else
                {
                    Debug.LogError($"[MRSetup] Falló la instalación: {_request.Error?.message}");
                }
            }
            catch (Exception ex)
            {
                EditorApplication.update -= Poll;
                Debug.LogError($"[MRSetup] Error al sondear la instalación: {ex}");
            }
        }
    }
}
#endif
