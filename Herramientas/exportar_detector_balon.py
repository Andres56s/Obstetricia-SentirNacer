"""
Genera un detector especializado en balones kinésicos (fitball) para la app de Obstetricia.

Usa YOLO-World (detección por vocabulario abierto): se le indica qué buscar por nombre y se exporta
a ONNX para Unity Inference Engine. No necesita fotos ni entrenamiento.

Requisitos (una sola vez, ~2-3 GB por PyTorch):
    pip install ultralytics

Uso:
    python exportar_detector_balon.py

Resultado: "detector_balon.onnx" en esta carpeta. Cópialo a Assets/Obstetricia/Modelos/
(y borra o mueve yolo11n.onnx) y ejecuta Tools > MR Setup > 5 en Unity.
"""
from ultralytics import YOLOWorld

model = YOLOWorld("yolov8s-worldv2.pt")  # se descarga automáticamente (~25 MB)
model.set_classes(["exercise ball", "yoga ball", "gym ball", "fitness ball"])
path = model.export(format="onnx", opset=15, imgsz=640, simplify=True)
print("Modelo exportado en:", path)

import os, shutil
destino = os.path.join(os.path.dirname(os.path.abspath(__file__)), "detector_balon.onnx")
shutil.copy(path, destino)
print("Copiado a:", destino)
