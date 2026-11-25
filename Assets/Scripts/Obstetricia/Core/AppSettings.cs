using System;
using UnityEngine;

namespace Obstetricia.Core
{
    /// <summary>
    /// Preferencias del usuario guardadas en el visor (PlayerPrefs): comodidad, accesibilidad y estudiante actual.
    /// </summary>
    public static class AppSettings
    {
        private const string Prefix = "ucen_";

        /// <summary>Se dispara cuando cambia cualquier ajuste.</summary>
        public static event Action Changed;

        /// <summary>Modo sentada: baja los paneles y los acerca.</summary>
        public static bool Seated
        {
            get => GetBool("seated", false);
            set => SetBool("seated", value);
        }

        /// <summary>Ajuste fino de altura (m), -0.3 .. +0.3.</summary>
        public static float HeightOffset
        {
            get => PlayerPrefs.GetFloat(Prefix + "height", 0f);
            set { PlayerPrefs.SetFloat(Prefix + "height", Mathf.Clamp(value, -0.3f, 0.3f)); Save(); }
        }

        /// <summary>Distancia (m) entre el usuario y la experiencia, 0.7 .. 1.6.</summary>
        public static float Distance
        {
            get => PlayerPrefs.GetFloat(Prefix + "distance", 1.1f);
            set { PlayerPrefs.SetFloat(Prefix + "distance", Mathf.Clamp(value, 0.7f, 1.6f)); Save(); }
        }

        /// <summary>Modo zurdo: el lápiz se usa con la mano izquierda.</summary>
        public static bool LeftHanded
        {
            get => GetBool("lefthanded", false);
            set => SetBool("lefthanded", value);
        }

        /// <summary>Sonidos de la interfaz.</summary>
        public static bool Sounds
        {
            get => GetBool("sounds", true);
            set => SetBool("sounds", value);
        }

        /// <summary>Narración por voz de las situaciones y pasos. Apagada por defecto: se activa en Ajustes.</summary>
        public static bool Narration
        {
            get => GetBool("narration", false);
            set => SetBool("narration", value);
        }

        /// <summary>Si ya se completó (o saltó) el tutorial inicial.</summary>
        public static bool TutorialDone
        {
            get => GetBool("tutorial", false);
            set => SetBool("tutorial", value);
        }

        /// <summary>Nombre del estudiante que está usando el visor.</summary>
        public static string StudentName
        {
            get => PlayerPrefs.GetString(Prefix + "student", "");
            set { PlayerPrefs.SetString(Prefix + "student", (value ?? "").Trim()); Save(); }
        }

        /// <summary>Identificador del ancla espacial guardada de la mesa (vacío = ninguna).</summary>
        public static string SavedAnchor
        {
            get => PlayerPrefs.GetString(Prefix + "anchor", "");
            set { PlayerPrefs.SetString(Prefix + "anchor", value ?? ""); PlayerPrefs.Save(); }
        }

        /// <summary>Altura final de la experiencia sobre el suelo según los ajustes.</summary>
        public static float EffectiveHeight => HeightOffset + (Seated ? -0.35f : 0f);

        /// <summary>Distancia final según los ajustes (sentada = algo más cerca).</summary>
        public static float EffectiveDistance => Seated ? Mathf.Max(0.7f, Distance - 0.15f) : Distance;

        private static bool GetBool(string key, bool fallback) => PlayerPrefs.GetInt(Prefix + key, fallback ? 1 : 0) == 1;

        private static void SetBool(string key, bool value)
        {
            PlayerPrefs.SetInt(Prefix + key, value ? 1 : 0);
            Save();
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
