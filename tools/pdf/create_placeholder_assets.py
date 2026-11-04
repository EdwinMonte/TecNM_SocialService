"""Draw deliberately generic calibration marks; never use real signature artwork."""
from pathlib import Path
from PIL import Image, ImageDraw

root = Path(__file__).resolve().parents[2] / 'backend/Resources/Documents'
for kind, size in [('signatures', (480, 120)), ('stamps', (240, 240))]:
    target = root / kind / 'dummy' / 'placeholder.png'
    target.parent.mkdir(parents=True, exist_ok=True)
    image = Image.new('RGBA', size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    color = (25, 65, 120, 255)
    if kind == 'stamps': draw.ellipse((8, 8, size[0]-8, size[1]-8), outline=color, width=4)
    else: draw.rectangle((8, 8, size[0]-8, size[1]-8), outline=color, width=3)
    draw.text((size[0]//2, size[1]//2), 'PRUEBA\n'+('SELLO GENERICO' if kind == 'stamps' else 'FIRMA GENERICA'), fill=color, anchor='mm', align='center', font_size=24)
    image.save(target)
    print(target.name, kind)
