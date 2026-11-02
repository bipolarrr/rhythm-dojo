"""Record only the selected Windows speaker loopback during the player tone window."""
import argparse
import datetime
import json
import pathlib
import sys
import wave

parser = argparse.ArgumentParser()
parser.add_argument('--output', required=True)
parser.add_argument('--speaker-id')
parser.add_argument('--seconds', type=float, default=3)
args = parser.parse_args()
sys.path.insert(0, str(pathlib.Path(__file__).parent / 'python-audio'))
import numpy as np
import soundcard as sc

speaker = sc.default_speaker()
if args.speaker_id:
    speaker = sc.get_speaker(args.speaker_id)
loopback = sc.get_microphone(speaker.id, include_loopback=True)
if not loopback.isloopback:
    raise RuntimeError('Refusing to record a microphone instead of speaker loopback.')
rate = 48000
started = datetime.datetime.now(datetime.timezone.utc).isoformat()
data = loopback.record(numframes=int(rate * args.seconds), samplerate=rate, channels=2)
mono = data.mean(axis=1)
spectrum = np.abs(np.fft.rfft(mono * np.hanning(len(mono)))) ** 2
frequencies = np.fft.rfftfreq(len(mono), 1 / rate)
band = (frequencies >= 438) & (frequencies <= 442)
peak = float(np.abs(data).max())
rms = float(np.sqrt(np.mean(data ** 2)))
dominant = float(frequencies[int(spectrum.argmax())])
band_fraction = float(spectrum[band].sum() / max(float(spectrum.sum()), 1e-30))
report = {
    'started_utc': started,
    'finished_utc': datetime.datetime.now(datetime.timezone.utc).isoformat(),
    'speaker_id': speaker.id,
    'speaker_name': speaker.name,
    'loopback': loopback.isloopback,
    'sample_rate': rate,
    'frames': len(data),
    'peak': peak,
    'rms': rms,
    'dominant_hz': dominant,
    'power_fraction_438_to_442_hz': band_fraction,
    'passed': peak > .001 and abs(dominant - 440) <= 2 and band_fraction > .8,
    'scope': 'Windows speaker loopback; does not measure acoustic speaker output or perceived quality.'
}
output = pathlib.Path(args.output)
output.parent.mkdir(parents=True, exist_ok=True)
output.with_suffix('.json').write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding='utf-8')
with wave.open(str(output.with_suffix('.wav')), 'wb') as recording:
    recording.setnchannels(2)
    recording.setsampwidth(2)
    recording.setframerate(rate)
    recording.writeframes((np.clip(data, -1, 1) * 32767).astype('<i2').tobytes())
print(json.dumps(report, ensure_ascii=False))
sys.exit(0 if report['passed'] else 1)
