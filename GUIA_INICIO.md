# Guía de inicio: modo escritorio y versión web

Esta guía explica cómo dejar funcionando en tu PC la rama **`modo-escritorio-web`**, que agrega a la
experiencia de las Quest una segunda forma de usarla: **sin visor**, con teclado y mouse, dentro de la página
web Sentir Nacer (Unity WebGL).

La versión para las Quest **sigue funcionando igual**: el modo escritorio solo se activa cuando no hay visor.

## Qué trae esta rama

- **Modo escritorio** (`Assets/Scripts/Obstetricia/Desktop/`): se crea solo al iniciar la escena cuando no hay
  visor. Apaga el rig XR, mueve la cámara con teclado y mouse y usa un rayo XRI que sale del centro de la
  pantalla, así los mismos botones y objetos agarrables de VR funcionan con el mouse.
- **Versión web**: plantilla `Assets/WebGLTemplates/SentirNacer`, puente con la página
  (`WebBridge.cs` + `Assets/Plugins/WebGL/ObstetriciaWebBridge.jslib`) y el menú
  **Tools > Obstetricia > Construir versión Web (WebGL)**.
- **Sala virtual en escritorio**: el menú se despliega con la tecla **E** y se pliega al elegir una opción
  (en VR queda como siempre, a un costado).
- **Tutorial** con textos de teclado y mouse cuando no hay visor.
- **Narración por voz apagada por defecto** (se activa en Ajustes > Narración por voz).
- **Video 360 que no carga**: se vuelve al entorno en vez de dejar la esfera negra.

## 1. Requisitos

| Qué | Para qué |
|---|---|
| **Unity 6000.3.27f1** con **Android Build Support** | Build de las Quest (lo que ya usas) |
| Módulo **Web Build Support** (en Unity Hub: Installs > ⚙ > Add modules) | Solo si vas a generar la versión web |
| **Git LFS** | Modelos, texturas, audio e imágenes |
| **Node.js 18+** | Solo para probar la versión web en el navegador |

## 2. Bajar la rama

En la carpeta de tu proyecto (`Obstetricia-VR`):

```powershell
git fetch origin
git switch modo-escritorio-web
git lfs pull
```

Abre el proyecto con Unity Hub (6000.3.27f1) y la escena `Assets/Scenes/SampleScene.unity`.
No hay que agregar nada a la escena: el modo escritorio se crea solo al dar Play.

## 3. Probar en las Quest (lo más importante)

Haz tu build para Android como siempre y revisa:

- [ ] Tutorial con manos y con controles.
- [ ] Mesa UCEN, programa Fisio-balón (pelvis, mecanismos, evaluación) y programa Legal.
- [ ] Sala virtual: entrar, moverse por los círculos del piso, menú a un costado, pelvis, guatero, salir.
- [ ] La voz viene apagada; al activarla en Ajustes debe narrar, y al apagarla debe callarse de inmediato.
- [ ] Recentrar (botón Meta / menú) sigue funcionando.

> **Play en el Editor con Quest Link:** si al dar Play aparece el modo escritorio (mouse y teclado), revisa que
> **Tools > Obstetricia > Modo escritorio en el Editor** esté **desmarcado** y que Link esté conectado antes de
> dar Play. En el APK de la Quest el modo escritorio nunca se activa.

## 4. Probar el modo escritorio en el Editor

Sin visor conectado, solo da **Play**: se activa solo. Con visor conectado puedes forzarlo con
**Tools > Obstetricia > Modo escritorio en el Editor**.

| Tecla | Acción |
|---|---|
| Mouse | Mirar (haz clic en la escena para tomar el cursor; **Esc** lo suelta) |
| W A S D / flechas | Moverse (**Shift** para correr) |
| Clic izquierdo | Presionar botones · mantener para tomar objetos |
| Rueda del mouse | Acercar / alejar el objeto tomado |
| **Tab** (mantener) + mouse | Rotar el objeto tomado |
| **H** | Devolver el objeto a su lugar original |
| **E** | Sala virtual: abrir / cerrar su menú |
| I J K L | Girar la pelvis |
| **R** (mantener) | Recentrar la mesa |
| **C** | Mostrar / ocultar los controles |

## 5. Generar la versión web (WebGL)

La build se guarda en una carpeta **`web/public/unity` al lado de tu proyecto** (un nivel arriba de
`Obstetricia-VR`), que es donde la busca la página web:

```
Escritorio/
├── Obstetricia-VR/        ← este proyecto de Unity
└── web/
    └── public/
        └── unity/         ← aquí queda la versión web
```

1. Instala el módulo **Web Build Support** (ver Requisitos).
2. En Unity: **Tools > Obstetricia > Construir versión Web (WebGL)**. La primera vez tarda bastante (10-15 min).
   El menú deja solo la plantilla, la compresión y los demás ajustes de WebGL.
3. Pruébala sin la página, con un servidor local (Unity WebGL no funciona abriendo el archivo con doble clic):

   ```powershell
   npx --yes serve ..\web\public\unity
   ```

   Abre la dirección que muestra (por ejemplo `http://localhost:3000`) y agrega parámetros si quieres:

   | URL | Abre |
   |---|---|
   | `/index.html` | La mesa UCEN |
   | `/index.html?modulo=fisio` | Programa Fisio-balón directo |
   | `/index.html?modulo=legal` | Programa Legal directo |
   | `/index.html?modulo=sala` | Sala virtual directo |
   | `&alumno=Nombre` | Deja el nombre del estudiante ya escrito |

### ⚠ Antes de hacer commit después de una build web

Al compilar para WebGL, Unity cambia solo algunos ajustes del proyecto, y uno de ellos **rompe la build de
las Quest**: borra de `ProjectSettings/ProjectSettings.asset` dos líneas de `preloadedAssets` (los ajustes de
XR). Antes de hacer commit revisa:

```powershell
git diff ProjectSettings/ Assets/Settings/
```

Si ves que se borran líneas bajo `preloadedAssets:` o cambios en `UnityConnectSettings.asset`,
`PC_RPAsset.asset`, `Mobile_RPAsset.asset` o `UniversalRenderPipelineGlobalSettings.asset` que tú no hiciste,
descártalos:

```powershell
git checkout -- ProjectSettings/ProjectSettings.asset ProjectSettings/UnityConnectSettings.asset Assets/Settings/
```

## 6. Conectar con la página Sentir Nacer

La página (React + Vite + Firebase) muestra la versión web dentro de cada curso con realidad inmersiva:
**Curso > Realidad inmersiva > «Sin visor (modo escritorio)»**. Unity le avisa a la página cuando está lista y
le envía los resultados de las evaluaciones, que se guardan en Firestore (colección `immersiveResults`).

> **Pendiente:** la versión de la página que trae la sección «Realidad inmersiva» todavía no está subida a
> GitHub. Cuando Andrés la suba, los pasos son:

1. Clona el repositorio de la página en la carpeta **`web`**, al lado de `Obstetricia-VR` (ver el esquema del paso 5).
2. Copia `web/.env.example` como `web/.env.local` y completa los datos de Firebase (pídeselos a Andrés; no se
   suben a GitHub).
3. Genera la versión web de Unity (paso 5): queda sola en `web/public/unity`.
4. Levanta la página:

   ```powershell
   cd ..\web
   npm install
   npm run dev
   ```

5. Abre `http://localhost:5173`, inicia sesión y entra a un curso con realidad inmersiva.

## 7. Problemas comunes

| Problema | Solución |
|---|---|
| En el Editor aparece el modo escritorio estando con Link | Desmarca **Tools > Obstetricia > Modo escritorio en el Editor** y conecta Link antes de dar Play |
| La build de la Quest dejó de funcionar después de una build web | Revisa el aviso del paso 5 (líneas `preloadedAssets` borradas) |
| El navegador no deja tomar el cursor | Algunos navegadores integrados lo bloquean; en Chrome o Edge funciona. Sin bloqueo igual se mira moviendo el mouse |
| Las secciones con video 360 no muestran nada | Los videos (`Assets/StreamingAssets/Videos360/*.mp4`) no están en el repo. Súbelos con Git LFS. Para la web conviene H.264 de 4K o menos |
| Piso o paredes en magenta en la web | El material `Assets/Resources/ObstetriciaDesktop/SalaEscritorio.mat` debe existir (viene en la rama) |

## 8. Cuando todo funcione

Junta la rama con `main` desde GitHub con un pull request:
<https://github.com/lNubet/Obstetricia-VR/pull/new/modo-escritorio-web>

Más detalle técnico del modo escritorio en el `README.md` (sección «Modo escritorio»).
