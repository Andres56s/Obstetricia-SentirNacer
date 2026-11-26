# Cómo dejar el proyecto Sentir Nacer funcionando en otro computador

Guía para armar **el mismo espacio de trabajo** en otro PC o notebook (Windows). Sirve para hacerlo a mano o
para pasársela a Claude Code: al final hay un [texto listo para pegarle](#texto-para-claude-code).

## Cómo está organizado

El proyecto son **dos repositorios** que se clonan dentro de una misma carpeta:

```
Obstetricia-SentirNacer/
├── web/    ← github.com/Andres56s/Proyecto_obste   rama realidad-inmersiva   (página: React + Vite + Firebase)
└── unity/  ← github.com/lNubet/Obstetricia-VR      rama modo-escritorio-web  (experiencia VR + modo escritorio)
```

- Los nombres de las carpetas importan: Unity guarda la versión web en `../web/public/unity` (al lado de `unity/`).
- Cada carpeta es su propio repositorio: los cambios de la página se suben desde `web/` y los de Unity desde `unity/`.
- **No se trabaja en `main`** de ninguno de los dos: todo va en las ramas indicadas hasta probarlo en las Quest.
  El repo de Unity es del compañero (lNubet); su `main` lo junta él con un pull request.

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

## 2. Clonar los dos repositorios

En PowerShell, desde donde quieras dejar el proyecto (por ejemplo el Escritorio):

```powershell
git lfs install
mkdir Obstetricia-SentirNacer
cd Obstetricia-SentirNacer
git clone -b realidad-inmersiva https://github.com/Andres56s/Proyecto_obste.git web
git clone -b modo-escritorio-web https://github.com/lNubet/Obstetricia-VR.git unity
```

Para **subir** cambios, la cuenta de GitHub debe tener permiso en cada repo (Andres56s ya es colaborador del de
Unity). La primera vez que hagas `git push`, Git abre una ventana para iniciar sesión en GitHub.

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

Antes de empezar, trae lo último de los dos repos:

```powershell
git -C web pull
git -C unity pull
```

Para subir cambios, en la carpeta que corresponda (`web` o `unity`):

```powershell
git status
git add -A
git commit -m "qué cambió"
git push
```

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
- **Pendiente**:
  - Publicar las reglas de `web/firestore.rules` (colección `immersiveResults`) en Firebase: cambian la base de
    datos real, hacerlo solo cuando el equipo lo decida.
  - Que el compañero pruebe la rama `modo-escritorio-web` en las Quest y la junte con su `main`.
  - Los videos 360° (`unity/Assets/StreamingAssets/Videos360/*.mp4`) no están en el repo: los tiene el compañero.
  - Juntar `realidad-inmersiva` con `main` en el repo de la página cuando todo esté probado.

## Texto para Claude Code

Si usas Claude Code en el otro computador, abre una sesión en la carpeta donde quieras el proyecto y pégale esto:

```text
Quiero dejar funcionando en este computador el proyecto Sentir Nacer (página web + experiencia Unity VR).
Lee primero la guía completa:
https://github.com/Andres56s/Proyecto_obste/blob/realidad-inmersiva/CONFIGURAR_OTRO_PC.md
y síguela paso a paso: revisa qué programas ya están instalados e instala los que falten (pídeme permiso
antes de instalar y avísame cuando tenga que aceptar algo yo, como la licencia de Android en Unity Hub),
crea la carpeta Obstetricia-SentirNacer con los dos repos clonados en sus ramas (web y unity), y dime
cuándo copiar mi archivo .env.local. Después levanta la página, genera la versión web de Unity y
pruébala en el navegador. No subas nada a GitHub ni hagas cambios en main sin preguntarme.
Lee también la sección "Estado del proyecto" de la guía: es el contexto de lo que ya está hecho.
```
