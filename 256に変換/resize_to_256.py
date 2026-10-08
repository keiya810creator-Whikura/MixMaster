from PIL import Image
import os

INPUT_FOLDER = "Input"
OUTPUT_FOLDER = "Output"
TARGET_SIZE = 256

SUPPORTED_EXTENSIONS = (
    ".png",
    ".jpg",
    ".jpeg",
    ".webp",
    ".bmp"
)

os.makedirs(INPUT_FOLDER, exist_ok=True)
os.makedirs(OUTPUT_FOLDER, exist_ok=True)

files = os.listdir(INPUT_FOLDER)
converted = 0

for filename in files:
    extension = os.path.splitext(filename)[1].lower()

    if extension not in SUPPORTED_EXTENSIONS:
        continue

    input_path = os.path.join(INPUT_FOLDER, filename)

    try:
        image = Image.open(input_path).convert("RGBA")

        width, height = image.size

        scale = min(
            TARGET_SIZE / width,
            TARGET_SIZE / height
        )

        new_width = max(1, round(width * scale))
        new_height = max(1, round(height * scale))

        resized = image.resize(
            (new_width, new_height),
            Image.Resampling.LANCZOS
        )

        canvas = Image.new(
            "RGBA",
            (TARGET_SIZE, TARGET_SIZE),
            (0, 0, 0, 0)
        )

        x = (TARGET_SIZE - new_width) // 2
        y = (TARGET_SIZE - new_height) // 2

        canvas.paste(
            resized,
            (x, y),
            resized
        )

        output_name = os.path.splitext(filename)[0] + ".png"
        output_path = os.path.join(OUTPUT_FOLDER, output_name)

        canvas.save(output_path, "PNG")

        converted += 1
        print(f"Converted: {filename} -> {output_name}")

    except Exception as e:
        print(f"Error: {filename}")
        print(e)

print()
print("==============================")
print(f"Done! {converted} image(s) converted to 256x256.")
print(f"Output: {OUTPUT_FOLDER}")
print("==============================")

input("Press Enter to exit...")
