# -*- coding: utf-8 -*-
from pathlib import Path
import sys
import math
import cv2
import numpy as np
from PIL import Image

TARGET_SIZE = 512
FILL_RATIO = 0.96

# 白背景との差。小さいほど薄い色も拾います。
COLOR_DISTANCE_THRESHOLD = 24.0
ALPHA_THRESHOLD = 10

# 画像全体に対する極小ゴミ除去
MIN_COMPONENT_RATIO = 0.00008

# 「本体」とみなす面積。
# 最大コンポーネント面積の何%以上なら別個体候補とするか
MAJOR_RELATIVE_TO_LARGEST = 0.09

# 本体候補の最低面積（画像全体比）
MAJOR_IMAGE_AREA_RATIO = 0.0015

# 小パーツを本体へ吸収する最大距離。
# 各本体サイズ基準なので、隣のモンスターへ連鎖結合しません。
SATELLITE_DISTANCE_RATIO = 0.42

# 仕上げ時の余白
EXTRA_MARGIN_RATIO = 0.015

SUPPORTED = {".png", ".jpg", ".jpeg", ".webp", ".bmp", ".tif", ".tiff"}


def estimate_background(rgb):
    h, w, _ = rgb.shape
    p = max(5, int(min(h, w) * 0.03))
    areas = [
        rgb[:p, :p], rgb[:p, -p:],
        rgb[-p:, :p], rgb[-p:, -p:]
    ]
    px = np.concatenate([a.reshape(-1, 3) for a in areas], axis=0)
    return np.median(px.astype(np.float32), axis=0)


def create_mask(rgba):
    rgb = rgba[:, :, :3].astype(np.float32)
    alpha = rgba[:, :, 3]
    bg = estimate_background(rgb.astype(np.uint8))

    transparent_source = float(np.mean(alpha < 245)) > 0.01

    if transparent_source:
        mask = (alpha > ALPHA_THRESHOLD).astype(np.uint8) * 255
    else:
        delta = rgb - bg.reshape(1, 1, 3)
        dist = np.sqrt(np.sum(delta * delta, axis=2))
        mask = (dist >= COLOR_DISTANCE_THRESHOLD).astype(np.uint8) * 255

    h, w = mask.shape

    # 細いノイズ線で別個体同士が繋がるのを切る
    k = max(3, int(round(min(h, w) * 0.002)))
    if k % 2 == 0:
        k += 1
    k = min(k, 7)

    kernel = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (k, k))
    mask = cv2.morphologyEx(mask, cv2.MORPH_OPEN, kernel, iterations=1)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, kernel, iterations=1)

    return mask, transparent_source, bg


def get_components(mask):
    h, w = mask.shape
    count, labels, stats, centroids = cv2.connectedComponentsWithStats(mask, 8)

    min_area = max(12, int(h * w * MIN_COMPONENT_RATIO))
    comps = []

    for i in range(1, count):
        area = int(stats[i, cv2.CC_STAT_AREA])
        if area < min_area:
            continue

        x = int(stats[i, cv2.CC_STAT_LEFT])
        y = int(stats[i, cv2.CC_STAT_TOP])
        cw = int(stats[i, cv2.CC_STAT_WIDTH])
        ch = int(stats[i, cv2.CC_STAT_HEIGHT])

        if cw < 3 or ch < 3:
            continue

        comps.append({
            "id": i,
            "x1": x,
            "y1": y,
            "x2": x + cw,
            "y2": y + ch,
            "area": area,
            "cx": float(centroids[i][0]),
            "cy": float(centroids[i][1]),
        })

    return comps


def bbox_distance(a, b):
    dx = max(0, max(a["x1"] - b["x2"], b["x1"] - a["x2"]))
    dy = max(0, max(a["y1"] - b["y2"], b["y1"] - a["y2"]))
    return math.hypot(dx, dy)


def bbox_union(box, c):
    box[0] = min(box[0], c["x1"])
    box[1] = min(box[1], c["y1"])
    box[2] = max(box[2], c["x2"])
    box[3] = max(box[3], c["y2"])


def choose_major_components(comps, image_area):
    if not comps:
        return []

    largest = max(c["area"] for c in comps)
    threshold = max(
        largest * MAJOR_RELATIVE_TO_LARGEST,
        image_area * MAJOR_IMAGE_AREA_RATIO
    )

    majors = [c for c in comps if c["area"] >= threshold]

    # 1個しか取れない場合、面積順の上位候補を少し緩める
    if len(majors) <= 1 and len(comps) > 1:
        soft = largest * 0.035
        majors = [
            c for c in comps
            if c["area"] >= max(soft, image_area * 0.0007)
        ]

    return sorted(majors, key=lambda c: c["area"], reverse=True)


def assign_satellites(comps, majors):
    """
    大きな本体同士は絶対に結合しない。
    小さなコンポーネントだけを最寄りの本体に追加する。
    """
    major_ids = {m["id"] for m in majors}

    groups = []
    for m in majors:
        groups.append({
            "major": m,
            "box": [m["x1"], m["y1"], m["x2"], m["y2"]],
            "components": [m]
        })

    satellites = [c for c in comps if c["id"] not in major_ids]

    # 大きい小パーツから処理
    satellites.sort(key=lambda c: c["area"], reverse=True)

    for s in satellites:
        best_idx = None
        best_score = None

        for i, g in enumerate(groups):
            m = g["major"]
            mw = m["x2"] - m["x1"]
            mh = m["y2"] - m["y1"]
            size = max(mw, mh)

            # 現在のグループ矩形との距離
            proxy = {
                "x1": g["box"][0], "y1": g["box"][1],
                "x2": g["box"][2], "y2": g["box"][3]
            }
            d = bbox_distance(s, proxy)

            # 本体サイズに比例した吸収距離
            max_dist = max(8, size * SATELLITE_DISTANCE_RATIO)

            if d > max_dist:
                continue

            # 距離を本体サイズで正規化
            score = d / max(1.0, size)

            if best_score is None or score < best_score:
                best_score = score
                best_idx = i

        if best_idx is not None:
            groups[best_idx]["components"].append(s)
            bbox_union(groups[best_idx]["box"], s)

    return groups


def split_by_whitespace(mask):
    """
    connected component が1個に繋がってしまった場合の保険。
    前景密度がほぼ0の縦/横帯で再帰的に分割する。
    """
    ys, xs = np.where(mask > 0)
    if len(xs) == 0:
        return []

    root = (int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1)

    def recurse(box, depth=0):
        x1, y1, x2, y2 = box
        sub = mask[y1:y2, x1:x2]
        h, w = sub.shape

        if depth >= 5 or w < 40 or h < 40:
            return [box]

        col = np.count_nonzero(sub, axis=0) / max(1, h)
        row = np.count_nonzero(sub, axis=1) / max(1, w)

        # 幅のある空白帯を探す
        def zero_runs(arr, limit):
            good = arr <= limit
            runs = []
            start = None
            for i, v in enumerate(good):
                if v and start is None:
                    start = i
                elif not v and start is not None:
                    if i - start >= 3:
                        runs.append((start, i))
                    start = None
            if start is not None and len(arr) - start >= 3:
                runs.append((start, len(arr)))
            return runs

        vruns = zero_runs(col, 0.004)
        hruns = zero_runs(row, 0.004)

        # 端の余白は分割候補から除外
        vruns = [r for r in vruns if r[0] > w * 0.08 and r[1] < w * 0.92]
        hruns = [r for r in hruns if r[0] > h * 0.08 and r[1] < h * 0.92]

        # 中央に近く、幅が広いギャップを優先
        candidates = []
        for a, b in vruns:
            center = (a + b) / 2
            candidates.append(("v", a, b, (b-a) - abs(center-w/2)*0.01))
        for a, b in hruns:
            center = (a + b) / 2
            candidates.append(("h", a, b, (b-a) - abs(center-h/2)*0.01))

        if not candidates:
            return [box]

        kind, a, b, _ = max(candidates, key=lambda t: t[3])
        cut = (a + b) // 2

        if kind == "v":
            left = (x1, y1, x1 + cut, y2)
            right = (x1 + cut, y1, x2, y2)
            return recurse(left, depth+1) + recurse(right, depth+1)
        else:
            top = (x1, y1, x2, y1 + cut)
            bottom = (x1, y1 + cut, x2, y2)
            return recurse(top, depth+1) + recurse(bottom, depth+1)

    boxes = recurse(root)

    # 空箱や極小箱を除外して前景にフィット
    final = []
    total_fg = np.count_nonzero(mask)

    for x1, y1, x2, y2 in boxes:
        sub = mask[y1:y2, x1:x2]
        sy, sx = np.where(sub > 0)
        if len(sx) == 0:
            continue

        area = len(sx)
        if total_fg > 0 and area < total_fg * 0.015:
            continue

        final.append((
            x1 + int(sx.min()),
            y1 + int(sy.min()),
            x1 + int(sx.max()) + 1,
            y1 + int(sy.max()) + 1
        ))

    return final


def sort_boxes(boxes):
    if not boxes:
        return boxes

    heights = [b[3] - b[1] for b in boxes]
    row_tol = max(15, float(np.median(heights)) * 0.45)

    # y中心で大まかな行に分け、各行を左→右
    items = []
    for b in boxes:
        cy = (b[1] + b[3]) / 2.0
        items.append((round(cy / row_tol), b[0], b))
    items.sort(key=lambda t: (t[0], t[1]))
    return [t[2] for t in items]


def detect_objects(mask):
    h, w = mask.shape
    comps = get_components(mask)

    if not comps:
        return []

    majors = choose_major_components(comps, h * w)

    if len(majors) >= 2:
        groups = assign_satellites(comps, majors)
        boxes = [tuple(g["box"]) for g in groups]

        # 極端に巨大なグループは除外しないが、通常はここで6体なら6個になる
        return sort_boxes(boxes)

    # 1個に繋がった時だけ空白帯分割を試す
    boxes = split_by_whitespace(mask)
    if len(boxes) >= 2:
        return sort_boxes(boxes)

    # 最終フォールバック
    c = max(comps, key=lambda x: x["area"])
    return [(c["x1"], c["y1"], c["x2"], c["y2"])]


def expand_box(box, w, h):
    x1, y1, x2, y2 = box
    bw, bh = x2 - x1, y2 - y1
    margin = max(2, int(max(bw, bh) * EXTRA_MARGIN_RATIO))
    return (
        max(0, x1 - margin),
        max(0, y1 - margin),
        min(w, x2 + margin),
        min(h, y2 + margin)
    )


def render(img, box, transparent_source):
    crop = img.crop(box).convert("RGBA")
    cw, ch = crop.size

    usable = int(TARGET_SIZE * FILL_RATIO)
    scale = min(usable / max(1, cw), usable / max(1, ch))

    nw = max(1, int(round(cw * scale)))
    nh = max(1, int(round(ch * scale)))
    crop = crop.resize((nw, nh), Image.Resampling.LANCZOS)

    if transparent_source:
        canvas = Image.new("RGBA", (TARGET_SIZE, TARGET_SIZE), (0, 0, 0, 0))
    else:
        canvas = Image.new("RGBA", (TARGET_SIZE, TARGET_SIZE), (255, 255, 255, 255))

    px = (TARGET_SIZE - nw) // 2
    py = (TARGET_SIZE - nh) // 2
    canvas.alpha_composite(crop, (px, py))
    return canvas


def process(path, input_root, output_root):
    try:
        img = Image.open(path).convert("RGBA")
    except Exception as e:
        print(f"[SKIP] {path.name}: {e}")
        return 0

    rgba = np.array(img)
    mask, transparent_source, bg = create_mask(rgba)
    boxes = detect_objects(mask)

    if not boxes:
        print(f"[WARN] 被写体を検出できません: {path.name}")
        return 0

    rel = path.parent.relative_to(input_root)
    out_dir = output_root / rel / path.stem
    out_dir.mkdir(parents=True, exist_ok=True)

    # 前回結果を同名画像だけ消す
    for old in out_dir.glob(f"{path.stem}_*.png"):
        try:
            old.unlink()
        except:
            pass

    w, h = img.size

    print(f"  検出数: {len(boxes)}")

    for i, box in enumerate(boxes, 1):
        box = expand_box(box, w, h)
        result = render(img, box, transparent_source)
        out_path = out_dir / f"{path.stem}_{i:02d}.png"
        result.save(out_path, "PNG", optimize=True)
        print(f"  [OK] {out_path.name}")

    return len(boxes)


def main():
    root = Path(__file__).resolve().parent
    input_root = root / "InputSprites"
    output_root = root / "Output512"
    input_root.mkdir(exist_ok=True)
    output_root.mkdir(exist_ok=True)

    files = [
        p for p in input_root.rglob("*")
        if p.is_file() and p.suffix.lower() in SUPPORTED
    ]

    print("")
    print("============================================================")
    print(" 1枚の画像から複数モンスター/装備を個別512x512へ分割")
    print("============================================================")
    print("")

    if not files:
        print("InputSprites フォルダに画像を入れてください。")
        print("")
        return

    total = 0
    for n, path in enumerate(files, 1):
        print(f"[{n}/{len(files)}] {path.relative_to(input_root)}")
        count = process(path, input_root, output_root)
        print(f"  -> {count} 枚出力")
        print("")
        total += count

    print("============================================================")
    print(f" 完了: 合計 {total} 枚")
    print("============================================================")
    print("")


if __name__ == "__main__":
    main()
