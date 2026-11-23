"""Синтез звуков костей для Асық ату: щелчок кость о кость и глухой стук о землю."""
import math
import os
import random
import sys
import wave
import struct

SR = 44100
out = sys.argv[1]
os.makedirs(out, exist_ok=True)
rng = random.Random(7)


def lowpass(x, cutoff):
    a = math.exp(-2 * math.pi * cutoff / SR)
    y, s = [], 0.0
    for v in x:
        s = (1 - a) * v + a * s
        y.append(s)
    return y


def highpass(x, cutoff):
    return [v - l for v, l in zip(x, lowpass(x, cutoff))]


def noise(n):
    return [rng.gauss(0, 1) for _ in range(n)]


def save(name, x):
    peak = max(abs(v) for v in x) or 1.0
    x = [v / peak * 0.7 for v in x]  # около −3 дБ
    fade = int(0.004 * SR)
    for i in range(fade):
        x[-fade + i] *= 1 - i / fade
    with wave.open(os.path.join(out, name + ".wav"), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(b"".join(struct.pack("<h", int(v * 32767)) for v in x))
    print(name)


# Кость о кость: звонкий «цок» — затухающие моды 2–7 кГц и короткий щелчок шума.
for i in range(6):
    n = int(0.12 * SR)
    base = rng.uniform(0.9, 1.12)
    x = [0.0] * n
    for f, a, d in [(2150, 1.0, 0.020), (3450, 0.65, 0.013), (5250, 0.40, 0.008), (7200, 0.22, 0.005)]:
        f *= base * rng.uniform(0.96, 1.04)
        a *= rng.uniform(0.8, 1.2)
        d *= rng.uniform(0.85, 1.2)
        ph = rng.uniform(0, 2 * math.pi)
        for k in range(n):
            t = k / SR
            x[k] += a * math.sin(2 * math.pi * f * t + ph) * math.exp(-t / d)
    click = highpass(noise(n), 2500)
    x = [v + c * 1.2 * math.exp(-(k / SR) / 0.0015) for k, (v, c) in enumerate(zip(x, click))]
    save(f"BoneClick_{i + 1:02d}", x)

# Кость о землю: глухой стук — низкий тон 150–260 Гц, мягкий удар шума и немного «песка».
for i in range(5):
    n = int(0.16 * SR)
    f0 = rng.uniform(150, 260)
    body_decay = rng.uniform(0.018, 0.03)
    thump = lowpass(noise(n), rng.uniform(500, 900))
    grit = lowpass(highpass(noise(n), 1500), 4000)
    x = []
    for k in range(n):
        t = k / SR
        x.append(math.sin(2 * math.pi * f0 * t) * math.exp(-t / body_decay)
                 + thump[k] * 3.0 * math.exp(-t / 0.012)
                 + grit[k] * 0.35 * math.exp(-t / 0.006))
    save(f"BoneGround_{i + 1:02d}", x)
