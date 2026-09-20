"""観賞ビルド用コースター（docs/WATCH.md M2(b)）: 螺旋樋 1 本。DinDon（神戸ハーバーランド umie のボールマシン）を思わせる
「リフトで上げて樋を転がり落ちる」だけの最小構成。gen_kuruun.py と同じ流儀（三角形スープ・Solidify・FBX 規約）。
    REPO=...; exec(open(REPO + "/BlenderSources/gen_coaster.py").read())
出力: Assets/Models/Coaster_Helix.fbx（Blender 原点 = 螺旋の軸・z = 高さ。Unity 角 = Blender θ + 180°）
"""
import bmesh, bpy, json, math, os

REPO = globals().get("REPO") or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "Models")
os.makedirs(OUT, exist_ok=True)

# ---- 寸法（WatchCoaster.cs の定数と一致させる）----
R = 0.55          # 樋の中心線の半径
Z_TOP = 1.50      # 入口（θ=0）の樋の底の高さ
TURNS = 4.0       # 周回数
PITCH = 0.36      # 1 周あたりの落差
FLAT_DEG = 30.0   # 出口手前の水平区間
TROUGH_R = 0.07   # 樋の断面半径（球 r=0.05）
WALL_DEG = (180.0, 360.0)   # 断面の弧（270° が底・半円。壁の天端は底から 0.07）
STEP_DEG = 4.0
THICK = 0.012


def P(deg, r, z):
    a = math.radians(deg)
    return (r * math.sin(a), r * math.cos(a), z)


def floor_z(deg):
    total = 360.0 * TURNS
    if deg <= total:
        return Z_TOP - PITCH * deg / 360.0
    return Z_TOP - PITCH * TURNS   # 水平区間


class Builder:
    def __init__(self):
        self.bm = bmesh.new()

    def quad(self, a, b, c, d, want):
        vs = [self.bm.verts.new(p) for p in (a, b, c, d)]
        f = self.bm.faces.new(vs)
        f.normal_update()
        n = f.normal
        if n.x * want[0] + n.y * want[1] + n.z * want[2] < 0:
            bmesh.ops.reverse_faces(self.bm, faces=[f])
        return f

    def finish(self, name, thickness):
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        bmesh.ops.dissolve_degenerate(self.bm, dist=1e-6, edges=self.bm.edges)
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me); self.bm.free()
        for p in me.polygons:
            p.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        mod = ob.modifiers.new("Solidify", "SOLIDIFY")
        mod.thickness = thickness; mod.offset = -1.0; mod.use_even_offset = True; mod.use_quality_normals = True
        bpy.context.view_layer.objects.active = ob; ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        return ob


def section(deg):
    """θ における断面の点列（外→内の順）と、断面中心。"""
    zc = floor_z(deg) + TROUGH_R
    pts, n = [], 8
    for i in range(n + 1):
        phi = math.radians(WALL_DEG[0] + (WALL_DEG[1] - WALL_DEG[0]) * i / n)
        pts.append(P(deg, R + TROUGH_R * math.cos(phi), zc + TROUGH_R * math.sin(phi)))
    return pts, P(deg, R, zc)


def build():
    b = Builder()
    end = 360.0 * TURNS + FLAT_DEG
    deg = 0.0
    prev, prev_c = section(deg)
    while deg < end - 1e-6:
        deg = min(deg + STEP_DEG, end)
        cur, c = section(deg)
        for i in range(len(cur) - 1):
            mid = tuple((prev[i][k] + cur[i + 1][k]) / 2 for k in range(3))
            want = tuple((prev_c[k] + c[k]) / 2 - mid[k] for k in range(3))   # 表 = 断面中心（球側）向き
            b.quad(prev[i], prev[i + 1], cur[i + 1], cur[i], want)
        prev, prev_c = cur, c
    return b.finish("Coaster_Helix", THICK)


def export(ob, fname):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True); bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fname), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False,
                             bake_space_transform=False, use_custom_props=False)


def main():
    for ob in [o for o in bpy.data.objects if o.name.startswith("Coaster_")]:
        bpy.data.objects.remove(ob, do_unlink=True)
    ob = build()
    export(ob, "Coaster_Helix.fbx")
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    rep = {"faces": len(me.polygons), "nonmanifold_edges": sum(1 for e in bm.edges if not e.is_manifold),
           "end_deg": 360.0 * TURNS + FLAT_DEG, "z_bottom": floor_z(360.0 * TURNS)}
    bm.free()
    return rep


REPORT = main()
print(json.dumps(REPORT))
