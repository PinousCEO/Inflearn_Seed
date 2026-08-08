from pathlib import Path

from PIL import Image, ImageDraw


WIDTH, HEIGHT = 1024, 256
SCALE = 4
OUTPUT = Path("Assets/05_Resources/UI/Title/Login")


def polygon(inset: int):
    cut = 34
    return [
        (inset + cut, inset),
        (WIDTH - inset - cut, inset),
        (WIDTH - inset, inset + cut),
        (WIDTH - inset, HEIGHT - inset - cut),
        (WIDTH - inset - cut, HEIGHT - inset),
        (inset + cut, HEIGHT - inset),
        (inset, HEIGHT - inset - cut),
        (inset, inset + cut),
    ]


def scaled(points):
    return [(x * SCALE, y * SCALE) for x, y in points]


def make_panel(filename: str, top, bottom, inner_line):
    canvas = Image.new("RGBA", (WIDTH * SCALE, HEIGHT * SCALE), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    # Restrained antique-gold frame matching the existing bright fantasy UI.
    draw.polygon(scaled(polygon(8)), fill=(70, 52, 35, 255))
    draw.line(scaled(polygon(8) + [polygon(8)[0]]), fill=(34, 27, 22, 255), width=3 * SCALE)
    draw.polygon(scaled(polygon(13)), fill=(211, 172, 112, 255))
    draw.line(scaled(polygon(13) + [polygon(13)[0]]), fill=(247, 220, 164, 255), width=2 * SCALE)
    draw.polygon(scaled(polygon(20)), fill=(92, 70, 45, 255))

    # Render the face as a clipped vertical gradient.
    mask = Image.new("L", canvas.size, 0)
    ImageDraw.Draw(mask).polygon(scaled(polygon(24)), fill=255)
    gradient = Image.new("RGBA", canvas.size)
    pixels = gradient.load()
    y0, y1 = 24 * SCALE, (HEIGHT - 24) * SCALE
    for y in range(y0, y1 + 1):
        t = (y - y0) / max(1, y1 - y0)
        color = tuple(round(top[i] * (1 - t) + bottom[i] * t) for i in range(3)) + (255,)
        for x in range(20 * SCALE, (WIDTH - 20) * SCALE):
            pixels[x, y] = color
    canvas.alpha_composite(Image.composite(gradient, Image.new("RGBA", canvas.size), mask))

    draw = ImageDraw.Draw(canvas)
    draw.line(scaled(polygon(27) + [polygon(27)[0]]), fill=inner_line, width=2 * SCALE)
    draw.line(scaled(polygon(31) + [polygon(31)[0]]), fill=(*top, 150), width=2 * SCALE)

    canvas.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS).save(OUTPUT / filename)


def make_guest_icon():
    size = 256
    scale = 4
    canvas = Image.new("RGBA", (size * scale, size * scale), (0, 0, 0, 0))
    draw = ImageDraw.Draw(canvas)

    def ellipse(box, fill, outline=None, width=1):
        draw.ellipse(tuple(v * scale for v in box), fill=fill, outline=outline, width=width * scale)

    # Gold-rimmed medallion matching the guest panel.
    ellipse((12, 12, 244, 244), (61, 47, 32, 255), (25, 22, 20, 255), 4)
    ellipse((19, 19, 237, 237), (205, 168, 105, 255), (245, 218, 163, 255), 3)
    ellipse((29, 29, 227, 227), (31, 42, 55, 255), (83, 96, 108, 255), 3)

    # Simple generic guest bust; intentionally neutral and text-free.
    ellipse((91, 58, 165, 132), (246, 238, 210, 255), (154, 132, 94, 255), 2)
    draw.rounded_rectangle(
        (62 * scale, 137 * scale, 194 * scale, 211 * scale),
        radius=34 * scale,
        fill=(246, 238, 210, 255),
        outline=(154, 132, 94, 255),
        width=2 * scale,
    )
    # Flatten the lower edge so the bust reads clearly at small sizes.
    draw.rectangle((62 * scale, 181 * scale, 194 * scale, 211 * scale), fill=(246, 238, 210, 255))
    draw.line(
        ((62 * scale, 211 * scale), (194 * scale, 211 * scale)),
        fill=(154, 132, 94, 255),
        width=2 * scale,
    )

    canvas.resize((size, size), Image.Resampling.LANCZOS).save(OUTPUT / "Icon_GuestLogin.png")


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    make_panel(
        "Panel_GoogleLogin.png",
        top=(255, 254, 249),
        bottom=(238, 234, 222),
        inner_line=(189, 181, 161, 255),
    )
    make_panel(
        "Panel_GuestLogin.png",
        top=(37, 51, 66),
        bottom=(20, 29, 40),
        inner_line=(77, 94, 108, 255),
    )
    make_guest_icon()


if __name__ == "__main__":
    main()
