"""Текстура доски из картинки с орнаментом (Art/board_frame_source.jpg).
В картинке посередине остались значки редактора и текст — центр восстанавливаем
по чистой полосе дерева слева, а левую половину обвязки отражаем на правую
(справа вверху лента смазана). Результат — 2048×1024, как доска 2.4 × 1.2 м.

Запуск в фоне: blender -b --factory-startup --python make_board_from_image.py -- <вход.jpg> <выход.png>
"""
import sys

import bpy
import numpy as np

src_path, out_path = sys.argv[sys.argv.index("--") + 1:][:2]
img = bpy.data.images.load(src_path)
w, h = img.size
px = np.array(img.pixels[:], dtype=np.float32).reshape(h, w, 4)[::-1].copy()   # строки сверху вниз
r, g, b = px[..., 0], px[..., 1], px[..., 2]
ornament = ((r > 0.40) & (g < 0.22) & (b < 0.22)) | ((r > 0.70) & (g > 0.50) & (b < 0.50))   # красное или золото

# Пропорция 2:1 — срезаем поровну сверху и снизу (там только дерево за рамкой)
crop = (h - w // 2) // 2
px, ornament = px[crop:crop + w // 2], ornament[crop:crop + w // 2]
h = px.shape[0]

# Центр рамки по горизонтали — по красной ленте в средней полосе
mid = ornament[h // 3: 2 * h // 3]
cols = np.where(mid.any(axis=0))[0]
center = (cols.min() + cols.max()) / 2.0

# Отражаем левую половину на правую вокруг центра рамки
xs = np.arange(w)
src = np.clip(np.round(2 * center - xs).astype(int), 0, w - 1)
right = xs > center
px[:, right] = px[:, src[right]]
ornament[:, right] = ornament[:, src[right]]

# Внутренний край ленты: последний пиксель орнамента от каждого края + запас
def inner_edge(profile, from_start):
    idx = np.where(profile)[0]
    return idx.max() if from_start else idx.min()

row_mid, col_q = h // 2, w // 4
# Ищем только у самого края: дальше в картинке кремовые буквы, похожие на золото
left = inner_edge(ornament[row_mid, : w // 6], True) + 14
top = inner_edge(ornament[: h // 3, col_q], True) + 14
bottom = 2 * h // 3 + inner_edge(ornament[2 * h // 3:, col_q], False) - 14
right_edge = int(round(2 * center)) - left

# Центр: доски горизонтальные, поэтому каждую строку продолжаем зеркальной плиткой
# из чистой полосы дерева у левого края панели (слева от бывшей надписи).
# Строки берём ниже самых глубоких кончиков волнистой ленты в этой полосе, иначе они размножатся.
strip = 300
band_cols = ornament[:, left:left + strip]
fill_top = max(np.where(c[: h // 3])[0].max() for c in band_cols.T if c[: h // 3].any()) + 6
fill_bottom = 2 * h // 3 + min(np.where(c[2 * h // 3:])[0].min() for c in band_cols.T if c[2 * h // 3:].any()) - 6
x0 = left
for x in range(left + strip, right_edge):
    k = (x - x0) % (2 * strip)
    sx = x0 + (k if k < strip else 2 * strip - 1 - k)
    px[fill_top:fill_bottom, x] = px[fill_top:fill_bottom, sx]

# Панель под текст плавно темнее к центру — кремовые буквы читаются лучше, а ступеньки нет
yy, xx = np.mgrid[0:h, 0:w]
inside = np.minimum.reduce([xx - left, right_edge - xx, yy - top, bottom - yy]).astype(np.float32)
dark = 1.0 - 0.14 * np.clip(inside / 80.0, 0.0, 1.0)
px[..., :3] *= dark[..., None]

out = bpy.data.images.new("Board", w, h, alpha=False)
out.pixels.foreach_set(px[::-1].ravel())
out.scale(2048, 1024)
out.filepath_raw = out_path
out.file_format = 'PNG'
out.save()
print(f"saved {out_path}: frame center x={center:.0f}, panel x {left}..{right_edge}, y {top}..{bottom} of {w}x{h}")
