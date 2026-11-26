"""Лицевая сторона доски табло: тёмный орех из горизонтальных планок,
красная лента с золотым орнаментом «қошқар мүйіз» по краю и розетки в углах.
Запуск: python make_board_texture.py <путь к png>"""
import math
import random
import struct
import sys
import zlib

W, H = 1024, 512                      # доска 2.4 × 1.2 м
BOARD_W, BOARD_H = 2.4, 1.2
BAND_IN, BAND_OUT = 0.035, 0.125      # лента орнамента: от края, м
rng = random.Random(5)


def grid(n):
    return [[rng.random() for _ in range(n + 1)] for _ in range(n + 1)]


NOISE = [(grid(8), 8, 0.5), (grid(32), 32, 0.3), (grid(128), 128, 0.2)]


def value_noise(x, y, g, n):
    x *= n; y *= n
    xi, yi = min(int(x), n - 1), min(int(y), n - 1)
    tx, ty = x - xi, y - yi
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    a, b = g[yi][xi], g[yi][xi + 1]
    c, d = g[yi + 1][xi], g[yi + 1][xi + 1]
    return (a + (b - a) * tx) * (1 - ty) + (c + (d - c) * tx) * ty


def fbm(x, y):
    return sum(value_noise(x, y, g, n) * w for g, n, w in NOISE)


def mix(a, b, t):
    return tuple(a[i] + (b[i] - a[i]) * t for i in range(3))


WOOD_LIGHT, WOOD_DARK = (0.36, 0.22, 0.12), (0.24, 0.14, 0.08)
PANEL = (0.15, 0.09, 0.055)
BAND = (0.50, 0.09, 0.07)
GOLD = (0.93, 0.74, 0.36)


def horn_motif(s, t):
    """Қошқар мүйіз в плитке: s — вдоль ленты (−1..1), t — поперёк (−1 снаружи, +1 к центру).
    Стебель и два рога, загнутых наружу спиралью. True — золото."""
    th = 0.11
    if abs(s) < th and -0.75 < t < 0.2:
        return True
    for side in (-1, 1):
        cx, cy = side * 0.42, 0.2
        dx, dy = s - cx, t - cy
        r = math.hypot(dx, dy)
        ang = math.degrees(math.atan2(dy, dx * side)) % 360     # для правого рога — зеркально
        # внешняя дуга идёт от стебля вверх, через верх и закручивается вниз
        if abs(r - 0.42) < th and (ang <= 200 or ang >= 330):
            return True
        if abs(r - 0.2) < th * 0.9 and 30 <= ang <= 250:          # внутренний виток
            return True
    return False


def rosette(px, py):
    r = math.hypot(px, py)
    a = math.atan2(py, px)
    if abs(r - 0.8) < 0.09 or r < 0.18:
        return True
    petal = abs(math.cos(4 * a))                                 # восемь лепестков
    return 0.3 < r < 0.62 * petal + 0.12 and petal > 0.35


pixels = []
for j in range(H):
    v = (j + 0.5) / H
    row = []
    for i in range(W):
        u = (i + 0.5) / W
        x, y = u * BOARD_W, v * BOARD_H
        # дерево: планки по 0.2 м, волокно вытянуто вдоль доски
        plank = int(y / 0.2)
        grain = value_noise(u * 0.25 + plank * 0.37 % 1 * 0.05, (v * 6 % 1), NOISE[1][0], 32)
        n = fbm(u, v)
        col = mix(WOOD_DARK, WOOD_LIGHT, 0.35 + 0.4 * n + 0.25 * grain)
        if (y % 0.2) < 0.006:
            col = mix(col, (0.08, 0.05, 0.03), 0.8)
        edge = min(x, y, BOARD_W - x, BOARD_H - y)
        if edge > BAND_OUT + 0.012:
            # внутренняя панель под текст: темнее, с мягкой виньеткой
            inner = min(1.0, (edge - BAND_OUT) / 0.25)
            col = mix(col, PANEL, 0.55 + 0.3 * inner)
        elif BAND_IN <= edge <= BAND_OUT:
            col = mix(BAND, (0.30, 0.05, 0.04), 0.35 * n)
            t = (edge - BAND_IN) / (BAND_OUT - BAND_IN) * 2 - 1    # −1 снаружи, +1 к центру
            corner = (x < BAND_OUT or x > BOARD_W - BAND_OUT) and (y < BAND_OUT or y > BOARD_H - BAND_OUT)
            if corner:
                cx = BAND_OUT / 2 + BAND_IN / 2 if x < BOARD_W / 2 else BOARD_W - (BAND_OUT + BAND_IN) / 2
                cy = BAND_OUT / 2 + BAND_IN / 2 if y < BOARD_H / 2 else BOARD_H - (BAND_OUT + BAND_IN) / 2
                half = (BAND_OUT - BAND_IN) / 2
                if rosette((x - cx) / half, (y - cy) / half):
                    col = GOLD
            else:
                along = x if (y < BAND_OUT or y > BOARD_H - BAND_OUT) else y
                tile = 0.11
                s = ((along % tile) / tile) * 2 - 1
                if horn_motif(s, t):
                    col = mix(GOLD, (0.75, 0.55, 0.25), 0.4 * n)
            if abs(t) > 0.86:
                col = GOLD                                           # тонкие золотые кромки ленты
        elif edge < BAND_IN:
            col = mix(col, (0.12, 0.07, 0.04), 0.4)                  # наружный бортик темнее
        row.append(col)
    pixels.append(row)


def write_png(path, rows):
    raw = b"".join(b"\x00" + bytes(int(max(0, min(1, c)) ** (1 / 1.0) * 255) for px in r for c in px) for r in reversed(rows))
    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)
    with open(path, "wb") as f:
        f.write(b"\x89PNG\r\n\x1a\n")
        f.write(chunk(b"IHDR", struct.pack(">IIBBBBB", W, H, 8, 2, 0, 0, 0)))
        f.write(chunk(b"IDAT", zlib.compress(raw, 9)))
        f.write(chunk(b"IEND", b""))


write_png(sys.argv[1], pixels)
print("saved", sys.argv[1])
