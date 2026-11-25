using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace Obstetricia.Data
{
    /// <summary>Materiales y recursos visuales compartidos (asignados por Tools > MR Setup > 5).</summary>
    [CreateAssetMenu(menuName = "Obstetricia/Tema", fileName = "ObstetriciaTheme")]
    public class ObstetriciaTheme : ScriptableObject
    {
        [Header("Mesa UCN")]
        public Material pedestalBody;
        public Material pedestalTop;
        public Material glow;
        [Tooltip("Logo UCEN (círculo azul con letras). Se aplica sobre el disco de la mesa. Si falta se escriben las letras.")]
        public Texture2D ucnLogo;
        [Tooltip("Círculo del logo dentro de la imagen, en UV: centro (x, y) y radio (z).")]
        public Vector3 logoCircle = new Vector3(0.501f, 0.499f, 0.436f);

        [Tooltip("Letras 3D del logo en el orden U, C, E, N (para la animación de entrada).")]
        public Mesh[] logoLetras;
        public Material logoLetra;
        [Tooltip("Azul del logo.")]
        public Color logoAzul = new Color32(0, 38, 255, 255);

        [Header("Anatomía")]
        public Material bone;
        public Material boneDetail;
        [Tooltip("Modelo detallado de la pelvis ósea femenina (prefab/FBX). Si falta se usa un modelo provisional.")]
        public GameObject pelvisModel;
        [Tooltip("Pelvis translúcida para ver la cabeza fetal en Mecanismos del parto.")]
        public Material boneGhost;
        public Material fetalHead;

        [Header("Otros")]
        public Material video360;
        public Material line;

        [Header("Tarjetas del menú principal")]
        public ProgramCard fisioCard = new ProgramCard { title = "PROGRAMA FISIO-BALÓN Y MÉTODOS NO FARMACOLÓGICOS DEL DOLOR", iconId = "ball" };
        public ProgramCard legalCard = new ProgramCard { title = "PROGRAMA LEGAL: VIOLENCIA OBSTÉTRICA / LEY INTEGRAL DE LA MUJER", iconId = "scale" };
    }
}
