using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace Obstetricia.Data
{
    /// <summary>Contenido de un quiz de decisiones (módulo Ley Integral o Evaluación).</summary>
    [CreateAssetMenu(menuName = "Obstetricia/Quiz", fileName = "Quiz")]
    public class QuizData : ScriptableObject
    {
        public string moduleLabel = "MÓDULO EDUCATIVO";
        public string title = "LEY INTEGRAL DE LA MUJER";
        public string subtitle = "Conoce tus derechos. Decidir informada es tu poder.";
        [Tooltip("Icono del encabezado: scale, clipboard, ball, cube…")]
        public string iconId = "scale";
        public int pointsPerCorrect = 100;
        public string motto = "Aprende, decide y protege tus derechos.";
        public string mottoDetail = "Cada decisión te acerca a un parto respetado";
        [Tooltip("Video 360° de ambientación de todo el módulo (opcional).")]
        public Video360Source environment;
        public QuizQuestion[] questions = Array.Empty<QuizQuestion>();
    }
}
