"""Bake licensed recordings, never synthesize foley. Requires Python and ffmpeg on PATH.

Sources stay outside Assets so originals are not shipped in the player. Existing
downloads are hash checked; --fetch obtains missing sources after checking CC0 on
each individual Freesound page. Recipes and the output manifest are reviewable.
"""
import argparse
import array
import hashlib
import html
import json
import math
from datetime import date
from pathlib import Path
import re
import subprocess
import urllib.request
import wave

ROOT = Path(__file__).resolve().parents[1]
SOURCES = ROOT / "Tools/AudioSources"
RATE = 32000
RECORDINGS = {
    "wood": ("alegemaate", 364691),
    "punch1": ("Joao_Janz", 483991),
    "punch2": ("Joao_Janz", 483990),
    "punch3": ("Joao_Janz", 483996),
    "swings": ("CapsLok", 181906),
    "bamboo": ("Evan_McLoud", 850166),
    "crack": ("SoundCollectah", 109359),
    "creak": ("Rudmer_Rotteveel", 502505),
    "steps": ("florianreichelt", 459970),
    "wind": ("florianreichelt", 459977),
    "voice": ("Nox_Sound", 474651),
}


def fetch():
    SOURCES.mkdir(parents=True, exist_ok=True)
    for key, (author, ident) in RECORDINGS.items():
        if (SOURCES / (key + ".mp3")).exists():
            continue
        url = f"https://freesound.org/people/{author}/sounds/{ident}/"
        page = urllib.request.urlopen(url, timeout=40).read().decode()
        licenses = re.findall(r'https?://creativecommons.org/[^"<> ]+', page)
        if not any("publicdomain/zero/1.0/" in value for value in licenses):
            raise ValueError(f"Individual CC0 license not found: {url}")
        download = re.search(r'https://cdn.freesound.org/previews/[^"<> ]+-hq.mp3', page).group(0)
        data = urllib.request.urlopen(download, timeout=40).read()
        title = html.unescape(re.search(r"<title>(.*?)</title>", page, re.S).group(1).strip())
        info = dict(key=key, author=author, id=ident, title=title, url=url,
                    download=download, license="CC0-1.0",
                    license_url="https://creativecommons.org/publicdomain/zero/1.0/",
                    retrieved=date.today().isoformat(), sha256=hashlib.sha256(data).hexdigest(), bytes=len(data))
        (SOURCES / (key + ".mp3")).write_bytes(data)
        (SOURCES / (key + ".json")).write_text(json.dumps(info, indent=2), encoding="utf-8")


def read(key, start, end, filters="highpass=f=65,lowpass=f=11000", speed=1):
    path = SOURCES / (key + ".mp3")
    info = json.loads(path.with_suffix(".json").read_text(encoding="utf-8"))
    if info["license"] != "CC0-1.0" or hashlib.sha256(path.read_bytes()).hexdigest() != info["sha256"]:
        raise ValueError(f"Source license/hash mismatch: {path}")
    processing = f"atrim=start={start}:end={end},asetpts=PTS-STARTPTS,{filters}"
    if speed != 1:
        processing += f",asetrate={RATE * speed},aresample={RATE}"
    raw = subprocess.check_output([
        "ffmpeg", "-v", "error", "-i", str(path), "-ac", "1", "-ar", str(RATE),
        "-af", "aresample=" + str(RATE) + "," + processing, "-f", "f32le", "-",
    ])
    return list(array.array("f", raw))


def trim(signal, threshold=.008, tail=.035):
    # Remove recording pre-roll, not the attack. Individual crops exclude adjacent takes.
    peak = max(abs(value) for value in signal)
    active = [i for i, value in enumerate(signal) if abs(value) > peak * threshold]
    first = max(0, active[0] - int(RATE * .001))
    last = min(len(signal), active[-1] + int(RATE * tail))
    return signal[first:last]


def level(signal, peak=1):
    gain = peak / max(abs(value) for value in signal)
    return [value * gain for value in signal]


def mix(*layers):
    output = [0.0] * max(len(signal) for signal, gain, delay in layers)
    for signal, gain, delay in layers:
        offset = round(delay * RATE)
        if len(signal) + offset > len(output):
            output.extend([0.0] * (len(signal) + offset - len(output)))
        for i, value in enumerate(signal):
            output[i + offset] += value * gain
    return output


def write(path, signal, peak, fade=.025):
    signal = level(signal, peak)
    for i in range(len(signal)):
        signal[i] *= min(1, i / (RATE * .0005), (len(signal) - 1 - i) / (RATE * fade))
    path = ROOT / path
    path.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(array.array("h", [round(value * 32767) for value in signal]).tobytes())
    rms = math.sqrt(sum(value * value for value in signal) / len(signal))
    return dict(path=path.relative_to(ROOT).as_posix(), seconds=round(len(signal) / RATE, 4),
                peak_db=round(20 * math.log10(max(abs(v) for v in signal)), 2),
                rms_db=round(20 * math.log10(rms), 2), pcm_bytes=len(signal) * 2,
                sha256=hashlib.sha256(path.read_bytes()).hexdigest())


def bake():
    manifest = []
    base = "Assets/Game/Audio/"
    foley = base + "Foley/"

    def emit(path, signal, sources, peak=.63, fade=.025):
        info = write(path, signal, peak, fade)
        info["sources"] = sources
        manifest.append(info)

    wood = level(trim(read("wood", 0, .324)))
    punches = [level(trim(read(key, 0, .6, "highpass=f=65,lowpass=f=6500")))
               for key in ("punch1", "punch2", "punch3")]
    for i, punch in enumerate(punches):
        suffix = str(i + 1)
        heavy = mix((punch, .75, 0), (wood, .38, .001))
        soft = mix((punch, .8, 0), (wood, .12, .002))
        emit(foley + "HitPerfect" + suffix + ".wav", heavy, ["punch" + suffix, "wood"])
        emit(foley + "HitGood" + suffix + ".wav", soft, ["punch" + suffix, "wood"], .50)
        emit(foley + "DummyHit" + suffix + ".wav",
             mix((wood, .8, 0), (punch, .32, .002)), ["wood", "punch" + suffix], .63)
    emit(base + "Gameplay/StrikeImpact.wav", mix((punches[0], .75, 0), (wood, .38, .001)), ["punch1", "wood"])
    emit(base + "DojoKnock.wav", wood, ["wood"], .55)

    for i, (start, end) in enumerate(((.12, .58), (1.82, 2.25), (3.51, 3.96))):
        swing = trim(read("swings", start, end, "highpass=f=180,lowpass=f=10000", 1.4), .025, .018)
        emit(foley + "Swing" + str(i + 1) + ".wav", swing, ["swings"], .48)
        if i == 0:
            emit(base + "Gameplay/StrikeWhoosh.wav", swing, ["swings"], .48)
    emit(foley + "KickSwing.wav", trim(read("bamboo", 0, .412,
         "highpass=f=90,lowpass=f=8500", .87)), ["bamboo"], .55)

    # Two distinct fractures; the saturated loud final take is deliberately excluded.
    fracture = level(trim(read("crack", .27, .62), .018))
    debris = level(trim(read("crack", .71, .95), .018))
    emit(base + "DojoBoardCrack.wav", mix((wood, .55, 0), (fracture, .72, .005),
         (debris, .28, .065)), ["wood", "crack"], .70)
    emit(foley + "WoodCreak.wav", trim(read("creak", 0, .987)), ["creak"], .42, .06)
    # Sliding doors: a softened real wood rub with a quiet wooden stop.
    rub = level(trim(read("creak", .13, .8, "highpass=f=110,lowpass=f=2800", .85)))
    emit(base + "DojoDoorSlide.wav", mix((rub, .75, 0), (wood, .15, len(rub) / RATE - .05)),
         ["creak", "wood"], .48, .055)
    for i, (start, end) in enumerate(((0, .35), (.42, .76), (.88, 1.23))):
        step = trim(read("steps", start, end, "highpass=f=80,lowpass=f=5500"), .045)
        emit(foley + "Footstep" + str(i + 1) + ".wav", step, ["steps"], .46)
        if i == 0:
            emit(base + "TrainingFootstep.wav", step, ["steps"], .46)

    paper = trim(read("swings", 1.82, 2.25, "highpass=f=320,lowpass=f=4200"), .02)
    emit(foley + "RoomTransition.wav", paper, ["swings"], .32, .06)
    emit("Assets/Game/Resources/UI/DojoClick.wav", mix((wood, .35, 0),
         (level(paper[:int(.12 * RATE)]), .05, 0)), ["wood", "swings"], .35, .03)

    breeze = read("wind", 10, 22, "highpass=f=180,lowpass=f=5000")
    # Circular equal-gain overlap makes a seamless 10-second loop; no runtime DSP/reverb.
    overlap = 2 * RATE
    body = breeze[overlap:]
    for i in range(overlap):
        t = i / (overlap - 1)
        body[len(body) - overlap + i] = breeze[len(breeze) - overlap + i] * (1 - t) + breeze[i] * t
    emit(foley + "CourtyardWind.wav", body, ["wind"], .35, .0005)
    emit(base + "SummitGust.wav", breeze[:3 * RATE], ["wind"], .28, .7)
    # A restrained double wooden acknowledgement. No synthetic substitute for an unobtained bell.
    emit(foley + "TrainingComplete.wav", mix((wood, .5, 0), (wood, .8, .14)), ["wood"], .48, .07)
    emit(foley + "TrainingFailed.wav", mix((wood, .4, 0), (rub, .18, .06)), ["wood", "creak"], .38, .12)

    destination = SOURCES / "outputs.json"
    destination.write_text(json.dumps(manifest, indent=2), encoding="utf-8")
    for item in manifest:
        print(item["path"], item["seconds"], item["peak_db"], item["rms_db"])


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--fetch", action="store_true")
    args = parser.parse_args()
    if args.fetch:
        fetch()
    bake()
