import React, { useEffect, useRef, useState } from 'react';
import { Maximize, CheckCircle2, AlertTriangle, Loader2 } from 'lucide-react';
import { saveImmersiveResult } from '../services/db';
import { UNITY_WEB_PATH } from '../constants';

// =====================================================================
// REPRODUCTOR DE LA EXPERIENCIA INMERSIVA (modo escritorio, sin visor)
// =====================================================================
// Muestra la versión WebGL del proyecto Unity (carpeta ../unity) dentro de un iframe.
//   Web -> Unity : parámetros en la URL  ?modulo=fisio|legal|sala&alumno=Nombre
//   Unity -> Web : window.postMessage({ type: 'obstetricia-resultado', module, correct, total, score })
//                  (ver unity/Assets/Scripts/Obstetricia/Desktop/WebBridge.cs)
// Cuando un ALUMNO termina una evaluación dentro de la experiencia, el resultado se guarda en
// Firestore (colección immersiveResults). Profesores/admin la usan en modo vista previa.
// =====================================================================

const toInt = (v) => (Number.isFinite(Number(v)) ? Math.max(0, Math.round(Number(v))) : null);

const ImmersivePlayer = ({ course, user, isStaff }) => {
  const frameRef = useRef(null);
  const [status, setStatus] = useState('checking'); // checking | missing | loading | ready
  const [lastResult, setLastResult] = useState(null);

  // ¿Existe la build WebGL? (En desarrollo, Vite responde index.html de la web aunque no exista,
  // por eso se revisa que el archivo sea realmente el cargador de Unity.)
  useEffect(() => {
    let cancelled = false;
    fetch(UNITY_WEB_PATH, { cache: 'no-store' })
      .then((r) => (r.ok ? r.text() : ''))
      .then((html) => {
        if (!cancelled) setStatus(html.includes('createUnityInstance') ? 'loading' : 'missing');
      })
      .catch(() => !cancelled && setStatus('missing'));
    return () => {
      cancelled = true;
    };
  }, []);

  // Mensajes que envía Unity desde el iframe.
  useEffect(() => {
    const onMessage = async (event) => {
      if (event.origin !== window.location.origin) return;
      if (!frameRef.current || event.source !== frameRef.current.contentWindow) return;
      const data = event.data || {};
      if (data.type === 'obstetricia-listo') {
        setStatus('ready');
        frameRef.current.focus();
        return;
      }
      if (data.type !== 'obstetricia-resultado') return;
      const correct = toInt(data.correct);
      const total = toInt(data.total);
      const score = toInt(data.score) ?? 0;
      if (correct === null || total === null || correct > total) return;
      const result = { module: String(data.module || 'Evaluación'), correct, total, score };
      if (isStaff) {
        setLastResult({ ...result, saved: false, preview: true });
        return;
      }
      try {
        await saveImmersiveResult(user, course, result);
        setLastResult({ ...result, saved: true });
      } catch (err) {
        console.error(err);
        setLastResult({ ...result, saved: false, error: true });
      }
    };
    window.addEventListener('message', onMessage);
    return () => window.removeEventListener('message', onMessage);
  }, [course, user, isStaff]);

  const params = new URLSearchParams();
  if (course.immersiveModule) params.set('modulo', course.immersiveModule);
  if (user?.name) params.set('alumno', user.name);
  const src = `${UNITY_WEB_PATH}?${params.toString()}`;

  const fullscreen = () => {
    const el = frameRef.current;
    if (el?.requestFullscreen) el.requestFullscreen().then(() => el.focus()).catch(() => {});
  };

  if (status === 'checking') {
    return <div className="text-center text-gray-500 py-16">Buscando la experiencia...</div>;
  }

  if (status === 'missing') {
    return (
      <div className="bg-amber-50 border border-amber-200 rounded-xl p-6 text-sm text-amber-900">
        <div className="flex items-center gap-2 font-semibold mb-2">
          <AlertTriangle size={18} /> La versión web de la experiencia aún no está publicada
        </div>
        <p className="mb-2">
          Hay que generarla una vez desde Unity: abre la carpeta <code className="bg-white px-1 rounded">unity</code> con
          Unity 6000.3.27f1 (con el módulo <em>Web Build Support</em>) y usa el menú{' '}
          <strong>Tools &gt; Obstetricia &gt; Construir versión Web (WebGL)</strong>.
        </p>
        <p>
          La build queda en <code className="bg-white px-1 rounded">web/public/unity</code> y esta página la mostrará
          automáticamente.
        </p>
      </div>
    );
  }

  return (
    <div>
      <div className="relative w-full aspect-video bg-[#0b1a33] rounded-xl overflow-hidden shadow-lg">
        <iframe
          ref={frameRef}
          src={src}
          title={`Experiencia inmersiva: ${course.title}`}
          className="w-full h-full border-0"
          allow="fullscreen; autoplay"
          allowFullScreen
          tabIndex={0}
        />
        {status !== 'ready' && (
          <div className="absolute inset-x-0 bottom-0 p-3 flex items-center justify-center gap-2 text-xs text-white/80 pointer-events-none">
            <Loader2 size={14} className="animate-spin" /> Cargando la experiencia (la primera vez puede tardar un poco)...
          </div>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 mt-3">
        <p className="text-xs text-gray-500">Haz clic dentro de la experiencia para usar el teclado.</p>
        <button
          onClick={fullscreen}
          className="inline-flex items-center gap-2 text-sm bg-primary hover:bg-primary-dark text-white px-4 py-2 rounded-md transition-colors"
        >
          <Maximize size={16} /> Pantalla completa
        </button>
      </div>

      {lastResult && (
        <div
          className={`mt-4 rounded-lg p-4 text-sm flex items-start gap-3 ${
            lastResult.error ? 'bg-red-50 text-red-700' : 'bg-green-50 text-green-800'
          }`}
        >
          <CheckCircle2 size={18} className="mt-0.5 shrink-0" />
          <div>
            <strong>{lastResult.module}:</strong> {lastResult.correct}/{lastResult.total} correctas
            {lastResult.total ? ` (${Math.round((lastResult.correct / lastResult.total) * 100)}%)` : ''}.{' '}
            {lastResult.saved && 'Tu resultado quedó registrado.'}
            {lastResult.preview && 'Modo vista previa: el resultado no se guarda.'}
            {lastResult.error && 'No se pudo guardar el resultado. Revisa tu conexión e inténtalo de nuevo.'}
          </div>
        </div>
      )}
    </div>
  );
};

export default ImmersivePlayer;
