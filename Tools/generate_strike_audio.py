"""Compatibility entry point for the reviewed CC0 recording pipeline.

See Docs/sound-design.md. Source WAV generation no longer synthesizes foley.
"""
from prepare_recorded_audio import bake


if __name__ == "__main__":
    bake()
