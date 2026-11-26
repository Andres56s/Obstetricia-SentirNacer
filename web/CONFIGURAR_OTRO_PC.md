# Cómo dejar el proyecto Sentir Nacer funcionando en otro computador

Guía para armar **el mismo espacio de trabajo** en otro PC o notebook (Windows). Sirve para hacerlo a mano o
para pasársela a Claude Code: al final hay un [texto listo para pegarle](#texto-para-claude-code).

## Cómo está organizado

Todo el proyecto está en **un solo repositorio**: <https://github.com/Andres56s/Obstetricia-SentirNacer> (rama `main`).

```
Obstetricia-SentirNacer/
├── web/    página: React + Vite + Firebase
└── unity/  experiencia VR (Meta Quest) + modo escritorio (Unity WebGL)
```

- Los nombres de las carpetas importan: Unity guarda la versión web en `../web/public/unity` (al lado de `unity/`).
- Se trabaja y se sube todo desde la raíz del repo (`git push` sube web y unity juntos).
- **El compañero (lNubet)** tiene su propio repo de Unity: <https://github.com/lNubet/Obstetricia-VR>. Para pasarle
  los cambios de `unity/` hay que subirlos a su rama `modo-escritorio-web` (ver el paso 6); su `main` lo junta él.
  La página también existe en <https://github.com/Andres56s/Proyecto_obste> (rama `realidad-inmersiva`), que quedó
  como versión anterior.

## 1. Instalar programas

| Programa | Para qué | Cómo |
|---|---|---|
| **Git** y **Git LFS** | Bajar los repos (Unity guarda modelos, texturas y audio con LFS) | `winget install Git.Git GitHub.GitLFS` |
| **Node.js 20 o más** | La página web | `winget install OpenJS.NodeJS.LTS` |
| **Unity Hub** | Abrir Unity | `winget install Unity.UnityHub` |
| **Unity 6000.3.27f1** con **Android Build Support** y **Web Build Support** | Build de las Quest y versión web | En Unity Hub: Installs > Install Editor > Archive > 6000.3.27f1 (o el enlace `unityhub://6000.3.27f1/672d562a42a7`) |

> Usa exactamente **6000.3.27f1**: otra versión de Unity modifica el proyecto. El módulo Android pide aceptar la
> licencia del SDK de Android; hazlo tú en Unity Hub.

Abre una terminal nueva después de instalar (para que reconozca `git`, `node` y `npm`).

## 2. Clonar el repositorio

En PowerShell, desde donde quieras dejar el proyecto (por ejemplo el Escritorio):

```powershell
git lfs install
git clone https://github.com/Andres56s/Obstetricia-SentirNacer.git
cd Obstetricia-SentirNacer
```

Si el repo es privado, Git abre una ventana para iniciar sesión en GitHub (cuenta **Andres56s**). Pesa unos
220 MB (los modelos de Unity se bajan con Git LFS).

## 3. Claves de Firebase (`web/.env.local`)

La página necesita `web/.env.local` con las claves de Firebase. **Ese archivo no está en GitHub** (a propósito).
Cópialo desde el otro computador (pendrive, correo a ti mismo, gestor de contraseñas) a `web/.env.local`, o
créalo desde `web/.env.example` con los datos de la consola de Firebase
(Configuración del proyecto > General > Tus apps > Configuración del SDK).

**Nunca lo subas a GitHub.** Ya está en `.gitignore`.

## 4. Levantar la página

```powershell
cd web
npm install
npm run dev
```

Abre <http://localhost:5173>. La sección «Realidad inmersiva» de un curso avisa que falta la versión web de
Unity hasta que la generes (paso 5).

## 5. Abrir Unity y generar la versión web

1. En Unity Hub: **Add > Add project from disk** y elige la carpeta `unity/`. La primera vez tarda bastante
   (reconstruye `Library/`).
2. Abre la escena `Assets/Scenes/SampleScene.unity`.
3. Menú **Tools > Obstetricia > Construir versión Web (WebGL)**. Tarda 10 a 15 minutos la primera vez y deja
   la build en `web/public/unity/`.
4. Con la página corriendo, prueba la experiencia en <http://localhost:5173/unity/index.html>.
   Parámetros útiles: `?modulo=fisio`, `?modulo=legal`, `?modulo=sala`, `&alumno=Nombre`.

También se puede compilar sin abrir el editor (con Unity cerrado):

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.3.27f1\Editor\Unity.exe" -batchmode -quit -nographics `
  -projectPath "$PWD\unity" -buildTarget WebGL `
  -executeMethod ObstetriciaSetup.ObstetriciaWebBuild.BuildWebGL -logFile "$env:TEMP\build-web.log"
```

### ⚠ Después de cada build web

Unity cambia solo algunos ajustes al compilar para WebGL y **uno rompe la build de las Quest**: borra dos
líneas de `preloadedAssets` (XR) en `ProjectSettings/ProjectSettings.asset`. Antes de hacer commit en `unity/`:

```powershell
cd unity
git status
git checkout -- ProjectSettings/ProjectSettings.asset ProjectSettings/UnityConnectSettings.asset Assets/Settings/
```

(Ese último comando descarta **todos** los cambios en esos archivos: úsalo solo si tú no los modificaste a propósito.)

## 6. Trabajar día a día

Antes de empezar, trae lo último (desde la raíz `Obstetricia-SentirNacer`):

```powershell
git pull
```

Para subir cambios (web y unity juntos), desde la raíz:

```powershell
git status
git add -A
git commit -m "qué cambió"
git push
```

**Pasarle los cambios de Unity al compañero** (cuando se quiera que los pruebe en sus Quest): clonar
<https://github.com/lNubet/Obstetricia-VR> en una carpeta temporal, cambiarse a la rama `modo-escritorio-web`,
copiar encima el contenido de `unity/`, revisar el diff (sin ajustes de XR borrados, ver el aviso del paso 5),
hacer commit y push a esa rama. Claude Code puede hacerlo si se le pide. Nunca subir a su `main`.

Revisa con `git status` que no aparezcan `.env.local`, `node_modules/`, `Library/` ni `public/unity/` (la build):
todos están ignorados, pero conviene mirarlo.

## Estado del proyecto (contexto)

Para quien retome el trabajo (persona o Claude):

- **Modo escritorio** (sin visor, `unity/Assets/Scripts/Obstetricia/Desktop/`): se activa solo sin visor (WebGL,
  Editor sin Link o *Tools > Obstetricia > Modo escritorio en el Editor*). Controles: mouse para mirar (cursor
  bloqueado; Esc lo suelta), WASD + Shift, clic izquierdo para botones y mantener para tomar, rueda para
  acercar/alejar, Tab + mouse para rotar, H devuelve el objeto, E abre el menú de la sala virtual, I J K L giran
  la pelvis, R (mantener) recentra, C muestra/oculta la ayuda.
- **Maletín Sentir Nacer** (`unity/Assets/Scripts/Obstetricia/Modules/MaletinSala.cs`): columna aparte a la
  izquierda de la mesa UCEN, en VR y web. Al abrirlo aparece el acceso a la sala virtual (reemplaza al botón que
  había sobre las tarjetas). Su lugar se ajusta en `HubController.maletinLocal`.
- **Sala virtual** (del compañero, `unity/Assets/Scripts/Obstetricia/Sala/`): en escritorio su menú se pliega y se
  abre con E; en VR queda a un costado.
- **Narración por voz**: apagada por defecto (Ajustes > Narración por voz).
- **Página**: `/curso/:id/inmersivo` (`web/src/pages/ImmersivePage.jsx`, `web/src/components/ImmersivePlayer.jsx`);
  cada curso elige qué programa abre (`immersiveModule`: mesa, `fisio`, `legal` o `sala`).
- **Práctico = simulación con videos 360°** (solo en la página, **no** va en la experiencia Unity): el apartado
  Práctico de cada curso es una galería de videos 360° (`web/src/components/Practica360.jsx`). Al elegir un video:
  - **Ver video** lo abre en YouTube (el reproductor incrustado no dejaba mirar alrededor).
  - **Ver en VR** usa el visor propio `web/src/components/Video360Viewer.jsx` (three.js + WebXR): con las Quest
    conectadas al PC por **Quest Link** y la página abierta en **Chrome o Edge** (o desde el navegador de las
    Quest), «Entrar al visor VR» pone al usuario dentro del video y se mueve con la cabeza; el gatillo pausa.
    Admite guía por tiempos (`minuto:segundo | texto | pausa`). Necesita el video como `.mp4` (YouTube no sirve).
  - Videos incluidos en código: `web/src/data/videos360.js`. El de Fisio-balón (curso `1`) usa YouTube
    `BPlModwMZG4` para «Ver video» y el archivo `web/public/videos360/practica-fisiobalon.mp4` (30 MB) para «Ver en VR».
  - Los profesores pueden agregar más con «Agregar video 360°» (se guardan en `courses/{id}.practica`).
  - Ya no se muestra el «Test práctico» (las preguntas siguen en `practicaTest`, solo ocultas).
- **Preferencias del usuario (Andrés)**: habla español; se trabaja en un solo repo,
  `Andres56s/Obstetricia-SentirNacer` (rama `main`, carpeta `Obstetricia-SentirNacer`); hay que preguntarle antes
  de subir a GitHub, de tocar la base de datos real o de cambiar algo fuera de lo que pidió; los cambios grandes se
  prueban en el navegador antes de entregarlos.
- **Pendiente**:
  - Probar «Ver en VR» del Práctico con las Quest conectadas por Quest Link (no se ha probado con visor real).
  - Publicar las reglas de `web/firestore.rules` (colección `immersiveResults`) en Firebase: cambian la base de
    datos real, hacerlo solo cuando el equipo lo decida.
  - Que el compañero pruebe la rama `modo-escritorio-web` en las Quest (maletín, modo escritorio) y la junte con su `main`.
  - Videos 360° del programa Fisio en Unity (`unity/Assets/StreamingAssets/Videos360/*.mp4`): no están en el repo,
    los tiene el compañero.
  - Publicar la página cuando todo esté probado (WebXR necesita https para usar el visor desde una página publicada).

## Texto para Claude Code

Si usas Claude Code en el otro computador, abre una sesión en la carpeta donde quieras el proyecto (por ejemplo
el Escritorio) y pégale esto:

```text
Quiero dejar funcionando en este computador el proyecto Sentir Nacer (página web + experiencia Unity VR)
y seguir trabajando en él desde aquí. Está todo en el repo https://github.com/Andres56s/Obstetricia-SentirNacer
1. Si no están, instala Git y Git LFS (pídeme permiso antes de instalar), ejecuta "git lfs install" y clona
   ese repo aquí. Si GitHub pide iniciar sesión, avísame para hacerlo yo.
2. Lee completa la guía web/CONFIGURAR_OTRO_PC.md del repo y síguela paso a paso: revisa qué programas ya
   están instalados e instala los que falten (pídeme permiso antes y avísame cuando tenga que aceptar algo
   yo, como la licencia de Android en Unity Hub), y dime cuándo copiar mi archivo web/.env.local.
3. Levanta la página, genera la versión web de Unity y pruébala en el navegador (la mesa UCEN con el
   maletín, y el Práctico de Fisio-balón con sus videos 360°).
Lee también la sección "Estado del proyecto" de la guía: es el contexto de todo lo que ya está hecho,
lo que falta y cómo prefiero trabajar. Guárdalo en tu memoria para las próximas sesiones.
No subas nada a GitHub ni toques la base de datos real de Firebase sin preguntarme.
```
