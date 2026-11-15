"""Bake Results Foley from the project's hash-verified CC0 physical recordings.

No oscillators or generated tones: wood attacks, bamboo air, wood/paper-like
friction and pitched, filtered contact bodies share the existing dojo palette.
Run from the repository root with python Tools/generate_results_audio.py.
"""
import json
from prepare_recorded_audio import ROOT, RATE, read, trim, level, mix, write


def envelope(signal, duration, attack=.003, release=.035):
    signal = signal[:round(duration * RATE)]
    return [sample * min(1, i / (attack * RATE),
                         (len(signal) - 1 - i) / (release * RATE))
            for i, sample in enumerate(signal)]


def room(signal):
    # A very small wooden room; no long reverb competing with the music.
    return mix((signal, 1, 0), (signal, .12, .027), (signal, .055, .049))


def bake():
    manifest = []

    def emit(name, signal, sources, peak=.42):
        info = write("Assets/Game/Audio/Foley/Results" + name + ".wav",
                     room(signal), peak, .025)
        info["sources"] = sources
        manifest.append(info)

    wood = level(trim(read("wood", 0, .324, "highpass=f=150,lowpass=f=5400")))
    body = level(trim(read("wood", 0, .324,
                           "highpass=f=65,lowpass=f=1100", .48)))
    air = level(trim(read("bamboo", 0, .412,
                          "highpass=f=340,lowpass=f=5800", 1.35)))
    # Actual wood friction, shaped as two soft bristle contacts, not a claim
    # that the source is a recording of a brush or traditional instrument.
    friction = level(read("creak", .17, .44,
                          "highpass=f=1100,lowpass=f=6200", 1.25))
    paper = envelope(friction, .20, .018, .065)
    brush = mix((paper, .85, 0), (envelope(friction, .12, .025, .07), .24, .10))
    emit("Arrival", mix((envelope(air, .29, .02, .10), .28, 0),
                         (envelope(body, .38, .004, .14), .65, .045),
                         (paper, .16, .035)), ["bamboo", "wood", "creak"], .46)
    emit("Brush", brush, ["creak"], .24)

    for index, speed in enumerate((1.10, 1.26, 1.42)):
        tap = level(trim(read("wood", 0, .20,
                              "highpass=f=600,lowpass=f=4200", speed)))
        emit("Count" + str(index + 1), envelope(tap, .085, .001, .04), ["wood"], .30)

    emit("Statistic", envelope(wood, .115, .001, .05), ["wood"], .31)
    emit("Clear", mix((envelope(wood, .18, .001, .07), .85, 0),
                       (envelope(body, .37, .002, .16), .52, .003),
                       (envelope(air, .16, .015, .08), .06, .015)), ["wood", "bamboo"], .58)
    dull = level(trim(read("wood", 0, .324, "highpass=f=65,lowpass=f=1800", .72)))
    emit("Failed", mix((envelope(dull, .32, .001, .15), .8, 0),
                        (envelope(body, .38, .003, .19), .45, .004),
                        (paper, .08, .06)), ["wood", "creak"], .49)
    (ROOT / "Tools/AudioSources/results-outputs.json").write_text(
        json.dumps(manifest, indent=2), encoding="utf-8")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    bake()
