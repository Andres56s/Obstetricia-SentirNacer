import bpy, bmesh, sys, math
from mathutils import Vector
out = sys.argv[-1]
PI = math.pi
SMOOTH_ANGLE = math.radians(40)

def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)

def fin(o, m, bev=0, seg=2):
    if bev:
        b = o.modifiers.new('b', 'BEVEL')
        b.width = bev
        b.segments = seg
        b.limit_method = 'ANGLE'
    o.data.materials.clear()
    o.data.materials.append(bpy.data.materials.get(m) or bpy.data.materials.new(m))
    for p in o.data.polygons:
        p.use_smooth = True
        p.material_index = 0
    return o

def box(m, p, s, bev=.008, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_cube_add(size=1, location=p)
    o = bpy.context.object
    o.scale = s
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.rotation_euler = rot
    return fin(o, m, bev)

def cyl(m, p, rad, h, bev=.003, rot=(0, 0, 0), n=24, r2=None):
    if r2 is None:
        bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=rad, depth=h, location=p, rotation=rot)
    else:
        bpy.ops.mesh.primitive_cone_add(vertices=n, radius1=rad, radius2=r2, depth=h, location=p, rotation=rot)
    return fin(bpy.context.object, m, bev)

def rod(m, a, b, rad, n=12):
    a, b = Vector(a), Vector(b)
    d = b - a
    q = d.to_track_quat('Z', 'Y')
    bpy.ops.mesh.primitive_cylinder_add(vertices=n, radius=rad, depth=d.length, location=(a + b) / 2, rotation=q.to_euler())
    return fin(bpy.context.object, m)

def torus(m, p, R, r, rot=(0, 0, 0), M=32, N=10):
    bpy.ops.mesh.primitive_torus_add(major_radius=R, minor_radius=r, location=p, rotation=rot, major_segments=M, minor_segments=N)
    return fin(bpy.context.object, m)

def sph(m, p, s, rot=(0, 0, 0), seg=24, ring=16):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=ring, radius=1, location=p)
    o = bpy.context.object
    o.scale = s if hasattr(s, '__len__') else (s, s, s)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    o.rotation_euler = rot
    return fin(o, m)

def tube(m, pts, rad, res=8):
    c = bpy.data.curves.new('c', 'CURVE')
    c.dimensions = '3D'
    c.bevel_depth = rad
    c.bevel_resolution = 3
    c.resolution_u = res
    c.use_fill_caps = True
    s = c.splines.new('NURBS')
    s.points.add(len(pts) - 1)
    for i, p in enumerate(pts):
        s.points[i].co = (*p, 1)
    s.use_endpoint_u = True
    s.order_u = min(4, len(pts))
    o = bpy.data.objects.new('t', c)
    bpy.context.collection.objects.link(o)
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action='DESELECT')
    o.select_set(True)
    bpy.ops.object.convert(target='MESH')
    return fin(bpy.context.object, m)

def wheel(x, y, rad=.05):
    cyl('Rueda', (x, y, rad), rad, .025, .004, (0, PI / 2, 0))
    cyl('MetalClaro', (x, y, rad), rad * .45, .03, .002, (0, PI / 2, 0))
    box('MetalClaro', (x, y, rad * 2 + .012), (.035, .035, .03), .006)

def exp(n):
    bpy.ops.object.select_all(action='SELECT')
    bpy.context.view_layer.objects.active = bpy.context.selected_objects[0]
    bpy.ops.object.convert(target='MESH')
    bpy.ops.object.join()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    o = bpy.context.object
    bm = bmesh.new()
    bm.from_mesh(o.data)
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > SMOOTH_ANGLE)
    bm.to_mesh(o.data)
    bm.free()
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    bpy.context.scene.cursor.location = (0, 0, 0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    bpy.ops.export_scene.fbx(filepath=f'{out}/{n}.fbx', use_selection=True, mesh_smooth_type='EDGE',
                             apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, object_types={'MESH'})
    print('OK', n, sum(len(p.vertices) - 2 for p in o.data.polygons), [round(x, 3) for x in o.dimensions])

# ---------------------------------------------------------------- Monitor fetal (cardiotocógrafo) en carro. Frente -Y
reset()
for x in (-.2, .2):
    for y in (-.2, .2):
        wheel(x, y)
box('PlasticoGris', (0, 0, .13), (.46, .46, .05), .02)
cyl('MetalClaro', (0, .06, .62), .025, .95)
box('PlasticoClaro', (0, 0, .5), (.4, .38, .025), .01)
box('PlasticoClaro', (0, 0, .86), (.44, .4, .025), .01)
box('PlasticoClaro', (0, .02, .96), (.38, .3, .17), .025)            # unidad registradora
box('PlasticoAzul', (0, -.133, .99), (.34, .01, .07), .004)
box('Papel', (0, -.15, .915), (.2, .12, .006), .001, (math.radians(-28), 0, 0))
for i in range(5):
    cyl('PlasticoGris', (-.13 + i * .065, -.14, .955), .011, .012, .002, (PI / 2, 0, 0), 16)
rod('MetalClaro', (0, .07, 1.04), (0, .07, 1.35), .02)
box('PlasticoOscuro', (0, .03, 1.46), (.78, .045, .56), .015)          # pantalla (la imagen se dibuja en Unity)
for s, m in ((-1, 'TransductorAzul'), (1, 'TransductorRosa')):
    cyl(m, (s * .12, -.06, .535), .04, .03, .01)
    tube('PlasticoGris', [(s * .12, -.06, .55), (s * .2, -.16, .62), (s * .24, -.12, .8), (s * .17, .0, .93)], .004)
tube('Correa', [(-.12, -.1, .52), (0, -.16, .53), (.12, -.1, .52), (0, -.03, .525), (-.12, -.1, .52)], .012)
exp('MonitorFetal')

# ---------------------------------------------------------------- Cuna radiante. Frente -Y
reset()
for x in (-.27, .27):
    for y in (-.3, .34):
        wheel(x, y, .055)
box('PlasticoGris', (0, .02, .15), (.6, .74, .05), .02)
box('PlasticoClaro', (0, .12, .5), (.34, .34, .66), .02)
for z in (.36, .52, .68):
    box('PlasticoAzulClaro', (0, -.055, z), (.3, .012, .13), .006)
    box('MetalClaro', (0, -.066, z + .03), (.1, .012, .012), .004)
box('PlasticoClaro', (0, 0, .87), (.66, .8, .07), .025)
box('Colchon', (0, -.02, .925), (.54, .66, .05), .022, )
for s in (-1, 1):
    box('Acrilico', (s * .315, -.02, .99), (.012, .72, .16), .005)
box('Acrilico', (0, -.385, .99), (.62, .012, .16), .005)
box('PlasticoClaro', (0, .42, 1.3), (.3, .07, .9), .025)              # columna
box('PlasticoOscuro', (0, .378, 1.28), (.22, .02, .16), .008)
box('PlasticoAzul', (0, .376, 1.12), (.22, .015, .08), .006)
box('PlasticoClaro', (0, .18, 1.79), (.36, .56, .08), .03)            # cabezal
box('Calefactor', (0, .06, 1.748), (.2, .3, .012), .004)
for s in (-1, 1):
    cyl('LuzCuna', (s * .12, -.04, 1.748), .025, .012, .003, n=16)
    cyl('MetalClaro', (s * .2, .4, 1.15), .03, .32, .012, n=20)        # balones O2
exp('CunaRadiante')

# ---------------------------------------------------------------- Balón kinésico (65 cm) centrado en el origen
reset()
bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=32, radius=.325)
o = bpy.context.object
for v in o.data.vertices:
    t = math.acos(max(-1, min(1, v.co.z / .325)))
    k = 1 + .0045 * math.sin(t * 26) * (1 if .5 < t < PI - .5 else 0)
    v.co *= k
fin(o, 'Balon')
cyl('PlasticoClaro', (0, 0, .325), .012, .008, .002, n=16)
exp('Balon')

# ---------------------------------------------------------------- Banquito de parto (asiento en U abierto al frente -Y)
reset()
bpy.ops.mesh.primitive_torus_add(major_radius=.17, minor_radius=.07, location=(0, .02, .37), major_segments=48, minor_segments=16)
o = bpy.context.object
bm = bmesh.new()
bm.from_mesh(o.data)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, -.07, 0), plane_no=(0, -1, 0), clear_outer=True)
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
for v in bm.verts:
    v.co.z *= .62
    v.co.y *= 1.1
bm.to_mesh(o.data)
bm.free()
fin(o, 'TapizVerde')
bpy.ops.mesh.primitive_torus_add(major_radius=.17, minor_radius=.03, location=(0, .02, .325), major_segments=48, minor_segments=8)
o = bpy.context.object
bm = bmesh.new()
bm.from_mesh(o.data)
bmesh.ops.bisect_plane(bm, geom=bm.verts[:] + bm.edges[:] + bm.faces[:], plane_co=(0, -.07, 0), plane_no=(0, -1, 0), clear_outer=True)
bmesh.ops.holes_fill(bm, edges=[e for e in bm.edges if e.is_boundary], sides=0)
for v in bm.verts:
    v.co.y *= 1.1
bm.to_mesh(o.data)
bm.free()
fin(o, 'MaderaClara')
for x, y in ((-.16, -.04), (.16, -.04), (-.13, .15), (.13, .15)):
    rod('MaderaClara', (x * 1.22, y * 1.2, .0), (x, y, .34), .022, 16)
    cyl('Rueda', (x * 1.22, y * 1.2, .008), .025, .016, .003, n=16)
exp('BanquitoParto')

# ---------------------------------------------------------------- Colchoneta
reset()
box('TapizAzul', (0, 0, .025), (1.2, .75, .05), .02)
for x in (-.2, .2):
    box('TapizAzulBorde', (x, 0, .0505), (.012, .72, .003), .001)
exp('Colchoneta')

# ---------------------------------------------------------------- Liana de tela. Origen en el techo (z = 0), cuelga hacia -Z
reset()
cyl('MetalClaro', (0, 0, -.012), .06, .024, .006)
torus('MetalClaro', (0, 0, -.05), .028, .008, (PI / 2, 0, 0))
bpy.ops.mesh.primitive_grid_add(x_subdivisions=14, y_subdivisions=64, size=1)
o = bpy.context.object
for v in o.data.vertices:
    u, t = v.co.x * 2, v.co.y + .5                    # u -1..1 ancho, t 0..1 largo (bucle)
    a = t * 2 * PI
    spread = math.sin(t * PI) ** .8
    x = math.sin(a) * .17 * spread * (1 - .35 * math.cos(a)) * .9
    z = -.07 - (1 - math.cos(a)) / 2 * 1.62
    w = .028 + .075 * (1 - math.cos(a)) / 2
    fold = math.sin(u * 9 + t * 3) * .012
    v.co = (x + fold * .6, u * w + math.cos(u * 7) * .004, z + fold)
m = o.modifiers.new('s', 'SOLIDIFY')
m.thickness = .006
fin(o, 'TelaLiana')
for z in (-.16, -.2):
    torus('TelaLiana', (0, 0, z), .03, .016)
exp('Liana')

# ---------------------------------------------------------------- Portasuero
reset()
for i in range(5):
    a = i * 2 * PI / 5
    rod('MetalClaro', (0, 0, .1), (math.cos(a) * .27, math.sin(a) * .27, .075), .014)
    cyl('Rueda', (math.cos(a) * .27, math.sin(a) * .27, .035), .035, .022, .004, (0, PI / 2, a + PI / 2), 20)
cyl('MetalClaro', (0, 0, .95), .016, 1.75)
cyl('MetalClaro', (0, 0, .1), .04, .07, .01)
for s in (-1, 1):
    tube('MetalClaro', [(0, 0, 1.82), (s * .08, 0, 1.86), (s * .13, 0, 1.84), (s * .15, 0, 1.88)], .006)
box('PlasticoClaro', (0, -.06, 1.22), (.15, .11, .22), .015)            # bomba de infusión
box('PlasticoAzul', (0, -.117, 1.26), (.11, .008, .07), .003)
for i in range(3):
    cyl('PlasticoGris', (-.035 + i * .035, -.117, 1.17), .009, .01, .002, (PI / 2, 0, 0), 12)
o = sph('Suero', (.14, 0, 1.68), (.06, .022, .11))
box('PapelEtiqueta', (.14, -.022, 1.68), (.06, .004, .08), .001)
tube('Via', [(.14, 0, 1.57), (.14, -.02, 1.45), (.06, -.07, 1.36), (.02, -.08, 1.32)], .003)
tube('Via', [(.0, -.08, 1.11), (-.04, -.12, .9), (-.1, -.16, .75), (-.02, -.1, .86), (.0, -.02, 1.0)], .003)
exp('Portasuero')

# ---------------------------------------------------------------- Bandeja con instrumental de parto
reset()
box('MetalClaro', (0, 0, .004), (.42, .28, .008), .003)
for s in (-1, 1):
    box('MetalClaro', (0, s * .14, .012), (.42, .008, .024), .003)
    box('MetalClaro', (s * .21, 0, .012), (.008, .28, .024), .003)
box('Campo', (0, 0, .01), (.39, .25, .006), .002)

def pinza(x, y, ang, largo=.16, curva=0, tijera=False):
    c, s = math.cos(ang), math.sin(ang)
    P = lambda u, v, z=.018: (x + u * c - v * s, y + u * s + v * c, z)
    for k in (-1, 1):
        tube('Acero', [P(-largo * .48, k * .022), P(-largo * .2, k * .01), P(0, 0), P(largo * .25, -k * .004 + curva * .3), P(largo * .5, -k * .001 + curva)], .0034)
        torus('Acero', P(-largo * .56, k * .024), .015, .0034, (0, 0, 0), 20, 8)
    cyl('Acero', P(0, 0), .006, .012, .002, n=14)
pinza(-.085, .07, 0)
pinza(-.085, .0, 0)
pinza(-.085, -.07, 0, .15, .012)
for i, yy in enumerate((.075, .045)):                                    # clamp umbilical
    box('PlasticoAmarillo', (.1, yy, .019), (.06, .008, .008), .002)
    box('PlasticoAmarillo', (.1, yy + .012, .019), (.06, .006, .008), .002, (0, 0, .12))
sph('Pera', (.11, -.04, .042), (.034, .034, .03))                        # pera de goma
cyl('Pera', (.065, -.04, .04), .012, .06, .002, (0, -PI / 2, 0), 16, .004)
for k in range(3):
    box('Gasa', (.11, -.1, .016 + k * .005), (.09, .05, .005), .002)
exp('BandejaParto')

# ---------------------------------------------------------------- Estetoscopio de Pinard (acostado, eje X)
reset()
prof = [(-.075, .026), (-.07, .028), (-.066, .018), (-.05, .010), (0, .008), (.04, .0095), (.06, .016), (.072, .028), (.078, .031), (.08, .029)]
bm = bmesh.new()
N = 28
rings = []
for px, pr in prof:
    rings.append([bm.verts.new((px, math.cos(i * 2 * PI / N) * pr, math.sin(i * 2 * PI / N) * pr)) for i in range(N)])
for a, b in zip(rings, rings[1:]):
    for i in range(N):
        bm.faces.new((a[i], a[(i + 1) % N], b[(i + 1) % N], b[i]))
bm.faces.new(rings[0][::-1])
bm.faces.new(rings[-1])
bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = bpy.data.meshes.new('p')
bm.to_mesh(me)
bm.free()
o = bpy.data.objects.new('p', me)
bpy.context.collection.objects.link(o)
fin(o, 'MaderaClara')
exp('Pinard')

# ---------------------------------------------------------------- Doppler fetal portátil (acostado)
reset()
box('PlasticoClaro', (0, 0, 0), (.075, .13, .03), .012)
box('PlasticoOscuro', (0, .025, .0145), (.055, .045, .004), .002)
for i in range(2):
    cyl('PlasticoAzul', (-.015 + i * .03, -.025, .0155), .009, .006, .002, n=16)
cyl('TransductorAzul', (.085, .0, -.0), .016, .1, .005, (PI / 2, 0, 0), 20)
cyl('TransductorAzul', (.085, -.06, 0), .022, .025, .005, (PI / 2, 0, 0), 20)
tube('PlasticoGris', [(.085, .05, 0), (.085, .1, .0), (.03, .11, 0), (0, .065, 0)], .004)
exp('DopplerFetal')

# ---------------------------------------------------------------- Balanza neonatal y toallas
reset()
box('PlasticoClaro', (0, 0, .035), (.5, .3, .07), .02)
bpy.ops.mesh.primitive_grid_add(x_subdivisions=18, y_subdivisions=10, size=1, location=(0, .01, 0))
o = bpy.context.object
for v in o.data.vertices:
    x, y = v.co.x, v.co.y
    v.co = (x * .54, y * .28, .078 + .05 * (abs(y) * 2) ** 3 + .02 * (abs(x) * 2) ** 6)
m = o.modifiers.new('s', 'SOLIDIFY')
m.thickness = .008
fin(o, 'PlasticoAzulClaro')
box('PlasticoOscuro', (0, -.152, .04), (.14, .008, .04), .003)
exp('BalanzaNeonatal')

reset()
for i, (m, w) in enumerate((('ToallaBlanca', .36), ('ToallaVerde', .35), ('ToallaBlanca', .355), ('ToallaRosa', .34))):
    box(m, ((i % 2) * .006 - .003, 0, .03 + i * .055), (w, .26, .052), .022)
exp('Toallas')

# ---------------------------------------------------------------- Reloj de muro (sin manecillas). Frente -Y
reset()
cyl('PlasticoOscuro', (0, 0, 0), .16, .03, .008, (PI / 2, 0, 0), 48)
cyl('PlasticoClaro', (0, -.014, 0), .146, .008, .001, (PI / 2, 0, 0), 48)
for i in range(60):
    a = i * PI / 30
    L = .022 if i % 5 == 0 else .009
    r = .138 - L / 2
    box('PlasticoOscuro', (math.sin(a) * r, -.0185, math.cos(a) * r), (.005 if i % 5 == 0 else .002, .002, L), 0, (0, a, 0))
exp('Reloj')
