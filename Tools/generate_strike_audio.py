"""Original offline noise/resonator synthesis; Python standard library only.

48 kHz mono PCM masters are also the Unity assets. Seeds make regeneration exact.
No downloaded samples, no runtime synthesis, no runtime dependencies.
"""
from pathlib import Path
import math
import random
import struct
import wave

RATE = 48000
ROOT = Path(__file__).resolve().parents[1] / "Assets/Game/Audio/Gameplay"


def lowpass(signal, frequencies):
    output = []
    value = 0.0
    for sample, frequency in zip(signal, frequencies):
        coefficient = 1 - math.exp(-2 * math.pi * frequency / RATE)
        value += coefficient * (sample - value)
        output.append(value)
    return output


def noise(count, seed):
    generator = random.Random(seed)
    return [generator.gauss(0, 0.3) for _ in range(count)]


def resonator(signal, frequency, decay):
    radius = math.exp(-1 / (RATE * decay))
    coefficient = 2 * radius * math.cos(2 * math.pi * frequency / RATE)
    previous = 0.0
    older = 0.0
    output = []
    for sample in signal:
        value = sample + coefficient * previous - radius * radius * older
        older = previous
        previous = value
        output.append(value)
    peak = max(abs(value) for value in output)
    return [value / peak for value in output]


def write(name, signal, peak):
    maximum = max(abs(value) for value in signal)
    # Short terminal taper prevents a discontinuity, no reverb tail.
    for index in range(len(signal)):
        fade = min(1, index / 48, (len(signal) - 1 - index) / 240)
        signal[index] = signal[index] / maximum * peak * fade
    ROOT.mkdir(parents=True, exist_ok=True)
    with wave.open(str(ROOT / name), "wb") as target:
        target.setnchannels(1)
        target.setsampwidth(2)
        target.setframerate(RATE)
        target.writeframes(b"".join(struct.pack("<h", round(value * 32767)) for value in signal))
    rms = math.sqrt(sum(value * value for value in signal) / len(signal))
    print(f"{name}: {len(signal) / RATE:.3f}s, peak {20 * math.log10(peak):.1f} dBFS, RMS {20 * math.log10(rms):.1f} dBFS")


def main():
    count = round(RATE * .200)
    times = [index / RATE for index in range(count)]
    air = noise(count, 831)
    sweep = [900 + 4300 * math.exp(-((time - .045) / .032) ** 2) for time in times]
    bright = lowpass(air, sweep)
    body = lowpass(air, [430] * count)
    texture = lowpass(noise(count, 832), [1500] * count)
    whoosh = []
    for index, time in enumerate(times):
        envelope = (1 - math.exp(-time / .010)) * math.exp(-time / .036)
        pressure = math.exp(-((time - .035) / .016) ** 2)
        whoosh.append((bright[index] - body[index] + .22 * texture[index]) * (envelope + .26 * pressure))
    write("StrikeWhoosh.wav", whoosh, .63)

    count = round(RATE * .135)
    times = [index / RATE for index in range(count)]
    excitation = [sample * math.exp(-time / .004) for sample, time in zip(noise(count, 234), times)]
    # Noise-excited damped modes produce a compact fist/pad body, not a pure tone.
    modes = [resonator(excitation, frequency, decay) for frequency, decay in [(105, .019), (178, .013), (293, .008)]]
    skin = lowpass(noise(count, 235), [2300] * count)
    impact = []
    for index, time in enumerate(times):
        body = .65 * modes[0][index] + .26 * modes[1][index] + .12 * modes[2][index]
        impact.append(body + .8 * skin[index] * math.exp(-time / .009))
    write("StrikeImpact.wav", impact, .71)


if __name__ == "__main__":
    main()
