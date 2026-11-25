import bpy, bmesh, sys, math
S, out = sys.argv[-2], sys.argv[-1]
sys.path.insert(0, S)
import letras as L
bpy.ops.wm.read_factory_settings(use_empty=True)
obs = []
for n, pts in L.LETRAS.items():
    px, py = L.PIVOTE[n]
    # Unidades: 1 = radio del disco. En Blender se escribe girado 180 grados para que en Unity quede derecho (x, z).
    P = []
    for x, y in pts:
        q = (-(x - px) / L.RADIO, (y - py) / L.RADIO)
        if not P or math.dist(P[-1], q) > 1e-5:
            P.append(q)
    if math.dist(P[0], P[-1]) < 1e-5:
        P.pop()
    bm = bmesh.new()
    f = bm.faces.new([bm.verts.new((x, y, 0)) for x, y in P])
    bm.normal_update()
    if f.normal.z > 0:
        f.normal_flip()
    r = bmesh.ops.extrude_face_region(bm, geom=[f])
    for v in r['geom']:
        if isinstance(v, bmesh.types.BMVert):
            v.co.z = .032
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    me = bpy.data.meshes.new('Letra' + n)
    bm.to_mesh(me)
    bm.free()
    o = bpy.data.objects.new('Letra' + n, me)
    bpy.context.collection.objects.link(o)
    m = o.modifiers.new('b', 'BEVEL'); m.width = .006; m.segments = 2; m.limit_method = 'ANGLE'; m.angle_limit = math.radians(60)
    m = o.modifiers.new('t', 'TRIANGULATE')
    bpy.context.view_layer.objects.active = o
    bpy.ops.object.select_all(action='DESELECT'); o.select_set(True)
    bpy.ops.object.convert(target='MESH')
    o = bpy.context.object
    bm = bmesh.new(); bm.from_mesh(o.data)
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0) > math.radians(35))
    for p in bm.faces:
        p.smooth = True
    bm.to_mesh(o.data); bm.free()
    o.data.materials.append(bpy.data.materials.get('Letra') or bpy.data.materials.new('Letra'))
    obs.append(o)
    print('LETRA', n, len(o.data.polygons), [round(v, 3) for v in o.dimensions])
bpy.ops.object.select_all(action='SELECT')
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, mesh_smooth_type='EDGE', apply_scale_options='FBX_SCALE_ALL', bake_space_transform=True, object_types={'MESH'})
