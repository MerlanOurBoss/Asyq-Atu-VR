"""Цветные варианты сақа из запечённой красной текстуры: краску перекрашиваем,
тени и стёртые до кости рёбра сохраняются.
Запуск: blender -b --factory-startup --python make_saqa_variants.py -- <Saqa_BaseColor.png> <папка>
"""
import os
import sys

import bpy
import numpy as np

src, out_dir = sys.argv[sys.argv.index("--") + 1:][:2]
img = bpy.data.images.load(src)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)
r, g, b = px[..., 0], px[..., 1], px[..., 2]

# Где краска: красный заметно сильнее зелёного и синего (стёртая кость сюда не попадает)
paint = np.clip((r - np.maximum(g, b) - 0.15) / 0.2, 0.0, 1.0)[..., None]
lum = (0.30 * r + 0.59 * g + 0.11 * b)[..., None]
mean_paint_lum = float((lum * paint).sum() / max(paint.sum(), 1.0))
shade = np.clip((lum / mean_paint_lum) ** 1.6, 0.4, 1.5)   # светотень и износ краски, чуть контрастнее

variants = {
    "Saqa_Blue": (0.20, 0.33, 0.72),     # көк сақа
    "Saqa_White": (0.88, 0.83, 0.70),    # ақ, некрашеный
    "Saqa_Quyma": (0.42, 0.40, 0.37),    # құйма: тёмный, «свинцовый»
}
for name, color in variants.items():
    target = np.clip(np.array(color, dtype=np.float32) * shade, 0.0, 1.0)
    rgb = px[..., :3] * (1 - paint) + target * paint
    outp = np.concatenate([rgb, np.ones((h, w, 1), dtype=np.float32)], axis=2)
    im = bpy.data.images.new(name, w, h, alpha=False)
    im.pixels.foreach_set(outp.ravel())
    im.filepath_raw = os.path.join(out_dir, name + ".png")
    im.file_format = 'PNG'
    im.save()
    print("saved", im.filepath_raw)
