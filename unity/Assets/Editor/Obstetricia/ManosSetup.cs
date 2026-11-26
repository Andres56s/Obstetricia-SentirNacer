#if UNITY_EDITOR
using Obstetricia.Core;
using Obstetricia.Modules;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Hands;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace ObstetriciaSetup
{
    /// <summary>Conecta el agarre preciso (contacto real de los dedos) a los interactores de las manos.</summary>
    public static class ManosSetup
    {
        [MenuItem("Tools/MR Setup/9 - Agarre preciso con las manos")]
        public static void Aplicar()
        {
            int n = 0;
            foreach (NearFarInteractor i in Object.FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string hand = i.transform.parent != null ? i.transform.parent.name : "";
                if (hand != "Left Hand" && hand != "Right Hand") continue;
                bool left = hand == "Left Hand";
                Lector(i, "AgarreObjetos", "m_SelectInput", left, true);
                Lector(i, "PulsarBotones", "m_UIPressInput", left, false);
                n++;
            }
            foreach (TutorialController t in Object.FindObjectsByType<TutorialController>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Tutorial(t);
            EditorSceneManager.MarkAllScenesDirty();
            if (!Application.isPlaying) EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Obstetricia] Agarre preciso conectado en {n} manos.");
        }

        /// <summary>Modelo y materiales de la mano animada del tutorial.</summary>
        public static void Tutorial(TutorialController t)
        {
            var so = new SerializedObject(t);
            so.FindProperty("handModel").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Hands/1.10.0/HandVisualizer/Models/RightHand.fbx");
            so.FindProperty("handMaterial").objectReferenceValue = Mat("ManoTutorial", "MR/ManoHolo");
            so.FindProperty("glowMaterial").objectReferenceValue = Mat("BrilloTutorial", "Universal Render Pipeline/Unlit");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Material Mat(string name, string shader)
        {
            string path = "Assets/Obstetricia/Materials/" + name + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find(shader));
                AssetDatabase.CreateAsset(m, path);
            }
            return m;
        }

        private static void Lector(NearFarInteractor i, string name, string property, bool left, bool puno)
        {
            Transform t = i.transform.Find(name);
            if (t == null)
            {
                t = new GameObject(name).transform;
                t.SetParent(i.transform, false);
            }
            var l = t.GetComponent<LectorMano>();
            if (l == null) l = t.gameObject.AddComponent<LectorMano>();
            var so = new SerializedObject(l);
            so.FindProperty("mano").intValue = (int)(left ? Handedness.Left : Handedness.Right);
            so.FindProperty("aceptaPuno").boolValue = puno;
            so.FindProperty("interactor").objectReferenceValue = i;
            so.ApplyModifiedPropertiesWithoutUndo();

            var si = new SerializedObject(i);
            SerializedProperty p = si.FindProperty(property);
            p.FindPropertyRelative("m_InputSourceMode").intValue = (int)XRInputButtonReader.InputSourceMode.ObjectReference;
            p.FindPropertyRelative("m_ObjectReferenceObject").objectReferenceValue = l;
            si.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(i);
        }
    }
}
#endif
