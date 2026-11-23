from PIL import Image

source = "src/KEYCE_KODER.App/Assets/logo.png"
destination = "src/KEYCE_KODER.App/Assets/app.ico"

image = Image.open(source).convert("RGBA")
image.save(
    destination,
    format="ICO",
    sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)]
)

print("Icône créée :", destination)
