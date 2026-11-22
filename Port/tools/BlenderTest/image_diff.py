"""Compares two pictures of the same size (for measuring how close an import renders to Blender's render).

    blender -b --factory-startup --python image_diff.py -- a.png b.png [diff.png]

Prints the mean absolute difference per channel (0..255), the share of pixels that differ by more than 32 in
some channel, the mean brightness of both pictures, and writes a picture of the differences when asked.
Blender's Python is used because it has numpy and reads PNG files.
"""

import sys

import bpy
import numpy as np


def load(path):
    image = bpy.data.images.load(path)
    w, h = image.size
    px = np.empty(w * h * 4, np.float32)
    image.pixels.foreach_get(px)
    return px.reshape(h, w, 4)[..., :3] * 255, image


def main():
    args = sys.argv[sys.argv.index("--") + 1:]
    a, _ = load(args[0])
    b, image = load(args[1])
    if a.shape != b.shape:
        raise SystemExit("different sizes: %s and %s" % (a.shape, b.shape))
    d = np.abs(a - b)
    luma = np.array([0.2126, 0.7152, 0.0722])
    print("RESULT mean_abs=%.2f r=%.2f g=%.2f b=%.2f over32=%.1f%% luma_a=%.1f luma_b=%.1f" % (
        d.mean(), d[..., 0].mean(), d[..., 1].mean(), d[..., 2].mean(), 100.0 * (d.max(axis=2) > 32).mean(),
        (a * luma).sum(axis=2).mean(), (b * luma).sum(axis=2).mean()))
    if len(args) > 2:
        out = bpy.data.images.new("diff", a.shape[1], a.shape[0])
        px = np.ones((a.shape[0], a.shape[1], 4), np.float32)
        px[..., :3] = np.clip(d / 64.0, 0, 1)
        out.pixels.foreach_set(px.ravel())
        out.filepath_raw = args[2]
        out.file_format = "PNG"
        out.save()


main()
