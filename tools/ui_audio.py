"""Rebuild the original, loopable menu ambience and short interface tones."""
from pathlib import Path
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[1] / "ASS/UI/Audio"
RATE = 22050


def write(name, samples):
    samples = np.clip(samples, -0.98, 0.98)
    with wave.open(str(ROOT / name), "wb") as file:
        file.setnchannels(2)
        file.setsampwidth(2)
        file.setframerate(RATE)
        file.writeframes((samples * 32767).astype("<i2").tobytes())


def build():
    ROOT.mkdir(parents=True, exist_ok=True)
    seconds = 32
    t = np.arange(RATE * seconds) / RATE
    stereo = np.zeros((len(t), 2))
    # Whole cycles over the loop avoid a splice transient; no sampled sound is used.
    for i, frequency in enumerate([55, 82.5, 110, 146.84375, 165, 220]):
        drift = 0.08 * np.sin(2 * np.pi * t / seconds + i)
        pad = np.sin(2 * np.pi * frequency * t + drift)
        pad += 0.12 * np.sin(2 * np.pi * frequency * 2 * t)
        gain = (0.58 + 0.18 * np.cos(2 * np.pi * t / seconds + i)) / (i + 2)
        pan = 0.3 * np.sin(2 * np.pi * t / seconds + i * 0.8)
        stereo[:, 0] += pad * gain * (0.7 - pan)
        stereo[:, 1] += pad * gain * (0.7 + pan)
    write("orbital_ambient.wav", stereo * 0.25)
    for name, seconds, notes, volume in [
        ("hover.wav", 0.07, [660], 0.075),
        ("confirm.wav", 0.38, [330, 495, 660], 0.12),
    ]:
        t = np.arange(int(RATE * seconds)) / RATE
        envelope = (1 - np.exp(-t * 300)) * np.exp(-t * (45 if name == "hover.wav" else 12))
        sound = sum(np.sin(2 * np.pi * f * t) for f in notes) / len(notes)
        samples = sound * envelope * volume
        write(name, np.column_stack([samples, samples]))
    t = np.arange(int(RATE * 3.6)) / RATE
    envelope = np.sin(np.pi * t / 3.6) ** 2
    phase = 2 * np.pi * (82.5 * t + 5 * t * t)
    swell = (np.sin(phase) + 0.18 * np.sin(phase * 2) + 0.07 * np.sin(phase * 4))
    shimmer = np.sin(2 * np.pi * 440 * t) * np.exp(-((t - 1.9) / 0.8) ** 2)
    sound = (swell * 0.07 + shimmer * 0.015) * envelope
    write("focus.wav", np.column_stack([sound, sound]))


if __name__ == "__main__":
    build()
