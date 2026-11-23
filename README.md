# Obstetricia-VR

Trabajo universitario para la carrera de Obstetricia en Realidad Virtual (UCEN).

Aplicación de realidad mixta para Meta Quest 3 / 3S para estudiantes de Obstetricia:

- **Mesa UCEN** con los dos programas.
- **Programa Fisio-balón**: anatomía de la pelvis (se activa al detectar el balón kinésico), diámetros pélvicos, mecanismos del parto en 3D, videos 360° y evaluación.
- **Programa Legal**: violencia obstétrica / Ley Integral de la Mujer (quiz de situaciones).
- Tutorial inicial, ajustes de comodidad, uso con manos o controles y registro de resultados.

## Requisitos

- **Unity 6000.3.27f1** con módulo **Android Build Support**.
- **Git LFS** (los modelos, texturas e imágenes se guardan con LFS).

## Cómo abrirlo

```bash
git lfs install
git clone <url-del-repositorio>
```

1. Abre la carpeta clonada con Unity Hub (Unity 6000.3.27f1). La primera vez tarda porque reconstruye `Library/`.
2. Abre la escena `Assets/Scenes/SampleScene.unity`.
3. Si algo falta en la escena, usa el menú **Tools > MR Setup > 5 - Construir experiencia Obstetricia**.

## Probar

- **En el Editor (Play):** con Quest Link activo como runtime OpenXR. Tecla **B**: simula la pelota.
- **En las Quest:** File > Build Profiles > Android > Build, e instala el APK.

## Estructura

| Carpeta | Contenido |
|---|---|
| `Assets/Scripts/Obstetricia` | Código de la experiencia (UI holográfica, módulos, detección, etc.) |
| `Assets/Editor/Obstetricia` | Setup de la escena y contenido inicial |
| `Assets/Obstetricia/Data` | Contenido editable (quizzes, secciones, tema) |
| `Assets/Obstetricia/Modelos` | Pelvis 3D y modelo de detección YOLO |
| `Assets/Obstetricia/Imagenes` | Logo y fotos de las tarjetas |
| `Herramientas` | Script para generar un detector especializado del balón |

> El contenido legal y clínico es un borrador que debe validar el equipo docente.
