using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace Obstetricia.Data
{
    /// <summary>
    /// Fuente de un video 360° (equirectangular 2:1). Usa el VideoClip si está asignado; si no, el archivo
    /// o URL. Una ruta relativa se busca en StreamingAssets (recomendado en Quest para videos pesados).
    /// </summary>
    [Serializable]
    public class Video360Source
    {
        [Tooltip("Clip importado en el proyecto (opcional).")]
        public VideoClip clip;

        [Tooltip("Archivo en StreamingAssets (p. ej. 'Videos360/trabajo_de_parto.mp4') o URL http(s).")]
        public string fileOrUrl;

        [Tooltip("Giro (grados) para alinear el frente del video con el usuario.")]
        public float yawOffset;

        public bool IsValid => clip != null || !string.IsNullOrWhiteSpace(fileOrUrl);

        /// <summary>URL lista para VideoPlayer.url (null si se usa clip).</summary>
        public string ResolveUrl()
        {
            if (clip != null || string.IsNullOrWhiteSpace(fileOrUrl)) return null;
            string value = fileOrUrl.Trim();
            if (value.Contains("://") || Path.IsPathRooted(value)) return value;
            return Path.Combine(Application.streamingAssetsPath, value).Replace('\\', '/');
        }
    }

    /// <summary>Una alternativa de respuesta (tarjeta con foto) en un escenario de quiz.</summary>
    [Serializable]
    public class QuizOption
    {
        [Tooltip("Frase principal de la tarjeta.")]
        public string title;
        [TextArea(2, 4)] public string description;
        [Tooltip("Foto de la tarjeta (opcional).")]
        public Sprite image;
    }

    /// <summary>Un escenario o pregunta del quiz.</summary>
    [Serializable]
    public class QuizQuestion
    {
        [TextArea(2, 5)] public string situation;
        public string question;
        public QuizOption[] options = new QuizOption[2];
        [Tooltip("Índice (0 = Opción A) de la respuesta correcta.")]
        public int correctIndex;
        [Tooltip("Video 360° de ambientación para este escenario (opcional; reemplaza al del módulo).")]
        public Video360Source environment;
    }

    public enum FisioSectionKind
    {
        /// <summary>Explicación en video 360°.</summary>
        Video360,
        /// <summary>Modelo 3D de la pelvis; solo se activa al detectar la pelota.</summary>
        Anatomy,
        /// <summary>Quiz de evaluación.</summary>
        Evaluation,
        /// <summary>Mecanismos del parto animados en 3D sobre la pelvis.</summary>
        Mechanisms,
    }

    /// <summary>Una sección del menú lateral del programa Fisio-balón.</summary>
    [Serializable]
    public class FisioSection
    {
        public string menuLabel = "Sección";
        [Tooltip("Icono: cube, pulse, ball, target, clipboard…")]
        public string iconId = "cube";
        public FisioSectionKind kind = FisioSectionKind.Video360;
        [Tooltip("Texto del campo 'Actividad' del panel Notas (vacío = nombre del menú).")]
        public string activity;
        public Video360Source video;
    }

    /// <summary>Un programa que aparece como tarjeta sobre la mesa UCEN.</summary>
    [Serializable]
    public class ProgramCard
    {
        public string title;
        public Sprite image;
        public string iconId = "ball";
    }

}
