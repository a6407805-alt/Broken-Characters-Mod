"""Synthesizes every Special Forces sound (44.1 kHz 16-bit mono wavs, seeded) into ../SpecialForces/Sounds/.

    python make_sounds.py <dir with raw_af_sarah.wav and raw_bf_emma.wav>

The radio voice lines are Kokoro TTS (kokoro-onnx, voices af_sarah / bf_emma) rendered by tts.py, then run through
a radio chain here: band-pass 300-3400 Hz, soft clipping, compression, static bed and squelch clicks.
Everything else is synthesized from noise and oscillators (same toolkit as Nidus's and Ashwing Drake's sounds:
filtered noise, falling sines, a convolution reverb with a decaying noise impulse response).
"""
import os
import sys

import numpy as np
import soundfile as sf
from scipy import signal

SR = 44100
rng = np.random.default_rng(1977)
HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "SpecialForces", "Sounds")


def t(seconds):
    return np.arange(int(seconds * SR)) / SR


def norm(x, peak=0.9):
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 0 else x


def bandpass(x, lo, hi, order=4):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lowpass(x, hi, order=4):
    return signal.sosfilt(signal.butter(order, hi, btype="low", fs=SR, output="sos"), x)


def highpass(x, lo, order=4):
    return signal.sosfilt(signal.butter(order, lo, btype="high", fs=SR, output="sos"), x)


def env(n, attack, decay_shape, total):
    e = np.ones(n)
    a = int(attack * SR)
    if a > 0:
        e[:a] = np.linspace(0, 1, a)
    k = np.arange(n - a) / SR
    e[a:] = np.exp(-k / max(1e-4, decay_shape))
    return e[:n]


def reverb(x, seconds=1.8, mix=0.35, damp=3000):
    n = int(seconds * SR)
    ir = rng.standard_normal(n) * np.exp(-np.arange(n) / (n / 5))
    ir = lowpass(ir, damp, 2)
    dry = np.concatenate([x, np.zeros(n)])
    wet = signal.fftconvolve(x, ir)[: len(dry)]
    wet = np.pad(wet, (0, len(dry) - len(wet)))
    return dry * (1 - mix) + norm(wet, np.max(np.abs(x)) + 1e-9) * mix


def write(name, x, peak=0.9):
    x = norm(np.nan_to_num(x), peak)
    path = os.path.join(OUT, name + ".wav")
    sf.write(path, x.astype(np.float32), SR, subtype="PCM_16")
    rms = float(np.sqrt(np.mean(x ** 2)))
    print(f"{name}.wav  {len(x) / SR:5.2f} s  peak {np.max(np.abs(x)):.2f}  rms {rms:.3f}")


# ---------------------------------------------------------------- radio

def resample(x, sr_from):
    return signal.resample_poly(x, SR, sr_from)


def squelch(seconds=0.12):
    n = int(seconds * SR)
    burst = highpass(rng.standard_normal(n), 1500) * np.exp(-np.arange(n) / (n / 4))
    click = np.zeros(n)
    click[:30] = np.hanning(60)[:30]
    return burst * 0.5 + click * 0.8


def radio(voice_path):
    v, sr = sf.read(voice_path)
    if v.ndim > 1:
        v = v.mean(axis=1)
    v = resample(v, sr)
    v = bandpass(v, 300, 3400, 6)
    v = np.tanh(norm(v, 1.0) * 2.2) / np.tanh(2.2)                 # soft clipping: a cheap radio speaker
    # Simple compressor: even out loud and quiet syllables.
    level = signal.sosfilt(signal.butter(2, 12, fs=SR, output="sos"), np.abs(v)) + 0.05
    v = v / np.power(level, 0.45)
    v = bandpass(v, 320, 3200, 4)
    # Limit the few loud spikes the compressor leaves, so the speech itself sits near full level.
    v = np.tanh(norm(v, 1.0) * 3.0)
    v = norm(v, 0.8)
    pad = np.zeros(int(0.25 * SR))
    body = np.concatenate([pad, v, pad])
    static = bandpass(rng.standard_normal(len(body)), 800, 5000) * 0.05
    crackle = (rng.random(len(body)) > 0.9993) * rng.standard_normal(len(body)) * 0.08
    body = body + static + bandpass(crackle, 1000, 6000)
    return np.concatenate([squelch(), body, squelch(0.18)])


# ---------------------------------------------------------------- weather

def thunder(seconds, seed):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    x = np.zeros(n)
    crack_n = int(0.25 * SR)
    crack = highpass(r.standard_normal(crack_n), 600) * np.exp(-np.arange(crack_n) / (0.05 * SR))
    x[:crack_n] += crack * 0.7
    # Rolling rumble: low noise with slow random amplitude bumps.
    rumble = lowpass(r.standard_normal(n), 140, 4)
    bumps = np.zeros(n)
    for _ in range(int(seconds * 3)):
        c = int(r.uniform(0.05, 0.8) * n)
        w = int(r.uniform(0.15, 0.6) * SR)
        lo, hi = max(0, c - w), min(n, c + w)
        bumps[lo:hi] += np.hanning(hi - lo) * r.uniform(0.4, 1.0)
    shape = np.exp(-np.arange(n) / (seconds * 0.35 * SR))
    x += norm(rumble, 1) * (0.35 + bumps) * shape
    return reverb(x, 2.5, 0.4, 900)


def rain_loop(seconds=12.0):
    n = int(seconds * SR)
    hiss = bandpass(rng.standard_normal(n), 1500, 9000) * 0.35
    body = bandpass(rng.standard_normal(n), 300, 1500) * 0.25
    drops = np.zeros(n)
    for _ in range(int(seconds * 220)):
        p = rng.integers(0, n - 400)
        f = rng.uniform(1800, 5000)
        k = np.arange(300) / SR
        drops[p:p + 300] += np.sin(2 * np.pi * f * k) * np.exp(-k / 0.0025) * rng.uniform(0.05, 0.25)
    x = hiss + body + drops
    # Seamless loop: crossfade the last second into the first.
    fade = int(1.0 * SR)
    ramp = np.linspace(0, 1, fade)
    x[:fade] = x[:fade] * ramp + x[-fade:] * (1 - ramp)
    return x[:-fade]


def wind(seconds, seed, sweep=True):
    r = np.random.default_rng(seed)
    n = int(seconds * SR)
    noise = r.standard_normal(n)
    out = np.zeros(n)
    block = 2048
    for i in range(0, n, block):
        ph = i / n
        centre = 300 + 900 * (np.sin(ph * np.pi) if sweep else 0.5)
        seg = noise[max(0, i - 4096): i + block]
        f = bandpass(seg, centre * 0.6, centre * 1.6, 2)[-min(block, n - i):]
        out[i:i + len(f)] = f
    shape = np.sin(np.linspace(0, np.pi, n)) ** 0.7
    return out * shape


# ---------------------------------------------------------------- boss

def roar(seconds, base, seed):
    r = np.random.default_rng(seed)
    tt = t(seconds)
    f = base * (1 + 0.25 * np.sin(2 * np.pi * 0.7 * tt)) * np.linspace(1.1, 0.8, len(tt))
    phase = 2 * np.pi * np.cumsum(f) / SR
    growl = sum(np.sin(phase * h + r.uniform(0, 6)) / h for h in range(1, 9))
    am = 1 + 0.6 * np.sin(2 * np.pi * 28 * tt)                          # throat rattle
    noise = bandpass(r.standard_normal(len(tt)), 200, 2500) * 0.6
    x = (growl * am * 0.6 + noise) * env(len(tt), 0.25, seconds * 0.5, seconds)
    x += wind(seconds, seed + 1) * 0.5
    return reverb(x, 2.2, 0.35, 2500)


def lightning_crack(seed):
    r = np.random.default_rng(seed)
    n = int(0.9 * SR)
    snap = highpass(r.standard_normal(n), 2000) * np.exp(-np.arange(n) / (0.03 * SR))
    sizzle = bandpass(r.standard_normal(n), 3000, 9000) * np.exp(-np.arange(n) / (0.25 * SR)) * 0.4
    boom = lowpass(r.standard_normal(n), 200) * np.exp(-np.arange(n) / (0.2 * SR))
    return reverb(snap + sizzle + norm(boom, 0.8), 1.2, 0.3, 4000)


def slam(seed):
    r = np.random.default_rng(seed)
    tt = t(1.4)
    thump = np.sin(2 * np.pi * np.cumsum(np.linspace(90, 30, len(tt))) / SR) * np.exp(-tt / 0.25)
    debris = bandpass(r.standard_normal(len(tt)), 300, 3000) * np.exp(-tt / 0.4) * 0.5
    return reverb(thump + debris, 1.5, 0.3, 2000)


# ---------------------------------------------------------------- base and lander

def engine(seconds, seed):
    r = np.random.default_rng(seed)
    tt = t(seconds)
    hum = sum(np.sin(2 * np.pi * f * tt) * a for f, a in ((55, 1), (110, 0.5), (165, 0.3), (220, 0.15)))
    jet = bandpass(r.standard_normal(len(tt)), 400, 4000) * 0.6
    x = (hum * 0.5 + jet) * np.clip(np.minimum(tt / 1.5, (seconds - tt) / 1.5), 0, 1)
    return x


def landing(seed):
    r = np.random.default_rng(seed)
    tt = t(2.5)
    thud = np.sin(2 * np.pi * np.cumsum(np.linspace(70, 35, len(tt))) / SR) * np.exp(-tt / 0.35)
    hiss = bandpass(r.standard_normal(len(tt)), 1500, 7000) * np.exp(-(tt - 0.3).clip(0) / 0.8) * 0.5
    clank = np.zeros(len(tt))
    k = int(0.9 * SR)
    seg = t(0.6)
    clank[k:k + len(seg)] = sum(np.sin(2 * np.pi * f * seg) for f in (420, 987, 1530)) * np.exp(-seg / 0.12) * 0.3
    return reverb(thud + hiss + clank, 1.4, 0.3, 3000)


def alarm():
    tt = t(1.6)
    f = np.where((tt * 2.5) % 1 < 0.5, 880, 660)
    x = signal.square(2 * np.pi * np.cumsum(f) / SR) * 0.4
    return lowpass(x, 3000) * np.clip(np.minimum(tt / 0.02, (1.6 - tt) / 0.1), 0, 1)


def build_clunk(seed):
    r = np.random.default_rng(seed)
    tt = t(0.6)
    thunk = np.sin(2 * np.pi * np.cumsum(np.linspace(180, 80, len(tt))) / SR) * np.exp(-tt / 0.08)
    ratchet = np.zeros(len(tt))
    for i in range(4):
        p = int((0.12 + i * 0.06) * SR)
        ratchet[p:p + 400] += highpass(r.standard_normal(400), 2000) * np.exp(-np.arange(400) / 80)
    return thunk + ratchet * 0.5


def nest_death(seed):
    r = np.random.default_rng(seed)
    tt = t(1.8)
    crunch = bandpass(r.standard_normal(len(tt)), 200, 2500) * np.exp(-tt / 0.5)
    zap = np.sin(2 * np.pi * np.cumsum(np.linspace(1800, 200, len(tt))) / SR) * np.exp(-tt / 0.3) * 0.4
    return reverb(crunch + zap + lightning_crack(seed + 7)[: len(tt)] * 0.5, 1.5, 0.3, 3000)


def main():
    os.makedirs(OUT, exist_ok=True)
    raw = sys.argv[1] if len(sys.argv) > 1 else HERE
    write("radio", radio(os.path.join(raw, "raw_af_sarah.wav")), 0.85)
    write("radio_alt", radio(os.path.join(raw, "raw_bf_emma.wav")), 0.85)
    for i in range(3):
        write(f"thunder{i + 1}", thunder(5.0 + i, 100 + i))
    write("rain", rain_loop(), 0.6)
    write("storm_wind", wind(8.0, 7), 0.7)
    write("boss_roar", roar(3.2, 48, 11))
    write("boss_growl", roar(1.6, 70, 12), 0.8)
    write("boss_lightning", lightning_crack(13))
    write("boss_slam", slam(14))
    write("boss_cyclone", wind(4.0, 15), 0.8)
    write("lander_engine", engine(12.0, 16), 0.7)
    write("lander_land", landing(17))
    write("tower_alarm", alarm(), 0.6)
    write("build", build_clunk(18), 0.7)
    write("nest_death", nest_death(19))


if __name__ == "__main__":
    main()
