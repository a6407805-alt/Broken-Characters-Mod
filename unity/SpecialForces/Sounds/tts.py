import sys, soundfile as sf
from kokoro_onnx import Kokoro
k = Kokoro("kokoro-v1.0.onnx", "voices-v1.0.bin")
print([v for v in k.get_voices() if v[:2] in ("af", "bf")])
text = ("Team, you must defend the control tower. It is the only thing keeping this rainstorm at bay. "
        "Deploy, and defend it. Then push out, find the cause of this, and put that scum down for good.")
for voice in sys.argv[1:]:
    samples, sr = k.create(text, voice=voice, speed=1.0, lang="en-us" if voice.startswith("a") else "en-gb")
    sf.write(f"raw_{voice}.wav", samples, sr)
    print(voice, sr, len(samples) / sr)
