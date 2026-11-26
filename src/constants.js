// Valores compartidos por toda la aplicación.

// Los 3 perfiles de usuario. Estos textos son los que se guardan en Firestore
// (users/{uid}.role) y los que comparan las reglas de seguridad (firestore.rules).
export const ROLES = {
  ALUMNO: 'alumno',
  PROFESOR: 'profesor',
  ADMIN: 'admin', // Administrador / Programador
};

// Texto que se muestra en pantalla para cada rol.
export const ROLE_LABELS = {
  alumno: 'Alumno',
  profesor: 'Profesor',
  admin: 'Administrador',
};

// A qué pantalla se envía a cada rol justo después de iniciar sesión.
export const homeForRole = (role) => (role === ROLES.ALUMNO ? '/dashboard' : '/panel');

// ---------------------------------------------------------------------
// PARÁMETROS DE LAS EVALUACIONES
// ---------------------------------------------------------------------
// Porcentaje mínimo para considerar aprobada una prueba.
export const PASS_PERCENT = 60;

// Tiempo límite de la PRUEBA final (examen), en minutos.
// 0 = sin límite. Si pones, por ejemplo, 30: aparece un contador y, al llegar a 0,
// el examen se envía solo con lo que el alumno haya respondido.
// (El test del apartado Práctico nunca tiene límite de tiempo ni de intentos.)
export const FINAL_EXAM_TIME_LIMIT_MIN = 0;

// ---------------------------------------------------------------------
// REALIDAD INMERSIVA (proyecto Unity de la carpeta ../unity)
// ---------------------------------------------------------------------
// Programas de la experiencia Unity que un curso inmersivo puede abrir directamente.
// `id` es el valor que recibe Unity en la URL (?modulo=...). Vacío = la mesa UCEN (el alumno elige).
export const IMMERSIVE_MODULES = [
  { id: '', label: 'Mesa UCEN (el alumno elige el programa)' },
  { id: 'fisio', label: 'Programa Fisio-balón (pelvis 3D, mecanismos del parto, evaluación)' },
  { id: 'legal', label: 'Programa Legal (Ley Integral de la Mujer)' },
  { id: 'sala', label: 'Sala virtual de obstetricia (cama de parto, monitor fetal, Apgar, musicoterapia)' },
];

export const immersiveModuleLabel = (id) =>
  (IMMERSIVE_MODULES.find((m) => m.id === (id || '')) || IMMERSIVE_MODULES[0]).label;

// Dónde queda la versión web (WebGL) del proyecto Unity. Unity la genera con
// "Tools > Obstetricia > Construir versión Web (WebGL)" dentro de web/public/unity.
export const UNITY_WEB_PATH = '/unity/index.html';

// Enlace opcional para descargar la app de las Meta Quest (APK). Se configura en .env.local:
// VITE_QUEST_APP_URL=https://...
export const QUEST_APP_URL = import.meta.env.VITE_QUEST_APP_URL || '';
