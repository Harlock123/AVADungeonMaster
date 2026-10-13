#!/usr/bin/env python3
"""Synthesises AVADungeonMaster's sound effects and tunes into ../src/AVADungeonMaster/Sounds as 16-bit mono WAV.

Nothing is sampled from any other game: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVADungeonMaster", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Step.wav", tone(0.06, 110, 70, "noise", release=1.5), 0.22)
    write("Bump.wav", mix(tone(0.1, 90, 60, "square", release=1.2), tone(0.08, 300, 100, "noise", release=1.5)), 0.35)
    write("Door.wav", cat(tone(0.15, 130, 100, "saw", release=0.5, vibrato=0.06, vib_rate=30), tone(0.15, 160, 90, "noise", release=1.0)), 0.4)
    write("Gate.wav", mix(tone(0.9, 80, 60, "saw", release=0.3, vibrato=0.1, vib_rate=14), tone(0.9, 300, 120, "noise", release=0.5, gain=0.5)), 0.45)
    write("Unlock.wav", cat(tone(0.04, 2200, kind="tri", release=0.6), silence(0.04), tone(0.06, 1500, kind="tri", release=0.6), tone(0.1, 180, 120, "noise", release=1.0)), 0.38)
    write("Click.wav", cat(tone(0.03, 900, kind="square", release=1.0), tone(0.05, 300, kind="square", release=1.0)), 0.3)
    write("Swing.wav", tone(0.14, 1200, 300, "noise", release=0.6, gain=0.7), 0.25)
    write("Hit.wav", mix(tone(0.08, 500, 120, "noise", release=1.0), tone(0.07, 180, 90, "square", release=1.2)), 0.42)
    write("MonsterHurt.wav", tone(0.18, 260, 140, "saw", release=0.7, vibrato=0.1, vib_rate=20), 0.35)
    write("MonsterDie.wav", mix(tone(0.5, 300, 50, "saw", release=0.5, vibrato=0.15, vib_rate=11), tone(0.5, 400, 60, "noise", release=0.6, gain=0.5)), 0.45)
    write("Hurt.wav", tone(0.14, 220, 130, "square", release=0.8), 0.32)
    write("Die.wav", cat(*[tone(0.18, hz(n), kind="square", release=0.6) for n in ["E4", "C4", "A3"]], tone(0.6, hz("E3"), kind="tri", release=0.5)), 0.5)
    write("Fireball.wav", mix(tone(0.6, 200, 900, "noise", release=0.4), tone(0.6, 120, 300, "saw", release=0.5, gain=0.5)), 0.42)
    write("Lightning.wav", mix(tone(0.4, 3000, 200, "noise", release=0.6), tone(0.4, 1800, 300, "square", release=0.7, gain=0.4, vibrato=0.3, vib_rate=40)), 0.45)
    write("Spell.wav", mix(tone(0.55, 400, 1600, "sine", release=0.5, vibrato=0.08, vib_rate=14), tone(0.55, 800, 3200, "tri", release=0.7, gain=0.4)), 0.4)
    write("Fizzle.wav", tone(0.35, 600, 120, "noise", release=0.5, gain=0.6), 0.25)
    write("Heal.wav", cat(*[tone(0.09, hz(n), kind="sine", release=0.3) for n in ["C5", "E5", "G5", "C6", "E6"]]), 0.4)
    write("Explode.wav", mix(tone(0.5, 300, 40, "noise", release=0.5), tone(0.4, 120, 40, "square", release=0.6, gain=0.5)), 0.45)
    write("PickUp.wav", cat(tone(0.04, 700, kind="tri", release=0.6), tone(0.06, 1100, kind="tri", release=0.8)), 0.3)
    write("Drop.wav", cat(tone(0.06, 400, 200, "tri", release=0.8), tone(0.05, 300, 100, "noise", release=1.0)), 0.3)
    write("Eat.wav", cat(*[tone(0.06, 250, 150, "noise", release=0.8, gain=0.7) for _ in range(3)]), 0.3)
    write("Drink.wav", cat(*[tone(0.08, f, f * 1.4, "sine", release=0.6) for f in [400, 450, 500, 550]]), 0.35)
    write("Fall.wav", cat(tone(0.8, 900, 120, "tri", release=0.2), tone(0.3, 200, 40, "noise", release=0.6)), 0.45)
    write("Teleport.wav", mix(tone(0.7, 300, 2400, "sine", release=0.3, vibrato=0.2, vib_rate=18), tone(0.7, 2400, 300, "tri", release=0.4, gain=0.5)), 0.4)
    write("Stairs.wav", cat(*[tone(0.09, 160 - i * 12, 120 - i * 10, "noise", release=1.0) for i in range(5)]), 0.3)
    write("LevelUp.wav", tune(240, [("C5", 1), ("E5", 1), ("G5", 1), ("C6", 1), ("G5", 1), ("C6", 3)], [("C3", 4), ("G2", 2), ("C3", 2)]), 0.5)
    write("Menu.wav", tone(0.03, 1200, kind="tri", release=0.8), 0.2)
    write("Pick.wav", cat(tone(0.03, 900, kind="tri", release=0.6), tone(0.05, 1350, kind="tri", release=0.8)), 0.25)
    write("Roar.wav", mix(tone(1.6, 90, 50, "saw", release=0.4, vibrato=0.12, vib_rate=7), tone(1.6, 300, 80, "noise", release=0.5, gain=0.6)), 0.55)

    # The deep: a slow drone in D with distant drips and a falling figure now and then (new). Loops.
    seconds = 16.0
    n = int(RATE * seconds)
    drone = mix(tone(seconds, 73.42, kind="tri", attack=0.001, release=0.0, gain=0.5),
                tone(seconds, 110.0, kind="sine", attack=0.001, release=0.0, vibrato=0.004, vib_rate=0.25, gain=0.3))
    drips = [0.0] * n
    for t in [1.3, 4.1, 6.8, 9.5, 12.2, 14.9]:
        d = tone(0.25, 1800 + 300 * (t % 3), 900, "sine", release=1.5, gain=0.35)
        start = int(t * RATE)
        for i, v in enumerate(d):
            if start + i < n: drips[start + i] += v
    figure = [0.0] * n
    melody = cat(*[tone(0.9, hz(nt), kind="tri", attack=0.05, release=0.6, gain=0.28) for nt in ["D5", "C5", "A4", "F4", "E4", "D4"]])
    for i, v in enumerate(melody):
        if 8 * RATE + i < n: figure[int(8 * RATE) + i] += v
    write("Ambience.wav", mix(drone, drips, figure), 0.38)

    write("TitleTune.wav", tune(76,
        [("D4", 1), ("A4", 1), ("F5", 2), ("E5", 1), ("D5", 1), ("C5", 2),
         ("D5", 1), ("A4", 1), ("Bb4", 2), ("A4", 1), ("G4", 1), ("A4", 2),
         ("D5", 1), ("F5", 1), ("A5", 2), ("G5", 1), ("F5", 1), ("E5", 2),
         ("F5", 1), ("E5", 1), ("C#5", 2), ("D5", 4)],
        [(nt, 2) for nt in ["D3", "D3", "Bb2", "A2", "D3", "G2", "A2", "A2", "D3", "D3", "Bb2", "C3", "A2", "A2", "D3", "D3"]], kind="tri"), 0.55)
    write("Victory.wav", tune(100,
        [("D5", 0.5), ("F#5", 0.5), ("A5", 0.5), ("D6", 1.5), ("C#6", 0.5), ("A5", 0.5), ("B5", 0.5), ("D6", 2)],
        [("D3", 2), ("A2", 2), ("G2", 1), ("A2", 1), ("D3", 2)]), 0.55)
    write("Defeat.wav", tune(60,
        [("A4", 1), ("G4", 1), ("F4", 1), ("E4", 1), ("D4", 3)],
        [("D3", 2), ("Bb2", 2), ("D2", 3)], kind="tri"), 0.55)


if __name__ == "__main__":
    main()
