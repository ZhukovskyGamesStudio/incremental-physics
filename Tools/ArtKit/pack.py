# Packs what ArtKit rendered (see run.ps1) into a folder for an artist: every piece with its alpha (matte from the
# black and white renders), the same piece on the board, overview sheets, the best screenshots, the board, the font.
#   python pack.py <raw dir> [out dir]
import os, re, sys, shutil
import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.join(HERE, '..', '..')
RAW = sys.argv[1]
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(ROOT, 'Build', 'ArtPack')
BOARD = (27, 36, 32)

DIRS = {
    'logo': '01 Лого и иконка',
    'mel': '02 Мел - маскот',
    'apparatus': '03 Приборы',
    'icon': '04 Иконки',
    'samples': '05 Образцы',
    'chalkie': '06 Мелок-помощник',
    'shots': '07 Скриншоты',
    'board': '08 Доска, шрифт, цвета',
}
MOODS = {'idle': 'спокоен', 'happy': 'доволен', 'joy': 'радость', 'wow': 'удивлён', 'oops': 'ой', 'sleep': 'спит'}
ICONS = {
    'bulb': 'идея (лампочка)', 'fall': 'полка', 'dyna': 'динамометр', 'spring': 'пружина', 'pend': 'маятник',
    'arch': 'ванна Архимеда', 'density': 'мензурка', 'friction': 'стол трения', 'slide': 'работа трения', 'fly': 'маховик',
    'stand': 'схема', 'charge': 'заряд', 'lamp': 'лампа', 'cap': 'конденсатор', 'coil': 'катушка', 'heater': 'кипятильник',
    'calor': 'калориметр', 'engine': 'паровая машина', 'ice': 'плавление', 'piston': 'поршень', 'psi': 'формула всего',
    'pusher': 'толкатель', 'hook': 'автоподвес', 'cam': 'кулачок', 'stack': 'стопка грузов', 'scoop': 'черпак',
    'scales': 'весы', 'nugget': 'золотой образец', 'console': 'пульт', 'hand': 'рука', 'bank': 'копилка', 'list': 'опись',
    'bolt': 'молния', 'spark': 'искра', 'gear': 'шестерня', 'lens': 'лупа', 'star': 'звезда (перк)', 'box': 'ящик образцов',
    'eye': 'глаз', 'clock': 'часы', 'rev': 'революция', 'act': 'действия', 'table': 'стол', 'chalkie': 'мелок',
    'guest': 'случайный прибор',
}
COLORS = [('мел белый', (237, 237, 227)), ('жёлтый (энергия, золото)', (255, 214, 82)), ('голубой (идеи)', (115, 219, 255)),
          ('красный', (255, 115, 115)), ('зелёный', (140, 255, 153)), ('розовый', (255, 153, 217)), ('доска', BOARD)]


def matte(name, pad=40):
    k = np.asarray(Image.open(os.path.join(RAW, name + '_k.png')).convert('RGB')).astype(np.float32) / 255
    w = np.asarray(Image.open(os.path.join(RAW, name + '_w.png')).convert('RGB')).astype(np.float32) / 255
    a = np.clip(1 - (w - k).mean(axis=2), 0, 1)
    a[a < 0.01] = 0
    rgb = np.where(a[..., None] > 1e-3, k / np.maximum(a[..., None], 1e-3), 0)
    img = Image.fromarray((np.dstack([np.clip(rgb, 0, 1), a]) * 255 + 0.5).astype(np.uint8), 'RGBA')
    ys, xs = np.nonzero(a > 0.02)
    if len(xs) == 0: return None
    return img.crop((max(0, xs.min() - pad), max(0, ys.min() - pad), min(img.width, xs.max() + pad), min(img.height, ys.max() + pad)))


def board(w, h):
    tile = Image.open(os.path.join(RAW, 'board_tile.png')).convert('RGB')
    k = 3.75                                           # the game stretches its 512 px board over a 1920 px screen
    t = tile.resize((int(tile.width * k), int(tile.height * k)), Image.BICUBIC)
    out = Image.new('RGB', (w, h))
    for y in range(0, h, t.height):
        for x in range(0, w, t.width):
            out.paste(t, (x, y))
    return out.convert('RGBA')


def on_board(img):
    b = board(img.width, img.height)
    b.alpha_composite(img)
    return b.convert('RGB')


def save(img, folder, name, with_board=True):
    d = os.path.join(OUT, folder)
    os.makedirs(d, exist_ok=True)
    img.save(os.path.join(d, name + '.png'))
    if with_board:
        os.makedirs(os.path.join(d, 'на доске'), exist_ok=True)
        on_board(img).save(os.path.join(d, 'на доске', name + '.png'))


def sheet(items, folder, title, cell=420, cols=6):
    """An overview of a folder on the board, captions in chalk-grey."""
    rows = (len(items) + cols - 1) // cols
    W, H = cols * cell, rows * (cell + 50)
    out = board(W, H)
    dr = ImageDraw.Draw(out)
    try:
        from PIL import ImageFont
        font = ImageFont.truetype(os.path.join(ROOT, 'Assets', 'Resources', 'Fonts', 'Melok-Regular.ttf'), 34)
    except Exception:
        font = None
    for i, (cap, img) in enumerate(items):
        x, y = (i % cols) * cell, (i // cols) * (cell + 50)
        t = img.copy()
        t.thumbnail((cell - 40, cell - 40), Image.LANCZOS)
        out.alpha_composite(t, (x + (cell - t.width) // 2, y + (cell - t.height) // 2))
        dr.text((x + cell // 2, y + cell + 10), cap, fill=(200, 200, 190), font=font, anchor='mt')
    out.convert('RGB').save(os.path.join(OUT, folder, '_обзор - ' + title + '.png'))


def formula_titles():
    src = open(os.path.join(ROOT, 'Assets', 'Scripts', 'Core', 'Defs.cs'), encoding='utf-8').read()
    return dict(re.findall(r'new FormulaDef \{ id = "(\w+)", title = "([^"]+)"', src))


def pick_shots():
    """Screenshots of real play: every screen and every era, nearly identical frames dropped."""
    names = sorted(n[:-4] for n in os.listdir(RAW) if n.startswith('shot_') and n.endswith('.png'))
    kept, thumbs = [], []
    for n in names:
        img = Image.open(os.path.join(RAW, n + '.png')).convert('RGB')
        th = np.asarray(img.resize((96, 54), Image.BILINEAR)).astype(np.float32)
        if th.std() < 4: continue                                           # a blank frame (the curtain)
        if any(np.abs(th - t).mean() < 1.5 for t in thumbs[-3:]): continue
        kept.append(n); thumbs.append(th)
    groups = {}
    for n in kept: groups.setdefault(n.split('_', 2)[2], []).append(n)   # screen + era
    out = []
    for g, ns in groups.items():
        step = max(1, len(ns) // 8)
        out += ns[::step][:8]
    return sorted(out)


def main():
    if os.path.exists(OUT): shutil.rmtree(OUT)
    os.makedirs(OUT)
    have = lambda n: os.path.exists(os.path.join(RAW, n + '_k.png'))

    # the logo and the app icon
    logo = matte('logo')
    save(logo, DIRS['logo'], 'лого')
    shutil.copy(os.path.join(ROOT, 'Assets', 'Icon.png'), os.path.join(OUT, DIRS['logo'], 'иконка приложения.png'))

    items = []
    for m, ru in MOODS.items():
        if have('mel_' + m):
            img = matte('mel_' + m); save(img, DIRS['mel'], 'мел - ' + ru); items.append((ru, img))
    sheet(items, DIRS['mel'], 'Мел', cols=3, cell=600)

    titles, items = formula_titles(), []
    for fid, ru in titles.items():
        if have('apparatus_' + fid):
            img = matte('apparatus_' + fid); save(img, DIRS['apparatus'], ru.lower()); items.append((ru, img))
    sheet(items, DIRS['apparatus'], 'приборы', cols=5, cell=560)

    items = []
    for ic, ru in ICONS.items():
        if have('icon_' + ic):
            img = matte('icon_' + ic); save(img, DIRS['icon'], ic + ' - ' + ru); items.append((ru, img))
    sheet(items, DIRS['icon'], 'иконки', cols=9, cell=300)

    save(matte('samples'), DIRS['samples'], 'образцы - размер это объём, штриховка это плотность, справа золотой')
    for c in ('white', 'yellow'):
        save(matte('chalkie_' + c), DIRS['chalkie'], 'мелок - ' + ('белый' if c == 'white' else 'жёлтый'))

    d = os.path.join(OUT, DIRS['shots']); os.makedirs(d, exist_ok=True)
    screens = {'menu': 'меню', 'bench': 'эксперимент', 'map': 'лаборатория (карта)', 'theory': 'теории'}
    eras = {'era0': 'механика', 'era1': 'электричество', 'era2': 'тепло'}
    for i, n in enumerate(pick_shots()):
        p = n.split('_')
        cap = screens.get(p[2], p[2]) + (' - ' + eras.get(p[3], p[3]) if len(p) > 3 else '')
        shutil.copy(os.path.join(RAW, n + '.png'), os.path.join(d, f'{i + 1:02d} {cap}.png'))

    d = os.path.join(OUT, DIRS['board']); os.makedirs(d, exist_ok=True)
    shutil.copy(os.path.join(RAW, 'board_tile.png'), os.path.join(d, 'доска - бесшовная текстура 512.png'))
    board(3840, 2160).convert('RGB').save(os.path.join(d, 'доска - 3840x2160 как в игре.png'))
    shutil.copy(os.path.join(ROOT, 'Assets', 'Resources', 'Fonts', 'Melok-Regular.ttf'), os.path.join(d, 'Melok-Regular.ttf'))
    pal = Image.new('RGB', (len(COLORS) * 300, 360), BOARD); dr = ImageDraw.Draw(pal)
    for i, (n, c) in enumerate(COLORS):
        dr.rectangle((i * 300 + 30, 30, i * 300 + 270, 270), fill=c, outline=(237, 237, 227), width=3)
        dr.text((i * 300 + 150, 300), '#%02X%02X%02X' % c, fill=(237, 237, 227), anchor='mt')
    pal.save(os.path.join(d, 'палитра.png'))
    with open(os.path.join(d, 'палитра.txt'), 'w', encoding='utf-8') as f:
        for n, c in COLORS: f.write('#%02X%02X%02X  %s\n' % (*c, n))
    with open(os.path.join(OUT, 'README.txt'), 'w', encoding='utf-8') as f:
        f.write(README)
    print('packed into', OUT)


README = '''Physics Incremental - арт игры для референса

Игра - инкрементальная игра про физику, целиком нарисованная мелом на школьной доске. Игрок - учёный-
экспериментатор: собирает формулы из букв-величин, ставит опыты на приборах, копит энергию и идеи. Путь идёт
от механики через электричество к теплу и заканчивается «Формулой всего» (Ψ).

Всё в игре рисуется кодом, мелом, поэтому эти картинки отрисованы самой игрой в 4K.

Как устроена папка
  Файлы .png в папках - мел на прозрачном фоне (белый мел, смотреть на тёмном).
  Папки «на доске» - то же самое на фоне доски, как в игре.
  Файлы «_обзор» - все картинки папки на одном листе с подписями.

  01 Лого и иконка      - надпись «Physics Incremental» и иконка приложения
  02 Мел - маскот       - Мел, кусочек мела с лицом; не говорит, только реагирует. Все 6 настроений
  03 Приборы            - все приборы игры, как их рисует экран «Теории» (эскиз прибора с образцом)
  04 Иконки             - иконки карты: приборы, улучшения, валюты (идея-лампочка)
  05 Образцы            - образцы-кубики: размер = объём, штриховка = плотность (слева лёгкие, справа тяжёлые),
                          жёлтый - золотой образец
  06 Мелок-помощник     - маленький мелок (сейчас в игре выключен, но персонаж есть)
  07 Скриншоты          - живая игра: эксперимент (стол с приборами и коробкой образцов),
                          лаборатория (карта-дерево прокачки), теории, меню
  08 Доска, шрифт, цвета - бесшовная текстура доски, рукописный шрифт игры Melok, палитра мела

Правила стиля
  - Всё - мел на тёмно-зелёной доске; линии неровные, рукописные.
  - Эффекты - из мира доски: никаких абстрактных звёздочек и свечения. Новое учитель обводит мелом,
    золото блестит штрихами, как в рисунках.
  - Цвет мела несёт смысл: жёлтый - энергия (Дж) и золото, голубой - идеи, белый - всё остальное.
  - Формулы пишутся без точек умножения: E = mgh, F = mg, T = 2π√(l / g).
'''


if __name__ == '__main__':
    main()
