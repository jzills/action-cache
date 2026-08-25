#!/usr/bin/env python3
"""Draw the ActionCache marks as vector geometry.

    python3 site/tools/gen-marks.py

Writes the marks and the site's icon set, all committed. This script exists so
the geometry is reproducible and reviewable, not so it can run at build time.

    site/assets/ac-wordmark.svg   the hero wordmark, currentColor
    site/assets/ac-mark.svg       the compact navbar mark, currentColor
    site/static/favicon.svg       the tab icon, brand colour
    site/static/favicon.ico       16 and 32, for browsers predating SVG icons
    site/static/favicon-*.png     the same two as PNG
    site/static/apple-touch-icon.png, android-chrome-*.png

The icons are the compact mark rather than the wordmark: a tab is sixteen
pixels wide, where ACTION over CACHE is a smudge and "AC" is at least a shape.
Without these the site publishes Hextra's own icon, so the tab claims to be the
theme's documentation rather than this project's.

Why this exists: resources/banner.svg draws the banner as twelve <text> lines
of box-drawing characters. That renders correctly only where the reader has a
monospace font carrying those glyphs; anywhere else the letters come out as
tofu boxes or shear into offset blocks. It also cannot take a colour from the
page, because an <img> has no access to currentColor and an inlined <style>
with a bare `text {}` rule would leak to every other SVG on the page.

ART below is that banner transcribed character for character, so what is drawn
here is the same banner and not a redesign.

The art is a fixed character grid, so the conversion is mechanical. The
box-drawing characters are a double line, which at any size this is actually
displayed reads as one stroke -- so each is drawn as a single bar of BAR
thickness, which is what those characters are already trying to look like.
"""

import io
import pathlib
import struct

# The ACTION and CACHE banners, transcribed from resources/banner.svg.
ACTION = [
    " █████╗  ██████╗████████╗██╗ ██████╗ ███╗   ██╗",
    "██╔══██╗██╔════╝╚══██╔══╝██║██╔═══██╗████╗  ██║",
    "███████║██║        ██║   ██║██║   ██║██╔██╗ ██║",
    "██╔══██║██║        ██║   ██║██║   ██║██║╚██╗██║",
    "██║  ██║╚██████╗   ██║   ██║╚██████╔╝██║ ╚████║",
    "╚═╝  ╚═╝ ╚═════╝   ╚═╝   ╚═╝ ╚═════╝ ╚═╝  ╚═══╝",
]

CACHE = [
    " ██████╗ █████╗  ██████╗██╗  ██╗███████╗",
    "██╔════╝██╔══██╗██╔════╝██║  ██║██╔════╝",
    "██║     ███████║██║     ███████║█████╗  ",
    "██║     ██╔══██║██║     ██╔══██║██╔══╝  ",
    "╚██████╗██║  ██║╚██████╗██║  ██║███████╗",
    " ╚═════╝╚═╝  ╚═╝ ╚═════╝╚═╝  ╚═╝╚══════╝",
]

# One monospace cell. 3:5 is roughly a terminal cell, which is the proportion
# the art was drawn for -- at 1:1 the letters come out squat.
CELL_W = 12.0
CELL_H = 20.0

# The thickness of a box-drawing stroke, as a fraction of the cell. The source
# glyphs are double lines; at the sizes these marks are displayed the gap
# between the two lines is under a pixel, so one bar of this weight is both
# what it looks like and what survives being scaled down.
BAR = 0.34

# One cell of padding around the art.
MARGIN = 1

# The icon's brand colour, matching --ac-brand in site/assets/css/custom.css.
# One literal for both themes: a tab strip may be light or dark and the icon
# cannot ask which, so the colour has to carry on either.
BRAND = "#8b5cf6"

# The plate behind the icons that are composited rather than drawn on the page.
# iOS flattens apple-touch-icon onto black and Android draws the manifest icons
# on a plate of its own, so transparency there means a mark floating on whatever
# the platform picked. The tab icons stay transparent.
PLATE = "#0d1117"

# Padding around the mark inside the square icon canvas, in user units. Kept
# small deliberately: at 16 pixels every unit spent on margin is taken from the
# six rows that have to stay distinguishable.
ICON_PAD = 8


def cell_rects(char, col, row):
    """The rectangles one character contributes, in cell-relative units."""
    x, y = col * CELL_W, row * CELL_H
    w, h = CELL_W, CELL_H
    bw, bh = w * BAR, h * BAR          # bar thickness, horizontal and vertical
    cx, cy = x + (w - bw) / 2, y + (h - bh) / 2   # centred bar origin

    if char == "█":
        return [(x, y, w, h)]
    if char == "║":
        return [(cx, y, bw, h)]
    if char == "═":
        return [(x, cy, w, bh)]
    # The corners are a half-bar to the centre plus a half-bar out the other
    # side, so a corner meeting its neighbour leaves no notch at the join.
    if char == "╔":
        return [(cx, cy, w - (cx - x), bh), (cx, cy, bw, h - (cy - y))]
    if char == "╗":
        return [(x, cy, cx - x + bw, bh), (cx, cy, bw, h - (cy - y))]
    if char == "╚":
        return [(cx, cy, w - (cx - x), bh), (cx, y, bw, cy - y + bh)]
    if char == "╝":
        return [(x, cy, cx - x + bw, bh), (cx, y, bw, cy - y + bh)]
    if char == " ":
        return []
    raise ValueError(f"no geometry for {char!r}")


def fmt(value):
    """Trim a float to the shortest form that still reads exactly."""
    return f"{value:.2f}".rstrip("0").rstrip(".")


def path_for(rows, col_offset=0.0, row_offset=0.0):
    """One path's worth of subpaths, all wound the same way.

    Every subpath winds clockwise so the default nonzero fill unions them.
    Adjacent cells share edges and the corner bars overlap their neighbours;
    under evenodd each of those overlaps would punch a hole in the letterform.
    """
    out = []
    for r, line in enumerate(rows):
        for c, char in enumerate(line):
            for (x, y, w, h) in cell_rects(char, c, r):
                x += col_offset * CELL_W
                y += row_offset * CELL_H
                out.append(
                    f"M{fmt(x)} {fmt(y)}"
                    f"L{fmt(x + w)} {fmt(y)}"
                    f"L{fmt(x + w)} {fmt(y + h)}"
                    f"L{fmt(x)} {fmt(y + h)}Z"
                )
    return "".join(out)


def svg(width, height, path, label, note):
    return (
        f"<!-- Generated by site/tools/gen-marks.py. Do not edit.\n"
        f"     {note} -->\n"
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {fmt(width)} {fmt(height)}"\n'
        f'     fill="currentColor"\n'
        f'     role="img" aria-label="{label}">\n'
        f'  <path d="{path}"/>\n'
        f"</svg>\n"
    )


def icon_svg(rows, colour, plate=None, crisp=True):
    """The compact mark centred in a square canvas, at a fixed colour.

    Square because every consumer of an icon assumes one; the mark is wider
    than it is tall, so the padding is uneven and computed rather than chosen.

    crispEdges snaps every bar to the pixel grid, which sharpens the mark at 32
    and above and destroys it below. The letterforms are outlines a third of a
    cell thick; at 16 pixels a cell is under two pixels, so snapping either
    doubles a stroke or drops it, and the result is noise rather than "AC".
    Antialiasing keeps the shape at the cost of softness, which at that size is
    the trade worth making -- so the small raster and the SVG a browser scales
    to the tab itself are drawn without it.
    """
    art_w = max(len(line) for line in rows) * CELL_W
    art_h = len(rows) * CELL_H
    side = art_w + 2 * ICON_PAD

    rendering = ' shape-rendering="crispEdges"' if crisp else ""
    path = path_for(rows, ICON_PAD / CELL_W, ((side - art_h) / 2) / CELL_H)
    background = (
        f'  <rect width="{fmt(side)}" height="{fmt(side)}" fill="{plate}"/>\n' if plate else ""
    )

    return (
        "<!-- Generated by site/tools/gen-marks.py. Do not edit. -->\n"
        f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {fmt(side)} {fmt(side)}"\n'
        f'     fill="{colour}"{rendering}\n'
        '     role="img" aria-label="ActionCache">\n'
        f"{background}"
        f'  <path d="{path}"/>\n'
        "</svg>\n"
    )


def png_bytes(svg_text, size):
    """Rasterise an SVG to a square PNG of the given side."""
    import cairosvg

    return cairosvg.svg2png(
        bytestring=svg_text.encode("utf-8"),
        output_width=size,
        output_height=size,
    )


def ico_bytes(pngs):
    """Pack PNG images into an .ico.

    Written by hand rather than through an imaging library because the format
    is a header and a directory entry per image, and the alternative is a
    dependency for forty lines of struct packing.
    """
    count = len(pngs)
    header = struct.pack("<HHH", 0, 1, count)
    directory = b""
    offset = 6 + 16 * count

    for size, data in pngs:
        # 0 means 256 in this field; no icon here is that large, but the rule
        # is the format's rather than ours.
        dimension = 0 if size >= 256 else size
        directory += struct.pack(
            "<BBBBHHII", dimension, dimension, 0, 0, 1, 32, len(data), offset
        )
        offset += len(data)

    return header + directory + b"".join(data for _, data in pngs)


# Below this the pixel grid is coarser than the letterforms, so snapping to it
# costs more than it gains. See icon_svg.
CRISP_FLOOR = 32


def write_icons(static, rows):
    """Write the whole icon set from the compact mark."""
    static.mkdir(parents=True, exist_ok=True)

    def source(size, standalone):
        return icon_svg(rows, BRAND, PLATE if standalone else None, crisp=size >= CRISP_FLOOR)

    # Served to browsers that prefer an SVG icon, which scale it to the tab
    # themselves -- so it is the small-size variant, not the sharp one.
    (static / "favicon.svg").write_text(icon_svg(rows, BRAND, crisp=False))

    for name, size, standalone in [
        ("favicon-16x16.png", 16, False),
        ("favicon-32x32.png", 32, False),
        ("apple-touch-icon.png", 180, True),
        ("android-chrome-192x192.png", 192, True),
        ("android-chrome-512x512.png", 512, True),
    ]:
        (static / name).write_bytes(png_bytes(source(size, standalone), size))

    (static / "favicon.ico").write_bytes(
        ico_bytes([(size, png_bytes(source(size, False), size)) for size in (16, 32)])
    )

    print(f"wrote {static}/favicon.svg and the icon set")


def main():
    assets = pathlib.Path(__file__).resolve().parent.parent / "assets"

    # --- the wordmark: ACTION over CACHE, CACHE centred under ACTION --------
    cols = max(len(line) for line in ACTION)
    cache_cols = max(len(line) for line in CACHE)
    indent = (cols - cache_cols) / 2          # 3.5 cells, as in the original

    path = (
        path_for(ACTION, MARGIN, MARGIN)
        + path_for(CACHE, MARGIN + indent, MARGIN + len(ACTION))
    )
    width = (cols + 2 * MARGIN) * CELL_W
    height = (len(ACTION) + len(CACHE) + 2 * MARGIN) * CELL_H
    (assets / "ac-wordmark.svg").write_text(
        svg(width, height, path, "ActionCache",
            "The hero wordmark: the README banner as geometry rather than text.")
    )

    # --- the compact mark: the A of ACTION and the C beside it --------------
    # The full wordmark is illegible at navbar height, and the first two
    # letters are the two words. Columns 0-15 of the ACTION rows are exactly
    # its A and C, so the mark is cut from the banner rather than redrawn.
    ac = [line[:16] for line in ACTION]
    mark_path = path_for(ac, MARGIN, MARGIN)
    mark_w = (16 + 2 * MARGIN) * CELL_W
    mark_h = (len(ac) + 2 * MARGIN) * CELL_H
    (assets / "ac-mark.svg").write_text(
        svg(mark_w, mark_h, mark_path, "ActionCache",
            "The compact mark: the banner's A and C, for the navbar, where the\n"
            "     full wordmark is illegible.")
    )

    print(f"wrote {assets/'ac-wordmark.svg'} ({fmt(width)}x{fmt(height)})")
    print(f"wrote {assets/'ac-mark.svg'} ({fmt(mark_w)}x{fmt(mark_h)})")

    write_icons(assets.parent / "static", ac)


if __name__ == "__main__":
    main()
