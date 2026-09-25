"""Core-folder inspired gumball cabinet. Run through Blender MCP with REPO set.

Only the dedicated Gumball scene is replaced; other open work is preserved.
Coordinates below are Unity (x, height, z); export uses gen_coaster.py's FBX convention.
The helix remains the existing Coaster_Helix.fbx. No textures or modifiers at runtime.
"""
import bpy, bmesh, math, os, json
from mathutils import Vector

REPO = globals().get('REPO') or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, 'Assets', 'Models')
SCENE = 'Gumball'
old = bpy.data.scenes.get(SCENE)
if old:
    for obj in list(old.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.scenes.remove(old)
scene = bpy.data.scenes.new(SCENE)
bpy.context.window.scene = scene

def P(p):
    return (-p[0], -p[2], p[1])

def mesh(name, verts, faces):
    me = bpy.data.meshes.new(name)
    me.from_pydata([P(v) for v in verts], [], faces)
    me.update()
    bm = bmesh.new(); bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new(name, me)
    scene.collection.objects.link(ob)
    return ob

def lathe(name, profile, n=32, center=(0, 0, 0)):
    vs, fs = [], []
    for radius, height in profile:
        for i in range(n):
            a = i * math.tau / n
            vs.append((center[0] + radius * math.cos(a), center[1] + height, center[2] + radius * math.sin(a)))
    for j in range(len(profile)-1):
        for i in range(n):
            a=j*n+i; b=j*n+(i+1)%n
            fs.append((a,b,b+n,a+n))
    return mesh(name, vs, fs)

def ellipsoid(name, center, scale, n=24, rings=12):
    bpy.ops.mesh.primitive_uv_sphere_add(segments=n, ring_count=rings, radius=1, location=P(center))
    ob=bpy.context.object; ob.name=name
    ob.scale=(scale[0],scale[2],scale[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for f in ob.data.polygons: f.use_smooth=True
    return ob

def box(name, center, size):
    bpy.ops.mesh.primitive_cube_add(size=1, location=P(center))
    ob=bpy.context.object; ob.name=name; ob.scale=(size[0],size[2],size[1])
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    return ob

def tube(name, points, radii, sides=8):
    vs,fs=[],[]
    for j,p in enumerate(points):
        tangent=Vector(points[min(j+1,len(points)-1)])-Vector(points[max(0,j-1)])
        tangent.normalize()
        a=tangent.cross(Vector((0,0,1))).normalized()
        b=tangent.cross(a).normalized()
        for i in range(sides):
            v=Vector(p)+radii[j]*(math.cos(i*math.tau/sides)*a+math.sin(i*math.tau/sides)*b)
            vs.append(tuple(v))
    for j in range(len(points)-1):
        for i in range(sides):
            a=j*sides+i; b=j*sides+(i+1)%sides
            fs.append((a,b,b+sides,a+sides))
    fs.extend([tuple(reversed(range(sides))),tuple((len(points)-1)*sides+i for i in range(sides))])
    return mesh(name,vs,fs)

def join(name, obs):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.object.join()
    ob=obs[0]; ob.name=name
    bpy.context.scene.cursor.location=(0,0,0)
    bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    return ob

def material(name, color):
    m=bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color=color
    return m

body=[]; trim=[]
# Wide pedestal, narrow spine and dispenser collar. The rail remains fully visible.
body.append(lathe('Base',[(0,-.18),(.78,-.18),(.82,-.12),(.82,-.045),(.72,-.005),(0,-.005)]))
body.append(lathe('Spine',[(0,0),(.075,0),(.075,1.75),(0,1.75)],16))
body.append(lathe('Collar',[(0,1.74),(.38,1.74),(.61,1.86),(.62,1.96),(.53,2.015),(0,2.015)]))
trim.append(lathe('FootBand',[(.821,-.11),(.826,-.11),(.826,-.075),(.821,-.075),(.821,-.11)]))
trim.append(lathe('Belt',[(.622,1.91),(.627,1.91),(.627,1.95),(.622,1.95),(.622,1.91)]))
# Rounded core-folder silhouette: pointed ears, forehead curl, one plush curled tail.
for side in [-1,1]:
    x=side*.40
    body.append(mesh('Ear',[(x-side*.17,2.65,-.03),(x+side*.16,2.65,-.03),(x+side*.18,3.13,.02),
                            (x-side*.17,2.65,.20),(x+side*.16,2.65,.20),(x+side*.18,3.13,.09)],
                           [(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)]))
    trim.append(mesh('EarInset',[(x-side*.08,2.72,-.038),(x+side*.11,2.72,-.038),(x+side*.15,3.035,.009)],[(0,1,2)]))
tail_points=[(.43,2.0,.27),(.65,2.05,.29),(.78,2.23,.24),(.76,2.43,.23),(.63,2.56,.26),(.57,2.67,.29)]
body.append(tube('Tail',tail_points,[.14,.17,.16,.125,.08,.008],10))
trim.append(tube('Forelock',[(-.15,2.79,-.27),(0,2.87,-.24),(.12,2.88,-.18),(.17,2.82,-.12)],[.06,.075,.05,.006]))
trim.append(ellipsoid('Lid',(0,2.845,0),(.23,.055,.23),24,8))
# Two small closed eyes on the outside of the globe, leaving the contents visible.
eyes=[]
for side in [-1,1]:
    x=side*.22
    eyes.append(tube('Eye',[(x-.08,2.48,-.56),(x,2.46,-.585),(x+.08,2.48,-.56)],[.012]*3,6))
shell=ellipsoid('Glass',(0,2.34,0),(.65,.53,.61),32,16)
# Open-ended clear chute; no whole-height transparent cylinder (Pi fill rate).
a=math.radians(188); cx=.55*math.sin(a); cz=.55*math.cos(a)
chute=lathe('Chute',[(.075,1.64),(.075,1.87),(.087,1.87),(.087,1.64),(.075,1.64)],16,(cx,0,cz))
gate=box('Gate',(cx,1.665,cz),(.18,.025,.18))
cabinet=[join('Body',body),join('Trim',trim),join('Face',eyes),shell,chute,gate]
colors=[(.035,.21,.28,1),(.92,.62,.24,1),(.04,.08,.12,1),(.65,.9,1,.12),(.65,.9,1,.18),(.92,.62,.24,1)]
for ob,color in zip(cabinet,colors): ob.data.materials.clear(); ob.data.materials.append(material('Gumball_'+ob.name,color))

def export(obs, filename):
    bpy.ops.object.select_all(action='DESELECT')
    for ob in obs: ob.select_set(True)
    bpy.context.view_layer.objects.active=obs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,filename),use_selection=True,object_types={'MESH'},
        apply_scale_options='FBX_SCALE_ALL',axis_forward='-Z',axis_up='Y',mesh_smooth_type='OFF',
        use_mesh_modifiers=True,add_leaf_bones=False,bake_space_transform=False,use_custom_props=False)

export(cabinet,'Gumball_Cabinet.fbx')
# Low-poly display ball retains the source's character UVs; never modify the submodule.
with bpy.data.libraries.load(os.path.join(REPO,'LotteryBallKit','BlenderSources','LotteryBall.blend'),link=False) as (src,dst):
    dst.meshes=['LotteryBall']
low=bpy.data.objects.new('Gumball_FillBall',dst.meshes[0].copy()); scene.collection.objects.link(low)
bm=bmesh.new(); bm.from_mesh(low.data)
bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index!=0],context='FACES')
bm.to_mesh(low.data); bm.free()
low.data.materials.clear()
bpy.ops.object.select_all(action='DESELECT'); low.select_set(True); bpy.context.view_layer.objects.active=low
dec=low.modifiers.new('DisplayOnly','DECIMATE'); dec.ratio=.34; dec.use_collapse_triangulate=True
bpy.ops.object.modifier_apply(modifier=dec.name)
export([low],'Gumball_FillBall.fbx')
low.hide_viewport=True; low.hide_render=True
report={}
for ob in cabinet+[low]:
    ob.data.calc_loop_triangles(); report[ob.name]=len(ob.data.loop_triangles)
assert sum(report[o.name] for o in cabinet)<6000, report
assert report[low.name]<300, report
os.makedirs(os.path.join(REPO,'Output'),exist_ok=True)
with open(os.path.join(REPO,'Output','gumball-mesh.json'),'w') as f: json.dump(report,f,indent=2)
# Write only this scene and dependencies; preserve the unrelated open project's contents and filename.
bpy.data.libraries.write(os.path.join(REPO,'BlenderSources','Gumball.blend'),{scene},compress=True)
print(json.dumps(report))
