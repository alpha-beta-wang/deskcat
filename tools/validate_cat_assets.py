"""Strict checks for Niangao's independent RGBA animation frames."""

from __future__ import annotations

import argparse
import json
from pathlib import Path

import cv2
import numpy as np
from PIL import Image


REQUIRED = [
    "niangao-rest", "niangao-blink-half", "niangao-blink",
    "niangao-side-look", "niangao-side-lie",
    "niangao-groom-01", "niangao-groom-02", "niangao-groom-03",
    "niangao-walk-01", "niangao-walk-02", "niangao-walk-03", "niangao-walk-04",
]


def inspect(path: Path) -> dict[str, object]:
    image = Image.open(path)
    if image.mode != "RGBA" or image.size != (768, 512):
        raise AssertionError(f"{path.name}: expected 768x512 RGBA, got {image.size} {image.mode}")
    rgba = np.asarray(image)
    alpha = rgba[:, :, 3]
    if np.any(alpha[0]) or np.any(alpha[-1]) or np.any(alpha[:, 0]) or np.any(alpha[:, -1]):
        raise AssertionError(f"{path.name}: foreground reaches a canvas edge")
    ys, xs = np.nonzero(alpha > 8)
    if len(xs) == 0:
        raise AssertionError(f"{path.name}: empty foreground")
    bbox = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
    width, height = bbox[2] - bbox[0], bbox[3] - bbox[1]
    if not 650 <= width <= 710 or not 300 <= height <= 440 or bbox[3] > 491:
        raise AssertionError(f"{path.name}: implausible normalized bounds {bbox}")

    mask = (alpha > 16).astype(np.uint8)
    count, labels, stats, _ = cv2.connectedComponentsWithStats(mask, 8)
    areas = stats[1:, cv2.CC_STAT_AREA] if count > 1 else np.array([], dtype=int)
    largest_ratio = float(areas.max() / areas.sum()) if len(areas) else 0.0
    if largest_ratio < 0.985:
        raise AssertionError(f"{path.name}: detached opaque debris detected ({largest_ratio:.4f})")
    return {
        "file": path.name,
        "bbox": bbox,
        "foreground_fraction": round(float(np.count_nonzero(alpha > 8) / alpha.size), 4),
        "alpha_levels": int(len(np.unique(alpha))),
        "largest_component_ratio": round(largest_ratio, 4),
    }


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("asset_dir", type=Path)
    args = parser.parse_args()
    report = {name: inspect(args.asset_dir / f"{name}.png") for name in REQUIRED}

    for group in (["niangao-rest", "niangao-blink-half", "niangao-blink"],
                  [f"niangao-walk-{index:02d}" for index in range(1, 5)],
                  [f"niangao-groom-{index:02d}" for index in range(1, 4)]):
        boxes = [report[name]["bbox"] for name in group]
        widths = [box[2] - box[0] for box in boxes]
        baselines = [box[3] for box in boxes]
        if max(widths) - min(widths) > 20 or max(baselines) - min(baselines) > 2:
            raise AssertionError(f"unstable frame group {group}: widths={widths}, baselines={baselines}")

    print(json.dumps(list(report.values()), ensure_ascii=False, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
