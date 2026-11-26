// =====================================================================
// VIDEOS 360° INCLUIDOS EN LA PÁGINA (apartado Práctico)
// =====================================================================
// Videos que aparecen siempre en el Práctico de un curso, sin tener que agregarlos desde el panel.
// La clave es el id del curso (el de la URL: /curso/<id>/practico).
// Los profesores pueden sumar más videos con "Agregar video 360°" (esos se guardan en Firestore).
//
// Campos de cada video:
//   title     Título de la tarjeta.
//   text      Descripción corta (opcional).
//   videoUrl  Enlace de YouTube (se ve en 360° arrastrando; en las Quest se abre en YouTube VR).
//   video360Url  (opcional) .mp4 360° para "Ver en VR" dentro de la página (WebXR), con guía. Puede ser un
//                archivo de public/videos360/ (p. ej. '/videos360/mi-video.mp4', máx. ~100 MB para GitHub) o un
//                enlace directo con CORS (p. ej. Firebase Storage). Google Drive y YouTube no sirven aquí.
//   guia360   (opcional) Guía del video: una línea por punto "minuto:segundo | texto | pausa".
// =====================================================================

export const VIDEOS_360 = {
  // Fisio-balón en el Trabajo de Parto
  1: [
    {
      id: 'fisiobalon-practica-360',
      title: 'Práctica con fisio-balón',
      text: 'Video 360° de la práctica con fisio-balón (UCEN). Mira a tu alrededor para seguir cada movimiento.',
      videoUrl: 'https://www.youtube.com/watch?v=BPlModwMZG4',
      // Copia del mismo video (1080p, 30 MB) servida por la propia página: permite "Ver en VR" con el visor.
      video360Url: '/videos360/practica-fisiobalon.mp4',
    },
  ],
};

/** Videos incluidos de un curso (lista vacía si no tiene). */
export const videosIncluidos = (courseId) => VIDEOS_360[courseId] || [];
