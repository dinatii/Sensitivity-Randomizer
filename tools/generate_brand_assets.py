"""Generate deterministic PNG and multi-resolution ICO files for the app mark."""

from pathlib import Path
import math

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parents[1]
ASSETS = ROOT / "assets"
SCALE = 2
SIZE = 1024 * SCALE


def lerp(left, right, amount):
    return tuple(round(a + (b - a) * amount) for a, b in zip(left, right))


def gradient_color(position):
    teal = (68, 224, 208, 255)
    blue = (111, 197, 232, 255)
    purple = (167, 139, 250, 255)
    if position <= 0.52:
        return lerp(teal, blue, position / 0.52)
    return lerp(blue, purple, (position - 0.52) / 0.48)


def cubic(start, control_a, control_b, end, steps=180):
    points = []
    for index in range(steps + 1):
        t = index / steps
        inverse = 1.0 - t
        x = (
            inverse ** 3 * start[0]
            + 3 * inverse * inverse * t * control_a[0]
            + 3 * inverse * t * t * control_b[0]
            + t ** 3 * end[0]
        )
        y = (
            inverse ** 3 * start[1]
            + 3 * inverse * inverse * t * control_a[1]
            + 3 * inverse * t * t * control_b[1]
            + t ** 3 * end[1]
        )
        points.append((round(x * SCALE), round(y * SCALE)))
    return points


def build_logo():
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    gradient = Image.new("RGBA", (SIZE, SIZE))
    pixels = gradient.load()
    for y in range(SIZE):
        for x in range(SIZE):
            position = max(0.0, min(1.0, (x + (SIZE - y)) / (2.0 * SIZE)))
            pixels[x, y] = gradient_color(position)

    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle(
        (64 * SCALE, 64 * SCALE, 960 * SCALE, 960 * SCALE),
        radius=238 * SCALE,
        fill=255,
    )
    image.alpha_composite(Image.composite(gradient, Image.new("RGBA", image.size), mask))

    path = []
    segments = [
        ((727, 300), (660, 221), (531, 194), (412, 225)),
        ((412, 225), (309, 252), (253, 323), (275, 397)),
        ((275, 397), (297, 471), (385, 493), (502, 515)),
        ((502, 515), (645, 542), (755, 583), (754, 686)),
        ((754, 686), (752, 803), (640, 865), (505, 865)),
        ((505, 865), (389, 865), (292, 825), (239, 751)),
    ]
    for segment in segments:
        points = cubic(*segment)
        if path: points = points[1:]
        path.extend(points)

    draw = ImageDraw.Draw(image)
    width = 86 * SCALE
    draw.line(path, fill=(255, 255, 255, 255), width=width, joint="curve")
    radius = width // 2
    for x, y in (path[0], path[-1]):
        draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=(255, 255, 255, 255))

    for x, y in ((727, 300), (239, 751)):
        radius = 23 * SCALE
        draw.ellipse(
            (x * SCALE - radius, y * SCALE - radius, x * SCALE + radius, y * SCALE + radius),
            fill=(255, 255, 255, 184),
        )

    return image.resize((1024, 1024), Image.Resampling.LANCZOS)


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    logo = build_logo()
    logo.save(ASSETS / "SensitivityRandomizer-logo.png", optimize=True)
    logo.save(
        ASSETS / "SensitivityRandomizer.ico",
        format="ICO",
        sizes=[(16, 16), (24, 24), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)],
    )


if __name__ == "__main__":
    main()
