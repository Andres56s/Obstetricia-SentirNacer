#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Herramienta de editor que agrega a la escena una planicie azul de 50x50 unidades.
/// Uso: menú GameObject > 3D Object > Planicie Azul 50x50.
/// </summary>
public static class CrearPlanicieAzul
{
    private const float Tamano = 50f;
    private const string RutaMaterial = "Assets/Materials/Azul.mat";

    /// <summary>Crea (o reutiliza) el material azul compatible con URP.</summary>
    private static Material ObtenerMaterialAzul()
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterial);
        if (material != null) return material;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                        ?? Shader.Find("Standard");
        material = new Material(shader) { name = "Azul" };
        var azul = new Color(0.1f, 0.35f, 0.9f, 1f);
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", azul);
        if (material.HasProperty("_Color")) material.SetColor("_Color", azul);

        AssetDatabase.CreateAsset(material, RutaMaterial);
        AssetDatabase.SaveAssets();
        return material;
    }

    /// <summary>Crea la planicie de 50x50 en el origen de la escena activa.</summary>
    [MenuItem("GameObject/3D Object/Planicie Azul 50x50")]
    public static void Crear()
    {
        try
        {
            // El Plane primitivo de Unity mide 10x10 con escala 1.
            GameObject planicie = GameObject.CreatePrimitive(PrimitiveType.Plane);
            planicie.name = "Planicie Azul 50x50";
            float escala = Tamano / 10f;
            planicie.transform.position = Vector3.zero;
            planicie.transform.localScale = new Vector3(escala, 1f, escala);
            planicie.GetComponent<Renderer>().sharedMaterial = ObtenerMaterialAzul();

            Undo.RegisterCreatedObjectUndo(planicie, "Crear Planicie Azul");
            Selection.activeGameObject = planicie;
            EditorUtility.SetDirty(planicie);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"Planicie de {Tamano}x{Tamano} creada.");
        }
        catch (Exception ex)
        {
            Debug.LogError($"No se pudo crear la planicie azul: {ex.Message}");
        }
    }
}
#endif
