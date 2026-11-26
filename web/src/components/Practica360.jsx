import React, { Suspense, lazy, useEffect, useState } from 'react';
import { Glasses, Monitor, X, Pencil, Trash2, Play, Move3d, ExternalLink } from 'lucide-react';
import { toEmbedUrl } from '../utils/video';

// El visor propio usa three.js (pesado): se descarga solo al abrir un video que tenga .mp4 360.
const Video360Viewer = lazy(() => import('./Video360Viewer'));

// =====================================================================
// PRÁCTICO: GALERÍA DE VIDEOS 360°
// =====================================================================
// Cada video se muestra como una tarjeta. Al elegirlo se abre una ventana con dos opciones:
//   - "Ver video": en la pantalla, arrastrando para mirar alrededor (YouTube 360 o el visor propio).
//   - "Ver en VR": con un visor. Si el video tiene .mp4 propio, se abre dentro de la página (WebXR, p. ej.
//     desde el navegador de las Meta Quest). Si solo es de YouTube, se abre en YouTube, que en las Quest
//     lo muestra en YouTube VR.
// Los videos vienen de data/videos360.js (incluidos en la página) y de los bloques de Práctico que los
// profesores agregan desde el panel.
// =====================================================================

const youtubeId = (url) => url?.match(/(?:youtu\.be\/|youtube\.com\/(?:watch\?v=|embed\/|shorts\/))([\w-]{11})/)?.[1];

const Miniatura = ({ video }) => {
  const id = youtubeId(video.videoUrl);
  return (
    <div className="relative aspect-video bg-gradient-to-br from-[#2a1340] to-primary overflow-hidden">
      {id && (
        <img
          src={`https://img.youtube.com/vi/${id}/hqdefault.jpg`}
          alt=""
          className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-500"
        />
      )}
      <div className="absolute inset-0 bg-black/25 group-hover:bg-black/10 transition-colors" />
      <span className="absolute top-3 left-3 flex items-center gap-1 text-xs font-bold bg-white/90 text-primary-dark px-2 py-1 rounded-full">
        <Move3d size={14} /> 360°
      </span>
      <span className="absolute inset-0 flex items-center justify-center">
        <span className="w-14 h-14 rounded-full bg-white/90 text-primary flex items-center justify-center shadow-lg group-hover:scale-110 transition-transform">
          <Play size={26} className="ml-1" />
        </span>
      </span>
    </div>
  );
};

/** Ventana del video: primero se elige cómo verlo, luego se reproduce. */
const ReproductorVideo = ({ video, onClose }) => {
  const [modo, setModo] = useState(null); // null (elegir) | 'video' | 'vr'
  const embed = toEmbedUrl(video.videoUrl);
  const propio = Boolean(video.video360Url);

  useEffect(() => {
    const alTeclado = (e) => e.key === 'Escape' && onClose();
    window.addEventListener('keydown', alTeclado);
    return () => window.removeEventListener('keydown', alTeclado);
  }, [onClose]);

  const elegir = (m) => {
    // Solo YouTube: "Ver video" lo abre en YouTube, donde el 360° se mueve arrastrando (dentro de la página,
    // el reproductor incrustado de YouTube no siempre deja mirar alrededor).
    if (m === 'video' && video.videoUrl) {
      window.open(video.videoUrl, '_blank', 'noopener');
      return;
    }
    setModo(m);
  };

  return (
    <div className="fixed inset-0 z-50 bg-black/70 flex items-center justify-center p-4" onClick={onClose}>
      <div className="bg-white rounded-2xl w-full max-w-4xl overflow-hidden shadow-2xl" onClick={(e) => e.stopPropagation()}>
        <div className="flex items-start justify-between gap-4 px-6 pt-5 pb-3">
          <div>
            <p className="text-xs uppercase tracking-wider text-primary font-semibold flex items-center gap-1">
              <Move3d size={14} /> Video 360°
            </p>
            <h3 className="text-xl font-semibold text-primary-dark">{video.title}</h3>
          </div>
          <button onClick={onClose} aria-label="Cerrar" className="text-gray-400 hover:text-gray-700 mt-1">
            <X size={24} />
          </button>
        </div>

        {modo === null && (
          <div className="px-6 pb-6">
            {video.text && <p className="text-gray-600 mb-5 whitespace-pre-line">{video.text}</p>}
            <div className="grid sm:grid-cols-2 gap-4">
              <button
                onClick={() => elegir('video')}
                className="group text-left border-2 border-gray-100 hover:border-primary rounded-xl p-5 transition-colors"
              >
                <Monitor size={30} className="text-primary mb-3" />
                <p className="font-semibold text-gray-800 text-lg flex items-center gap-1.5">
                  Ver video {video.videoUrl && <ExternalLink size={15} className="text-gray-400" />}
                </p>
                <p className="text-sm text-gray-500">
                  {video.videoUrl
                    ? 'Se abre en YouTube. Arrastra con el mouse (o mueve el celular) para mirar alrededor.'
                    : 'En esta pantalla. Arrastra con el mouse (o mueve el celular) para mirar alrededor.'}
                </p>
              </button>
              <button
                onClick={() => elegir('vr')}
                className="group text-left border-2 border-gray-100 hover:border-primary rounded-xl p-5 transition-colors"
              >
                <Glasses size={30} className="text-primary mb-3" />
                <p className="font-semibold text-gray-800 text-lg">Ver en VR</p>
                <p className="text-sm text-gray-500">
                  Con el visor conectado al computador (Quest Link) o desde el navegador de las Meta Quest: el video te
                  rodea y se mueve con tu cabeza.
                </p>
              </button>
            </div>
          </div>
        )}

        {modo !== null && (
          <div className="px-6 pb-6">
            {propio ? (
              <Suspense
                fallback={
                  <div className="aspect-video rounded-lg bg-black text-white/70 text-sm flex items-center justify-center">
                    Cargando visor 360°…
                  </div>
                }
              >
                <Video360Viewer url={video.video360Url} guiaTexto={video.guia360} title={video.title} modoVR={modo === 'vr'} />
              </Suspense>
            ) : (
              // Video solo en YouTube: su reproductor no se puede llevar al visor desde la página (WebXR).
              <div className="rounded-xl bg-[#faf8fb] border border-primary/20 p-5 text-sm text-gray-700 space-y-3">
                <p className="flex items-center gap-2 font-semibold text-primary-dark">
                  <Glasses size={20} /> Este video todavía está solo en YouTube
                </p>
                <p>
                  Para verlo en VR desde la página, con el video moviéndose con tu cabeza, se necesita el archivo del video
                  (.mp4). YouTube no permite usar su reproductor dentro de un visor desde otra página.
                </p>
                <p className="font-medium">Cuando el video esté cargado, para verlo con el visor conectado al computador:</p>
                <ol className="list-decimal pl-5 space-y-1">
                  <li>Conecta las Meta Quest al computador con <b>Quest Link</b> (cable o Air Link), igual que para Unity.</li>
                  <li>Abre esta página en <b>Chrome</b> o <b>Edge</b> en el computador.</li>
                  <li>
                    Elige el video, <b>Ver en VR</b> y <b>Entrar al visor VR</b>. El gatillo del control pausa y reanuda.
                  </li>
                </ol>
                <p className="text-gray-500">
                  Mientras tanto, en las Meta Quest puedes abrir el video en la app <b>YouTube VR</b>, que sí lo muestra en
                  360° alrededor tuyo.
                </p>
                {embed && (
                  <a
                    href={video.videoUrl}
                    target="_blank"
                    rel="noopener noreferrer"
                    className="inline-flex items-center gap-1.5 text-primary font-medium hover:underline"
                  >
                    <ExternalLink size={16} /> Abrir en YouTube
                  </a>
                )}
              </div>
            )}
            <button onClick={() => setModo(null)} className="mt-3 text-sm text-primary hover:underline">
              ← Elegir otra forma de verlo
            </button>
          </div>
        )}
      </div>
    </div>
  );
};

const Practica360 = ({ videos, onEdit, onDelete }) => {
  const [abierto, setAbierto] = useState(null);

  if (videos.length === 0) return null;

  return (
    <>
      <div className="grid sm:grid-cols-2 gap-6">
        {videos.map((video) => (
          <article key={video.id} className="bg-white rounded-xl shadow-sm border border-gray-100 overflow-hidden relative">
            <button onClick={() => setAbierto(video)} className="group block w-full text-left">
              <Miniatura video={video} />
              <div className="p-4">
                <h3 className="font-semibold text-primary-dark pr-14">{video.title}</h3>
                {video.text && <p className="text-sm text-gray-500 mt-1 line-clamp-2">{video.text}</p>}
                <p className="flex items-center gap-3 text-xs text-gray-400 mt-3">
                  <span className="flex items-center gap-1">
                    <Monitor size={14} /> Ver video
                  </span>
                  <span className="flex items-center gap-1">
                    <Glasses size={14} /> Ver en VR
                  </span>
                </p>
              </div>
            </button>
            {/* Solo los videos agregados desde el panel se editan aquí (los incluidos están en data/videos360.js). */}
            {!video.incluido && (onEdit || onDelete) && (
              <div className="absolute bottom-4 right-4 flex gap-3">
                {onEdit && (
                  <button onClick={() => onEdit(video)} aria-label="Editar" className="text-primary hover:text-primary-dark">
                    <Pencil size={18} />
                  </button>
                )}
                {onDelete && (
                  <button onClick={() => onDelete(video)} aria-label="Eliminar" className="text-red-500 hover:text-red-700">
                    <Trash2 size={18} />
                  </button>
                )}
              </div>
            )}
          </article>
        ))}
      </div>
      {abierto && <ReproductorVideo video={abierto} onClose={() => setAbierto(null)} />}
    </>
  );
};

export default Practica360;
