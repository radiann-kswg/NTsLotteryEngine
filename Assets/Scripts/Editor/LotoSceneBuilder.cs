using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace NTsLotoEngine.EditorTools
{
    /// <summary>
    /// シーン生成の唯一の入口（冪等）。Tools > NTsLoto > Build Loto Scene。
    /// 配置寸法はすべてここ。単位 m、球径 0.1（LotteryBallKit）。docs/DESIGN.md 3章と対応。
    /// </summary>
    public static class LotoSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/LotoScene.unity";
        const string BallPrefabPath = "Packages/net.numbertales-radiann.lotteryballkit/Prefabs/NumberBall.prefab";
        const string MatDir = "Assets/Materials/Generated";

        // ---- 寸法 ----
        const float RailZ = -1.10f, RailY = 0.05f, RailLen = 2.0f;
        const float TowerZ = 3.0f, TowerPitch = 2.4f, TowerBaseY = 0.9f;   // 最下段ボウル底の高さ（完走トレイ＋通過トリガー分）
        const float FunnelH = 0.28f, LevelGap = 0.86f, Stagger = 0.195f;   // 千鳥オフセット（対角 (±S,±S)。喉→下段コーン面 r=0.55。X/Z 軸上はメッシュの継ぎ目で突き抜けるので対角に置く）                       // 段ピッチ = ボウル高 + LevelGap（シュートが下段の縁を越える分）

        [MenuItem("Tools/NTsLoto/Build Loto Scene")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Loto" || go.name == "Main Camera" || go.name == "Directional Light") Object.DestroyImmediate(go);

            var glass = Mat("Glass", new Color(0.80f, 0.90f, 1f, 0.22f), true);
            var frame = Mat("Frame", new Color(0.18f, 0.18f, 0.20f));
            var rail = Mat("Rail", new Color(0.85f, 0.82f, 0.75f));
            var bowlMat = Mat("Bowl", new Color(0.93f, 0.94f, 0.96f, 0.75f), true);

            var root = new GameObject("Loto").transform;
            Box(root, "Ground", new Vector3(0, -0.05f, 0), new Vector3(30, 0.1f, 30), frame);

            // ---- 二層式ロトマシーン ----
            var machine = new GameObject("LotoMachine").transform; machine.SetParent(root, false);
            // 下段 12〜99: 内径 0.70・高さ 0.60・床 y=0.30。排出口は正面(180°)、シュート 0.40 → 端が z≈-1.10（レール中心の真上・壁より高い）
            var lower = BuildTier(machine, "TierL", 0.30f, 0.70f, 0.60f, 180f, 0.40f, 0.55f, glass, frame, out var lowerEnd);
            Cylinder(machine, "PedestalL", new Vector3(0, 0.125f, 0), 0.35f, 0.25f, frame);
            Disc(machine, "LidL", 0.92f, 0.72f, 0.02f, frame);   // 上段シュート（r=0.72 で y≈0.94）に当てない
            // 上段 1〜11: 内径 0.45・高さ 0.50・床 y=1.00。排出口 215°、シュート 0.902 → 端が z≈-1.10, x≈-0.77
            var upper = BuildTier(machine, "TierU", 1.00f, 0.45f, 0.50f, 215f, 0.902f, 0.30f, glass, frame, out var upperEnd);
            Disc(machine, "LidU", 1.54f, 0.47f, 0.04f, frame);

            // 上段の落下管（漏斗口 → 結果レール）
            var dirU = new Vector3(Mathf.Sin(215f * Mathf.Deg2Rad), 0, Mathf.Cos(215f * Mathf.Deg2Rad));
            var mouth = upperEnd + dirU * 0.06f;
            var tube = Tube(machine, "DropTubeU", new Vector3(mouth.x, RailY + 0.20f, RailZ), 0.08f, 0.18f, 0.02f, mouth.y - 0.03f - (RailY + 0.20f), glass);

            // 結果レール（+X 側が 3° 低い）
            var railT = new GameObject("ResultRail").transform; railT.SetParent(root, false);
            railT.SetPositionAndRotation(new Vector3(0, RailY, RailZ), Quaternion.Euler(0, 0, -3f));
            Box(railT, "Floor", new Vector3(0, 0, 0), new Vector3(RailLen, 0.02f, 0.24f), rail);
            Box(railT, "WallF", new Vector3(0, 0.06f, -0.13f), new Vector3(RailLen, 0.12f, 0.02f), glass);
            Box(railT, "WallB", new Vector3(0, 0.06f, 0.13f), new Vector3(RailLen, 0.12f, 0.02f), glass);
            Box(railT, "StopR", new Vector3(RailLen / 2, 0.06f, 0), new Vector3(0.02f, 0.12f, 0.28f), frame);
            Box(railT, "StopL", new Vector3(-RailLen / 2, 0.06f, 0), new Vector3(0.02f, 0.12f, 0.28f), frame);

            // ---- 別ボール 7 塔（縦連クルーン）----
            var towers = new KuruunTower[LotoRules.Streaks.Length];
            for (int i = 0; i < towers.Length; i++)
                towers[i] = BuildTower(root, i, (i - (towers.Length - 1) / 2f) * TowerPitch, bowlMat, frame, rail, glass);

            // ---- カメラ・照明・進行役 ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.05f; cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f); cam.clearFlags = CameraClearFlags.SolidColor;
            var camMachine = Anchor(root, "Cam_Machine", new Vector3(0, 1.3f, -3.6f), new Vector3(0, 0.9f, 0));
            var camOverview = Anchor(root, "Cam_Overview", new Vector3(0, 4.5f, -7.5f), new Vector3(0, 0.8f, 1.0f));
            camGo.transform.SetPositionAndRotation(camMachine.position, camMachine.rotation);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var director = new GameObject("Director").AddComponent<LotoDirector>();
            director.transform.SetParent(root, false);
            director.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath)?.GetComponent<NumberBall>();
            if (!director.ballPrefab) Debug.LogError($"NumberBall prefab が見つからない: {BallPrefabPath}（Packages/manifest.json の LotteryBallKit を確認）");
            director.upper = upper; director.lower = lower; director.towers = towers;
            director.cam = cam; director.camMachine = camMachine; director.camOverview = camOverview;

            // 壁・シュート・漏斗・フラップは摩擦ゼロ（引き寄せ中の球が壁に張り付かない。床とフィンは既定の摩擦で球を運ぶ）
            var slick = Slick();
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                string nm = col.name;
                if (nm == "Wall" || nm == "Sill" || nm == "SideL" || nm == "SideR" || nm == "DropTubeU" || nm.StartsWith("Plate") || nm.StartsWith("Channel"))   // 漏斗は摩擦あり（周回減衰）
                    col.sharedMaterial = slick;
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            Debug.Log($"[LotoSceneBuilder] built: tiers=2 towers={towers.Length} lowerEnd={lowerEnd} upperEnd={upperEnd} tube={tube.name}");
        }

        // 角度規約: ProcMesh と同じ（0° = +Z、Y 軸まわり右ねじ）。正面（カメラ側）は 180°。
        static LotoDrumTier BuildTier(Transform parent, string name, float floorY, float rIn, float height, float portDeg, float chuteLen, float spawnRadius,
                                      Material glass, Material frame, out Vector3 chuteEnd)
        {
            var tier = new GameObject(name).transform; tier.SetParent(parent, false); tier.localPosition = new Vector3(0, floorY, 0);
            var t = tier.gameObject.AddComponent<LotoDrumTier>();
            t.spawnRadius = spawnRadius;

            float gapDeg = 2f * Mathf.Asin(0.10f / rIn) * Mathf.Rad2Deg;   // 口幅 0.20 = 2d（球の輪の中から引き出すので広め）
            Tube(tier, "Wall", Vector3.zero, rIn, rIn, 0.02f, height, glass, 72, portDeg, gapDeg, 0.16f);

            // 回転床（中央が 0.04 盛り上がった円錐・放射フィン 6 枚）
            var floor = new GameObject("Floor"); floor.transform.SetParent(tier, false);
            var disc = floor.AddComponent<DiscPlate>(); disc.radius = rIn - 0.01f; disc.thickness = 0.05f; disc.centerRise = 0.04f; disc.convex = true; disc.Rebuild();
            floor.GetComponent<MeshRenderer>().sharedMaterial = frame;
            var rb = floor.AddComponent<Rigidbody>(); rb.isKinematic = true;
            var rot = floor.AddComponent<Rotator>(); t.floor = rot;
            float finLen = rIn - 0.25f - 0.16f;   // 先端と壁の隙間 0.16 = 1.6d（球径未満だと噛んで弾け飛ぶ。RSC 罠48）
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f;
                var fin = Box(floor.transform, $"Fin{i}", Quaternion.Euler(0, a, 0) * new Vector3(0, 0.06f, 0.25f + finLen / 2), new Vector3(0.02f, 0.08f, finLen), frame);
                fin.transform.localRotation = Quaternion.Euler(0, a, 0);
            }

            // 排出口シュート（8° 下り）。ブロッカーは壁の切り欠きを塞ぎ、当選球（ChosenBall 層）だけ通す
            var dir = new Vector3(Mathf.Sin(portDeg * Mathf.Deg2Rad), 0, Mathf.Cos(portDeg * Mathf.Deg2Rad));
            var chute = new GameObject("Chute").transform; chute.SetParent(tier, false);
            chute.localPosition = dir * rIn; chute.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(8f, 0, 0);
            Box(chute, "Sill", new Vector3(0, -0.01f, chuteLen / 2), new Vector3(0.26f, 0.02f, chuteLen + 0.02f), frame);
            Box(chute, "SideL", new Vector3(-0.14f, 0.06f, chuteLen / 2), new Vector3(0.02f, 0.14f, chuteLen + 0.02f), glass);
            Box(chute, "SideR", new Vector3(0.14f, 0.06f, chuteLen / 2), new Vector3(0.02f, 0.14f, chuteLen + 0.02f), glass);
            var blocker = Box(chute, "Blocker", new Vector3(0, 0.08f, 0.015f), new Vector3(0.28f, 0.16f, 0.03f), null);
            blocker.layer = LotoLayers.Blocker; Object.DestroyImmediate(blocker.GetComponent<MeshRenderer>()); Object.DestroyImmediate(blocker.GetComponent<MeshFilter>());
            var trig = Box(chute, "PortTrigger", new Vector3(0, 0.08f, chuteLen * 0.6f), new Vector3(0.2f, 0.16f, 0.06f), null);
            Object.DestroyImmediate(trig.GetComponent<MeshRenderer>()); Object.DestroyImmediate(trig.GetComponent<MeshFilter>());
            trig.GetComponent<BoxCollider>().isTrigger = true; t.port = trig.AddComponent<BallTrigger>();
            chuteEnd = chute.TransformPoint(0, 0, chuteLen);

            var spawn = new GameObject("SpawnCenter").transform; spawn.SetParent(tier, false); spawn.localPosition = new Vector3(0, 0.15f, 0); t.spawnCenter = spawn;
            return t;
        }

        static KuruunTower BuildTower(Transform parent, int index, float x, Material bowlMat, Material frame, Material rail, Material glass)
        {
            var s = LotoRules.Streaks[index];
            int n = s.max;
            var root = new GameObject($"Tower{index}_Ball{s.ball:00}_x{n}").transform; root.SetParent(parent, false);
            root.localPosition = new Vector3(x, 0, TowerZ);
            var k = root.gameObject.AddComponent<KuruunTower>();
            var c = root.position;

            // ボウルを 1 個置いて寸法を測る（KuruunBowl の既定寸法 = RSC TowerD_Kuruun）
            var first = PlaceBowl(root, bowlMat, 0f, 0f, 0f, out var bb);
            float H = bb.size.y, R = Mathf.Max(bb.extents.x, bb.extents.z), pitch = H + LevelGap;
            float zEnd = -(0.18f + (R + 0.25f));        // 落下チャンネル入口（全段共通・塔の中心線 x=0）
            float zCh = zEnd - 0.13f;                   // 落下チャンネル中心

            k.levels = new KuruunTower.Level[n];
            for (int i = 0; i < n; i++)
            {
                float y = TowerBaseY + (n - 1 - i) * pitch;      // この段のボウル底
                float xi = (i % 2 == 0 ? -1f : 1f) * Stagger, zi = xi;    // 千鳥（対角）: 上段の喉が下段コーン面（r=2√2·Stagger）に落ちる
                var L = new KuruunTower.Level();
                if (i == 0) { first.transform.localPosition = new Vector3(xi, y, zi); L.bowl = first.transform; }
                else L.bowl = PlaceBowl(root, bowlMat, xi, zi, y, out _).transform;

                Tube(root, $"Funnel{i}", new Vector3(xi, y - 0.02f - FunnelH, zi), 0.12f, 0.40f, 0.02f, FunnelH, glass, 40);   // 45° 漏斗・喉 2.4d（狭い喉だと球が縁を周回して落ちない）
                L.throat = Anchor(root, $"Throat{i}", c + new Vector3(xi, y - 0.28f, zi), null);

                // 排出シュートは各段の軸から塔中心線上のチャンネル入口へ向ける（6° 下り）
                var origin = new Vector3(xi, 0, zi); var mouth = new Vector3(0, 0, zEnd);
                var dir = (mouth - origin).normalized;                 // 水平
                float Lc = Vector3.Distance(origin, mouth) - 0.18f;
                var yaw = Quaternion.LookRotation(-dir);              // フラップの局所 -Z = シュート方向

                float yf = y - 0.38f;                              // フラップ軸（喉の 0.08 下）
                var pivot = new GameObject($"FlapPivot{i}").transform; pivot.SetParent(root, false);
                pivot.localPosition = origin + new Vector3(0, yf, 0) - dir * 0.15f; pivot.localRotation = yaw;
                var flap = new GameObject("Flap").transform; flap.SetParent(pivot, false);
                Box(flap, "Plate", new Vector3(0, 0, -0.16f), new Vector3(0.28f, 0.02f, 0.32f), frame);   // 倒すと落下柱（±0.12）全体を覆う
                Box(flap, "PlateWall", new Vector3(-0.15f, 0.04f, -0.16f), new Vector3(0.02f, 0.10f, 0.32f), glass);   // 喉から横向きに出てきた球を板から落とさない
                Box(flap, "PlateWall", new Vector3(0.15f, 0.04f, -0.16f), new Vector3(0.02f, 0.10f, 0.32f), glass);
                flap.localRotation = KuruunTower.FlapDivert;
                L.flap = flap;

                var pass = Box(root, $"Pass{i}", new Vector3(xi, yf - 0.34f, zi), new Vector3(0.30f, 0.06f, 0.30f), null);
                L.passTrigger = MakeTrigger(pass);

                var chute = new GameObject($"Chute{i}").transform; chute.SetParent(root, false);
                chute.localPosition = origin + new Vector3(0, yf - 0.26f, 0) + dir * 0.18f;   // 落下柱（喉 r0.12 + 球 r0.05）の外・フラップ先端の下
                chute.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(6f, 0, 0);
                Box(chute, "Sill", new Vector3(0, -0.01f, Lc / 2), new Vector3(0.24f, 0.02f, Lc), frame);
                Box(chute, "SideL", new Vector3(-0.13f, 0.05f, Lc / 2), new Vector3(0.02f, 0.12f, Lc), glass);
                Box(chute, "SideR", new Vector3(0.13f, 0.05f, Lc / 2), new Vector3(0.02f, 0.12f, Lc), glass);
                var exit = Box(chute, "ExitTrigger", new Vector3(0, 0.06f, Lc * 0.5f), new Vector3(0.2f, 0.12f, 0.06f), null);
                L.exitTrigger = MakeTrigger(exit);
                k.levels[i] = L;
            }

            // 落下チャンネル（+Z 側が開いた U 字。上段のシュート端から下のハズレトレイまで）
            float top = TowerBaseY + (n - 1) * pitch;
            float chH = top - 0.45f;
            Box(root, "ChannelF", new Vector3(0, 0.1f + chH / 2, zEnd - 0.25f), new Vector3(0.28f, chH, 0.02f), glass);
            Box(root, "ChannelL", new Vector3(-0.13f, 0.1f + chH / 2, zCh), new Vector3(0.02f, chH, 0.24f), glass);
            Box(root, "ChannelR", new Vector3(0.13f, 0.1f + chH / 2, zCh), new Vector3(0.02f, chH, 0.24f), glass);
            Tray(root, "LoseTray", new Vector3(0, 0.05f, zCh), rail, glass);
            Tray(root, "WinTray", new Vector3(((n - 1) % 2 == 0 ? -1f : 1f) * Stagger, 0.05f, ((n - 1) % 2 == 0 ? -1f : 1f) * Stagger), rail, glass);   // 最下段の喉の真下
            foreach (float sx in new[] { -(R + Stagger + 0.12f), R + Stagger + 0.12f }) Rod(root, c + new Vector3(sx, 0, 0), c + new Vector3(sx, top + H, 0), 0.025f, frame);

            // 投入は最上段ボウルのコーン面（r=0.55・面の 0.13 上）へ接線速度付きで。中央ドーム頂点に落とすと弾かれて縁を越える
            k.dropPoint = Anchor(root, "DropPoint", c + new Vector3(-Stagger - 0.39f, top + 0.40f, -Stagger + 0.39f), null);   // 最上段ボウル軸から角度 135°・r=0.55（継ぎ目を避ける）
            k.camAnchor = Anchor(root, "Cam", c + new Vector3(0, 0, -(R + 2.4f)), null);
            return k;
        }

        static GameObject PlaceBowl(Transform root, Material mat, float xOff, float zOff, float bottomY, out Bounds bb)
        {
            var bowl = new GameObject("Bowl"); bowl.transform.SetParent(root, false);
            bowl.transform.localPosition = new Vector3(xOff, bottomY, zOff);
            bowl.AddComponent<KuruunBowl>().Rebuild();
            bowl.GetComponent<MeshRenderer>().sharedMaterial = mat;
            bb = bowl.GetComponent<MeshRenderer>().bounds;
            return bowl;
        }

        static BallTrigger MakeTrigger(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>()); Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            go.GetComponent<BoxCollider>().isTrigger = true;
            return go.AddComponent<BallTrigger>();
        }

        static void Tray(Transform parent, string name, Vector3 localPos, Material rail, Material glass)
        {
            var tray = new GameObject(name).transform; tray.SetParent(parent, false); tray.localPosition = localPos;
            Box(tray, "Floor", Vector3.zero, new Vector3(0.30f, 0.02f, 0.30f), rail);
            Box(tray, "W0", new Vector3(0, 0.04f, 0.15f), new Vector3(0.32f, 0.08f, 0.02f), glass);
            Box(tray, "W1", new Vector3(0, 0.04f, -0.15f), new Vector3(0.32f, 0.08f, 0.02f), glass);
            Box(tray, "W2", new Vector3(0.15f, 0.04f, 0), new Vector3(0.02f, 0.08f, 0.32f), glass);
            Box(tray, "W3", new Vector3(-0.15f, 0.04f, 0), new Vector3(0.02f, 0.08f, 0.32f), glass);
        }

        // ---- helpers ----
        static GameObject Box(Transform parent, string name, Vector3 localPos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos; go.transform.localScale = size;
            if (mat) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // 見た目だけの円柱（Cylinder のカプセルコライダ罠を避けるためコライダは外す）
        static GameObject Cylinder(Transform parent, string name, Vector3 localPos, float radius, float height, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false); go.transform.localPosition = localPos; go.transform.localScale = new Vector3(radius * 2, height / 2, radius * 2);
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material mat)
        {
            var go = Cylinder(parent, "Rod", Vector3.zero, radius, (b - a).magnitude, mat);
            go.transform.position = (a + b) / 2; go.transform.up = b - a;
            return go;
        }

        static GameObject Tube(Transform parent, string name, Vector3 localPos, float rBottom, float rTop, float thickness, float height, Material mat,
                               int segments = 48, float gapCenterDeg = 0, float gapAngleDeg = 0, float gapHeight = 0)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = localPos;
            var tw = go.AddComponent<TubeWall>();
            tw.innerRadiusBottom = rBottom; tw.innerRadiusTop = rTop; tw.thickness = thickness; tw.height = height; tw.segments = segments;
            tw.gapCenterDeg = gapCenterDeg; tw.gapAngleDeg = gapAngleDeg; tw.gapHeight = gapHeight; tw.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static GameObject Disc(Transform parent, string name, float topY, float radius, float thickness, Material mat)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0, topY, 0);
            var d = go.AddComponent<DiscPlate>(); d.radius = radius; d.thickness = thickness; d.centerRise = 0; d.convex = false; d.Rebuild();
            go.GetComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        static Transform Anchor(Transform parent, string name, Vector3 worldPos, Vector3? lookAt)
        {
            var t = new GameObject(name).transform; t.SetParent(parent, false); t.position = worldPos;
            if (lookAt.HasValue) t.rotation = Quaternion.LookRotation(lookAt.Value - worldPos);
            return t;
        }

        static PhysicsMaterial Slick()
        {
            string path = $"{MatDir}/Slick.physicsMaterial";
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (pm) return pm;
            Directory.CreateDirectory(MatDir);
            pm = new PhysicsMaterial("Slick") { dynamicFriction = 0f, staticFriction = 0f, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0.05f };
            AssetDatabase.CreateAsset(pm, path);
            return pm;
        }

        static Material Mat(string name, Color color, bool transparent = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) return m;
            Directory.CreateDirectory(MatDir);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Cull", (float)CullMode.Off);   // 生成メッシュの巻き方向に依存しない
            m.SetFloat("_Smoothness", transparent ? 0.9f : 0.3f);
            if (transparent)
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.SetFloat("_ZWrite", 0f);
                m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetOverrideTag("RenderType", "Transparent"); m.renderQueue = (int)RenderQueue.Transparent;
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            AssetDatabase.CreateAsset(m, path);
            return m;
        }
    }
}
