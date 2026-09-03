"""クルーン 1 段（回転ボウル＋静止コレクタ）を kuruun_params.json から生成し、FBX を Assets/Models/ へ書き出す。

使い方（Blender GUI を開いた状態で Blender MCP / Python コンソールから）:
    exec(open(r"D:/.../NTsLotoEngine/BlenderSources/gen_kuruun.py").read())
    # 変数 REPO を先に定義しておけばそのパスを使う。未定義ならこのファイルの位置から辿る

出力:
    Assets/Models/Kuruun_Bowl.fbx              … 回転ボウル（穴は一様。回転させて位相を一様化する）
    Assets/Models/Kuruun_Collector_<variant>.fbx … 静止コレクタ（当たり扇形＝内側へ落とす床／ハズレ扇形＝外周の樋へ落とす床／樋は exit_deg へ下る）
    BlenderSources/Kuruun.blend                … 生成結果の保存（原本は params.json とこのスクリプト）

規約（RSC AGENTS.md 3章 罠19〜22・32・49 に倣う）:
    - 原点はワールド原点。ボウルは穴リングの上面が z=0、コレクタは top_z から下。Z-up で作り、FBX は -Z forward / Y up で出す。
    - 面は「球が触れる側」を表にして作り、Solidify（厚み thickness・法線の裏側へ）で閉じた殻にする。フラットシェード。
    - エクスポートは DESELECT → 対象のみ選択 → use_selection。
    - 角度 θ: x = r·sinθ, y = r·cosθ（θ=0 が +Y）。Unity 側での対応は LotoSceneBuilder のレイキャスト検証で確定する（罠19）。
"""
import bpy, bmesh, json, math, os

REPO = globals().get("REPO") or os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
PARAMS = json.load(open(os.path.join(REPO, "BlenderSources", "kuruun_params.json"), encoding="utf-8"))
OUT = os.path.join(REPO, "Assets", "Models")
os.makedirs(OUT, exist_ok=True)


def P(deg, r, z):
    a = math.radians(deg)
    return (r * math.sin(a), r * math.cos(a), z)


class Builder:
    """三角形スープ。quad(a,b,c,d) の巻きは want（表が向くべき方向）で自動的に揃える。"""

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

    def tri(self, a, b, c, want):
        vs = [self.bm.verts.new(p) for p in (a, b, c)]
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
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        mod = ob.modifiers.new("Solidify", "SOLIDIFY")
        mod.thickness = thickness
        mod.offset = -1.0          # 法線（球側）の反対へ肉を付ける
        mod.use_even_offset = True
        mod.use_quality_normals = True
        bpy.context.view_layer.objects.active = ob
        ob.select_set(True)
        bpy.ops.object.modifier_apply(modifier=mod.name)
        return ob


def inward(deg, up=0.7):
    """表の向き: 上＋軸向き（ボウル内面用）"""
    a = math.radians(deg)
    return (-math.sin(a) * 0.7, -math.cos(a) * 0.7, up)


def outward(deg, up=0.0):
    a = math.radians(deg)
    return (math.sin(a), math.cos(a), up)


# ---------------------------------------------------------------- ボウル
def build_bowl(bp):
    b = Builder()
    seg = bp["segments"]
    prof = [  # (r0, z0, r1, z1, is_ring)
        (bp["rim_r"], bp["rim_h"], bp["rim_r"], bp["cone_top_h"], False),         # 外周壁
        (bp["rim_r"], bp["cone_top_h"], bp["ring_r_out"], 0.0, False),            # コーン
        (bp["ring_r_out"], 0.0, bp["ring_r_in"], 0.0, True),                       # 穴リング（z=0）
        (bp["ring_r_in"], 0.0, 0.0001, bp["dome_h"], False),                       # 中央ドーム
    ]
    pitch = 360.0 / bp["holes"]
    for i in range(seg):
        d0, d1 = 360.0 * i / seg, 360.0 * (i + 1) / seg
        dm = (d0 + d1) * 0.5
        for (r0, z0, r1, z1, ring) in prof:
            if ring and (dm % pitch) < bp["hole_deg"]:
                continue   # 穴（各ピッチの先頭 hole_deg 度）
            b.quad(P(d0, r0, z0), P(d1, r0, z0), P(d1, r1, z1), P(d0, r1, z1), inward(dm))
    return b.finish("Kuruun_Bowl", bp["thickness"])


# ---------------------------------------------------------------- コレクタ
def win_at(deg, wins):
    for (c, w) in wins:
        d = (deg - c + 180.0) % 360.0 - 180.0
        if abs(d) <= w * 0.5:
            return True
    return False


def build_collector(cp, wins, name):
    """穴リングの真下の静止床。当たり扇形は内側へ下り（軸の穴 r<r_in へ）、ハズレ扇形は外側へ下り（樋へ）。
    扇形の境目に放射方向の壁。樋（r_out〜gutter_r_out）は exit_deg へ向かって両回りに下る。"""
    b = Builder()
    seg = cp["segments"]
    r_in, r_out, top, drop = cp["r_in"], cp["r_out"], cp["top_z"], cp["drop"]
    g_out, g_depth, g_fall, exit_deg = cp["gutter_r_out"], cp["gutter_depth"], cp["gutter_fall"], cp["exit_deg"]

    def floor_z(deg, r):
        t = (r - r_in) / (r_out - r_in)
        return top - drop * (1 - t) if win_at(deg, wins) else top - drop * t   # 当たり: 内側が低い／ハズレ: 外側が低い

    def gutter_z(deg):
        d = abs((deg - exit_deg + 180.0) % 360.0 - 180.0)   # 出口からの角距離 0..180
        return top - drop - g_depth - g_fall * (1 - d / 180.0)   # 出口で最も低い

    prev_win = win_at(360.0 * (seg - 0.5) / seg, wins)
    for i in range(seg):
        d0, d1 = 360.0 * i / seg, 360.0 * (i + 1) / seg
        dm = (d0 + d1) * 0.5
        w = win_at(dm, wins)
        # 扇形の床（表は上）
        b.quad(P(d0, r_in, floor_z(dm, r_in)), P(d1, r_in, floor_z(dm, r_in)),
               P(d1, r_out, floor_z(dm, r_out)), P(d0, r_out, floor_z(dm, r_out)), (0, 0, 1))
        # 当たり扇形の外縁には低い縁（外へこぼれない）／ハズレ扇形の内縁にも縁
        if w:
            b.quad(P(d0, r_out, floor_z(dm, r_out)), P(d1, r_out, floor_z(dm, r_out)),
                   P(d1, r_out, top + 0.02), P(d0, r_out, top + 0.02), inward(dm, 0))
        else:
            b.quad(P(d0, r_in, floor_z(dm, r_in)), P(d1, r_in, floor_z(dm, r_in)),
                   P(d1, r_in, top + 0.02), P(d0, r_in, top + 0.02), outward(dm))
        # 扇形の境目: 放射壁（両面が表になるよう 2 枚）
        if w != prev_win:
            lo = top - drop
            wall = [P(d0, r_in, lo), P(d0, r_out, lo), P(d0, r_out, top + cp["wall_h"]), P(d0, r_in, top + cp["wall_h"])]
            t0 = math.radians(d0)
            side = (math.cos(t0), -math.sin(t0), 0)   # 角度増加方向
            b.quad(*wall, side)
            b.quad(*wall, (-side[0], -side[1], 0))
        prev_win = w
        # 樋: 床（表は上・外側へ 0.05 下がる＝球は外壁沿いに走り、出口の開口からそのまま外へ出る。平らだと V の底で止まる）＋外壁（表は内向き）
        gz0, gz1 = gutter_z(d0), gutter_z(d1)
        tilt = cp.get("gutter_tilt", 0.05)
        b.quad(P(d0, r_out, gz0 + tilt), P(d1, r_out, gz1 + tilt), P(d1, g_out, gz1), P(d0, g_out, gz0), (0, 0, 1))
        if abs((dm - exit_deg + 180.0) % 360.0 - 180.0) > cp["exit_open_deg"] * 0.5:   # 出口は外壁を開ける
            b.quad(P(d0, g_out, gz0), P(d1, g_out, gz1), P(d1, g_out, top + 0.02), P(d0, g_out, top + 0.02), inward(dm, 0))
        # 樋の内壁（ハズレ床の外縁より下の段差。当たり扇形の縁の下も塞ぐ）
        b.quad(P(d0, r_out, gz0 + tilt), P(d1, r_out, gz1 + tilt), P(d1, r_out, top - drop), P(d0, r_out, top - drop), outward(dm))
    return b.finish(name, cp["thickness"])


# ---------------------------------------------------------------- 出力
def export(ob, fname):
    bpy.ops.object.select_all(action="DESELECT")
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, fname), use_selection=True, object_types={"MESH"},
                             apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False,
                             bake_space_transform=False, use_custom_props=False)


def health(ob):
    me = ob.data
    bm = bmesh.new(); bm.from_mesh(me)
    degenerate = sum(1 for f in bm.faces if f.calc_area() < 1e-9)
    nonmanifold = sum(1 for e in bm.edges if not e.is_manifold)
    bm.free()
    return {"faces": len(me.polygons), "degenerate": degenerate, "nonmanifold_edges": nonmanifold}


def main():
    for ob in [o for o in bpy.data.objects if o.name.startswith(("Kuruun_", "Sieve_"))]:
        bpy.data.objects.remove(ob, do_unlink=True)
    report = {}
    bowl = build_bowl(PARAMS["bowl"])
    export(bowl, "Kuruun_Bowl.fbx"); report["Kuruun_Bowl"] = health(bowl)
    for key, sp in PARAMS.get("sieves", {}).items():
        if key.startswith("_"):
            continue
        dish = build_bowl(sp); dish.name = key
        export(dish, key + ".fbx"); report[key] = health(dish)
        dish.location.y = -3.0 * (list(PARAMS["sieves"]).index(key))
    for key, v in PARAMS["variants"].items():
        if key.startswith("_"):
            continue
        name = f"Kuruun_Collector_{key}"
        ob = build_collector(PARAMS["collector"], v["win"], name)
        export(ob, name + ".fbx"); report[name] = health(ob)
        ob.location.x = 3.0 * (list(PARAMS["variants"]).index(key))   # 見やすく横に並べる（エクスポート後なので FBX には影響しない）
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "BlenderSources", "Kuruun.blend"))
    return report


REPORT = main()
print(json.dumps(REPORT, indent=1))
