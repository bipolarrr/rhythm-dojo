"""Bake the Gameplay UI sprite sheet at eight texels per drawing unit (requires resvg-py)."""

from pathlib import Path
import xml.etree.ElementTree as ET

from resvg_py import svg_to_bytes


def main():
    tools = Path(__file__).resolve().parent
    source = ET.parse(tools / "Art" / "ArcadeField.svg").getroot()
    destination = tools.parent / "Assets" / "Game" / "Art" / "ArcadeField"
    namespace = "{http://www.w3.org/2000/svg}"
    ET.register_namespace("", namespace[1:-1])
    ET.register_namespace("xlink", "http://www.w3.org/1999/xlink")

    for sprite in source.findall(namespace + "svg"):
        canvas = ET.Element(namespace + "svg", {key: sprite.attrib[key] for key in ("width", "height")})
        canvas.append(source.find(namespace + "defs"))
        canvas.extend(sprite)
        png = svg_to_bytes(svg_string=ET.tostring(canvas, encoding="unicode"), zoom=8)
        (destination / (sprite.attrib["id"] + ".png")).write_bytes(png)


if __name__ == "__main__":
    main()
