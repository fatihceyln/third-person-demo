#!/usr/bin/env python3
"""Generates the footstep sounds in Assets/Audio/Footsteps. Usage: generate-footsteps.py Assets/Audio/Footsteps"""
import math, random, struct, sys, wave

SAMPLE_RATE = 44100

def lowpass(samples, cutoff):
    coefficient = 1 - math.exp(-2 * math.pi * cutoff / SAMPLE_RATE)
    state = 0.0
    output = []
    for sample in samples:
        state += coefficient * (sample - state)
        output.append(state)
    return output

def bandpass(samples, low, high):
    upper = lowpass(samples, high)
    lower = lowpass(upper, low)
    return [value - base for value, base in zip(upper, lower)]

def thump(length, start, start_freq, end_freq, decay, amplitude):
    output = [0.0] * length
    phase = 0.0
    begin = int(start * SAMPLE_RATE)
    for index in range(begin, length):
        time = (index - begin) / SAMPLE_RATE
        freq = end_freq + (start_freq - end_freq) * math.exp(-time / 0.02)
        phase += 2 * math.pi * freq / SAMPLE_RATE
        attack = min(1.0, time / 0.002)
        output[index] = amplitude * attack * math.exp(-time / decay) * math.sin(phase)
    return output

def noise_burst(length, rng, start, low, high, attack, decay, amplitude):
    raw = [rng.uniform(-1, 1) for _ in range(length)]
    filtered = bandpass(raw, low, high)
    peak = max(abs(value) for value in filtered) or 1
    begin = int(start * SAMPLE_RATE)
    output = [0.0] * length
    for index in range(begin, length):
        time = (index - begin) / SAMPLE_RATE
        envelope = min(1.0, time / attack) * math.exp(-time / decay)
        output[index] = amplitude * envelope * filtered[index] / peak * 3
    return output

def footstep(profile, seed):
    rng = random.Random(seed)
    jitter = lambda value, amount=0.12: value * rng.uniform(1 - amount, 1 + amount)
    length = int(profile["duration"] * SAMPLE_RATE)
    toe_time = jitter(profile["toe_time"], 0.2)
    layers = [
        # Heel strike: a short pitched-down body thump.
        thump(length, 0, jitter(profile["thump_start"]), jitter(profile["thump_end"]), jitter(profile["thump_decay"]), 1.0),
        # Sole slapping the floor.
        noise_burst(length, rng, 0, 80, jitter(profile["click_cutoff"]), 0.001, jitter(profile["click_decay"]), jitter(profile["click_level"])),
        # Toe rolling onto the ground a moment later.
        thump(length, toe_time, jitter(profile["thump_start"] * 1.2), jitter(profile["thump_end"] * 1.2), jitter(profile["thump_decay"] * 0.8), jitter(profile["toe_level"])),
        noise_burst(length, rng, toe_time, 80, jitter(profile["click_cutoff"]), 0.001, jitter(profile["click_decay"]), jitter(profile["toe_level"] * profile["click_level"])),
        # Light grit scuff under the sole.
        noise_burst(length, rng, jitter(0.01), jitter(1500), jitter(6000), 0.006, jitter(profile["scuff_decay"]), jitter(profile["scuff_level"], 0.3)),
    ]
    mixed = [sum(values) for values in zip(*layers)]
    fade = int(0.02 * SAMPLE_RATE)
    for index in range(fade):
        mixed[length - 1 - index] *= index / fade
    peak = max(abs(value) for value in mixed)
    gain = 10 ** (-6 / 20) / peak
    return [value * gain for value in mixed]

def write(path, samples):
    with wave.open(path, "wb") as file:
        file.setnchannels(1)
        file.setsampwidth(2)
        file.setframerate(SAMPLE_RATE)
        file.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, value)) * 32767)) for value in samples))

PROFILES = {
    # Grown-up character in sneakers: deeper, rounder steps.
    "Skater": dict(duration=0.22, thump_start=230, thump_end=140, thump_decay=0.016, click_cutoff=2800, click_decay=0.009, click_level=0.5, toe_time=0.05, toe_level=0.3, scuff_decay=0.035, scuff_level=0.08),
    # Small chibi character: lighter, higher and quicker taps.
    "Bearded": dict(duration=0.14, thump_start=330, thump_end=200, thump_decay=0.009, click_cutoff=5000, click_decay=0.005, click_level=0.45, toe_time=0.028, toe_level=0.22, scuff_decay=0.018, scuff_level=0.07),
}

for name, profile in PROFILES.items():
    for variant in range(1, 5):
        write(f"{sys.argv[1]}/Footstep{name}{variant:02d}.wav", footstep(profile, variant * 101 + len(name)))
