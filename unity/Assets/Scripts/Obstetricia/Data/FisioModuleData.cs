using System;
using System.IO;
using UnityEngine;
using UnityEngine.Video;

namespace Obstetricia.Data
{
    /// <summary>Contenido del programa Fisio-balón y métodos no farmacológicos del dolor.</summary>
    [CreateAssetMenu(menuName = "Obstetricia/Programa Fisio-balón", fileName = "FisioBalon")]
    public class FisioModuleData : ScriptableObject
    {
        public string programTitle = "PROGRAMA FISIO-BALÓN";
        public string studentName = "Estudiante";
        public FisioSection[] sections = Array.Empty<FisioSection>();
        public QuizData evaluation;
    }
}
