"""Deterministic original wood effects; no sampled or third-party audio."""
import math
import random
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets/Game/Audio"
RATE = 44100


def write(name, duration, kind):
    randomizer = random.Random(7621 + kind)
    values = []
    filtered = 0.0
    for index in range(int(duration * RATE)):
        time = index / RATE
        noise = randomizer.uniform(-1.0, 1.0)
        filtered = filtered * 0.78 + noise * 0.22
        if kind == 0:
            value = math.sin(2 * math.pi * 460 * time) * math.exp(-time * 34)
            value += 0.45 * math.sin(2 * math.pi * 1130 * time) * math.exp(-time * 60)
            value += 0.3 * noise * math.exp(-time * 170)
        elif kind == 1:
            value = 0.45 * math.sin(2 * math.pi * 240 * time) * math.exp(-time * 32)
            for onset in (0.0, 0.018, 0.041, 0.073):
                if time >= onset:
                    age = time - onset
                    value += 0.5 * noise * math.exp(-age * 125)
                    value += 0.2 * math.sin(2 * math.pi * 820 * age) * math.exp(-age * 70)
        else:
            envelope = math.sin(math.pi * time / duration) ** 0.7
            value = filtered * envelope * (0.65 + 0.25 * math.sin(time * 107))
            value += 0.07 * math.sin(2 * math.pi * (160 * time + 24 * time * time)) * envelope
            if time > duration - 0.07:
                age = time - (duration - 0.07)
                value += 0.22 * math.sin(2 * math.pi * 320 * age) * math.exp(-age * 65)

        fade = min(1.0, time / 0.001, (duration - time) / 0.012)
        values.append(value * fade)

    peak = max(abs(value) for value in values)
    frames = b"".join(struct.pack("<h", round(value / peak * 23000)) for value in values)
    with wave.open(str(ROOT / name), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(frames)


if __name__ == "__main__":
    ROOT.mkdir(parents=True, exist_ok=True)
    write("DojoKnock.wav", 0.16, 0)
    write("DojoBoardCrack.wav", 0.25, 1)
    write("DojoDoorSlide.wav", 0.55, 2)
