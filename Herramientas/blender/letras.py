"""Contornos exactos de U C E N en pixeles del logo (LogoUCEN.png) y comparacion con el original."""
import math

def bez(p0, p1, p2, p3, n=14):
    return [tuple((1 - t) ** 3 * a + 3 * (1 - t) ** 2 * t * b + 3 * (1 - t) * t * t * c + t ** 3 * d for a, b, c, d in zip(p0, p1, p2, p3))
            for t in (i / n for i in range(1, n + 1))]

def arco(cx, cy, r, a0, a1, n=44):
    return [(cx + r * math.cos(math.radians(a0 + (a1 - a0) * i / n)), cy + r * math.sin(math.radians(a0 + (a1 - a0) * i / n))) for i in range(n + 1)]

K = .5523
def U():
    xo, xi, xr, xir, top, yo, yi = 180, 285, 595, 490, 196, 469, 469
    mo, mi, bo, bi = 387, 388, 678.5, 582
    p = [(xo, top), (xo, yo)]
    p += bez((xo, yo), (xo, yo + 132), (mo - 130, bo), (mo, bo)) + bez((mo, bo), (mo + 130, bo), (xr, yo + 132), (xr, yo))
    p += [(xr, top), (xir, top), (xir, yi)]
    p += bez((xir, yi), (xir, yi + 72), (mi + 62, bi), (mi, bi)) + bez((mi, bi), (mi - 62, bi), (xi, yi + 72), (xi, yi))
    p += [(xi, top)]
    return p

def C():
    cx, cy, ro, ri = 390.5, 1039.5, 241.5, 149.5
    a0 = math.degrees(math.atan2(892 - cy, 584 - cx)); a1 = math.degrees(math.atan2(1186 - cy, 589 - cx))
    b0 = math.degrees(math.atan2(948 - cy, 509 - cx)); b1 = math.degrees(math.atan2(1128 - cy, 511 - cx))
    return arco(cx, cy, ro, a0, a1 - 360) + arco(cx, cy, ri, b1 - 360, b0)

def E():
    return [(750, 567), (1072.5, 567), (1072.5, 651), (843, 651), (843, 738), (1045, 738), (1045, 821.5), (843, 821.5), (843, 911.5), (1076, 911.5), (1076, 995), (750, 995)]

def N():
    return [(1085, 1005), (1181, 1005), (1401.5, 1294), (1401.5, 1005), (1503.5, 1005), (1503.5, 1476), (1415, 1476), (1187, 1177), (1187, 1476), (1085, 1476)]

LETRAS = {'U': U(), 'C': C(), 'E': E(), 'N': N()}
CENTRO, RADIO = (835.5, 834.), 727.5
PIVOTE = {'U': (387, 437), 'C': (390.5, 1039.5), 'E': (911, 781), 'N': (1294, 1240.5)}

if __name__ == '__main__':
    from PIL import Image, ImageDraw
    import numpy as np
    im = np.array(Image.open('Assets/Obstetricia/Imagenes/LogoUCEN.png').convert('RGB'))
    print(im.shape)
    S = 2
    a = Image.new('L', (im.shape[1] * S, im.shape[0] * S), 255)
    d = ImageDraw.Draw(a)
    cx, cy = CENTRO
    d.ellipse([(cx - RADIO) * S, (cy - RADIO) * S, (cx + RADIO) * S, (cy + RADIO) * S], fill=0)
    for p in LETRAS.values():
        d.polygon([(x * S, y * S) for x, y in p], fill=255)
    a = np.array(a.resize((im.shape[1], im.shape[0]), Image.BOX)) > 127
    b = im[..., 0] > 127
    print('pixeles distintos: %.3f %%' % (100 * np.mean(a != b)))
