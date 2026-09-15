"""Extract, normalize, and validate independently generated desktop-pet frames."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from rembg import new_session, remove


CANVAS = (768, 512)
MAX_SUBJECT = (700, 430)
BASELINE_Y = 490


def parse_frames(values: list[str]) -> dict[str, Path]:
    frames: dict[str, Path] = {}
    for value in values:
        if "=" not in value:
            raise ValueError(f"frame must be NAME=PATH: {value}")
        name, raw_path = value.split("=", 1)
        frames[name] = Path(raw_path)
    return frames


def extract_and_normalize(source: Path, session) -> Image.Image:
    image = Image.open(source).convert("RGB")
    cutout = remove(
        image,
        session=session,
        alpha_matting=True,
        alpha_matting_foreground_threshold=235,
        alpha_matting_background_threshold=15,
        alpha_matting_erode_size=4,
        post_process_mask=True,
    ).convert("RGBA")

    pixels = np.asarray(cutout).copy()
    alpha = pixels[:, :, 3]
    alpha[alpha < 6] = 0
    pixels[:, :, 3] = alpha
    cutout = Image.fromarray(pixels, "RGBA")
    bbox = cutout.getchannel("A").getbbox()
    if bbox is None:
        raise RuntimeError(f"no foreground found in {source}")
    cutout = cutout.crop(bbox)

    scale = min(MAX_SUBJECT[0] / cutout.width, MAX_SUBJECT[1] / cutout.height)
    size = (max(1, round(cutout.width * scale)), max(1, round(cutout.height * scale)))
    cutout = cutout.resize(size, Image.Resampling.LANCZOS)

    canvas = Image.new("RGBA", CANVAS, (0, 0, 0, 0))
    x = (CANVAS[0] - cutout.width) // 2
    y = BASELINE_Y - cutout.height
    canvas.alpha_composite(cutout, (x, y))
    return canvas


def metrics(name: str, image: Image.Image) -> dict[str, object]:
    rgba = np.asarray(image.convert("RGBA"))
    alpha = rgba[:, :, 3]
    ys, xs = np.nonzero(alpha > 8)
    if len(xs) == 0:
        raise RuntimeError(f"{name}: empty alpha")
    edge = np.concatenate((alpha[0], alpha[-1], alpha[:, 0], alpha[:, -1]))
    bbox = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    return {
        "name": name,
        "mode": image.mode,
        "size": list(image.size),
        "bbox": bbox,
        "opaque_edge_pixels": int(np.count_nonzero(edge)),
        "foreground_fraction": round(float(np.count_nonzero(alpha > 8) / alpha.size), 4),
        "alpha_levels": int(len(np.unique(alpha))),
    }


def make_contact_sheet(frames: dict[str, Image.Image], path: Path) -> None:
    thumb_size = (384, 256)
    sheet = Image.new("RGB", (thumb_size[0] * 3, thumb_size[1] * 4), "white")
    draw = ImageDraw.Draw(sheet)
    for index, (name, image) in enumerate(frames.items()):
        x = (index % 3) * thumb_size[0]
        y = (index // 3) * thumb_size[1]
        tile = Image.new("RGBA", thumb_size, (238, 238, 238, 255))
        checker = ImageDraw.Draw(tile)
        step = 16
        for cy in range(0, thumb_size[1], step):
            for cx in range(0, thumb_size[0], step):
                if (cx // step + cy // step) % 2:
                    checker.rectangle((cx, cy, cx + step - 1, cy + step - 1), fill=(210, 210, 210, 255))
        tile.alpha_composite(image.resize(thumb_size, Image.Resampling.LANCZOS))
        sheet.paste(tile.convert("RGB"), (x, y))
        draw.rectangle((x + 6, y + 6, x + 190, y + 28), fill="white")
        draw.text((x + 10, y + 9), name, fill="black")
    path.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(path, quality=92)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--frame", action="append", default=[], help="NAME=SOURCE_PATH")
    parser.add_argument("--output-dir", required=True, type=Path)
    parser.add_argument("--contact-sheet", type=Path)
    parser.add_argument("--model", default="u2net")
    args = parser.parse_args()

    sources = parse_frames(args.frame)
    if not sources:
        parser.error("at least one --frame is required")
    args.output_dir.mkdir(parents=True, exist_ok=True)
    session = new_session(args.model)
    outputs: dict[str, Image.Image] = {}
    report = []
    for name, source in sources.items():
        image = extract_and_normalize(source, session)
        destination = args.output_dir / f"{name}.png"
        image.save(destination, optimize=True)
        outputs[name] = image
        item = metrics(name, image)
        if item["mode"] != "RGBA" or item["size"] != list(CANVAS):
            raise RuntimeError(f"{name}: invalid format")
        if item["opaque_edge_pixels"] != 0:
            raise RuntimeError(f"{name}: foreground touches canvas edge")
        report.append(item)
    if args.contact_sheet:
        make_contact_sheet(outputs, args.contact_sheet)
    print(json.dumps(report, ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
