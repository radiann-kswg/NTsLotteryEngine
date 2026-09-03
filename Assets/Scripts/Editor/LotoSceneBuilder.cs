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
        const float TowerArcR = 6.2f, TowerBaseY = 0.9f;   // 隣の塔との弦 3.2m ＞ ボウル径 2.0 ＋ 千鳥 0.85   // 塔はロトマシーンを中心とする半円弧（扇状）に並べ、正面を機械に向ける（User 指定 2026-09-03。横一列は俯瞰が横に広すぎる）。TowerBaseY = 最下段ボウル底の高さ（完走トレイ＋通過トリガー分）
        static readonly int[] TowerArcOrder = { 0, 2, 5, 4, 3, 6, 1 };   // 弧の左→右に置く Streaks の index。段数の多い塔（33:x11, 2:x10）を中央奥へ、単発（1, 10）を両端へ
        const string SkinTablePath = "Assets/Data/BallSkins.asset";
        const string SampleSkinPath = "Packages/net.numbertales-radiann.lotteryballkit/Textures/BallSkins_Sample.png";
        const float FunnelH = 0.28f;

        [MenuItem("Tools/NTsLoto/Build Loto Scene")]
        public static void Build()
        {
            var scene = SceneManager.GetActiveScene().path == ScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (go.name == "Loto" || go.name == "Main Camera" || go.name == "Directional Light") Object.DestroyImmediate(go);

            var glass = Mat("Glass", new Color(0.80f, 0.90f, 1f, 0.10f), true);   // 0.22 だと筒が曇って中の球が見えない。篩の追従カメラ（1.9m）だと 0.14 でも白い
            var frame = Mat("Frame", new Color(0.18f, 0.18f, 0.20f));
            var rail = Mat("Rail", new Color(0.85f, 0.82f, 0.75f));
            var bowlMat = Mat("Bowl", new Color(0.93f, 0.94f, 0.96f, 0.75f), true);

            var root = new GameObject("Loto").transform;
            Box(root, "Ground", new Vector3(0, -0.05f, 0), new Vector3(30, 0.1f, 30), frame);

            // ---- 篩型ロトマシーン ×2（Blender 生成の回転皿。到達順が抽選順。2026-09-03 User 指示）----
            var machine = new GameObject("LotoMachine").transform; machine.SetParent(root, false);
            var lower = BuildSieve(machine, "SieveL", "Sieve_Dish_L", LowerLayers, new Vector3(0, 0, 0), 0.70f, 0.34f, new Vector3(0.3f, 0, RailZ), bowlMat, frame, glass);     // 12〜99・15 層
            var upper = BuildSieve(machine, "SieveU", "Sieve_Dish_U", UpperLayers, new Vector3(-1.7f, 0, 0), 0.45f, 0.32f, new Vector3(-0.8f, 0, RailZ), bowlMat, frame, glass);  // 2〜11・4 層

            // 結果レール（+X 側が 3° 低い）
            var railT = new GameObject("ResultRail").transform; railT.SetParent(root, false);
            railT.SetPositionAndRotation(new Vector3(0, RailY, RailZ), Quaternion.Euler(0, 0, -3f));
            Box(railT, "Floor", new Vector3(0, 0, 0), new Vector3(RailLen, 0.02f, 0.24f), rail);
            Box(railT, "WallF", new Vector3(0, 0.15f, -0.13f), new Vector3(RailLen, 0.30f, 0.02f), glass);   // シュートから 1.5m/s で来る球を跳び越えさせない
            Box(railT, "WallB", new Vector3(0, 0.06f, 0.13f), new Vector3(RailLen, 0.12f, 0.02f), glass);
            Box(railT, "StopR", new Vector3(RailLen / 2, 0.06f, 0), new Vector3(0.02f, 0.12f, 0.28f), frame);
            Box(railT, "StopL", new Vector3(-RailLen / 2, 0.06f, 0), new Vector3(0.02f, 0.12f, 0.28f), frame);

            // ---- 別ボール 7 塔（縦連クルーン）----
            var towers = new KuruunTower[LotoRules.Streaks.Length];
            for (int slot = 0; slot < TowerArcOrder.Length; slot++)
            {
                float phi = Mathf.Lerp(180f, 0f, (float)slot / (TowerArcOrder.Length - 1)) * Mathf.Deg2Rad;   // 左(+X 側から見て 180°) → 右(0°)。中央 90° が真後ろ
                int i = TowerArcOrder[slot];
                towers[i] = BuildTower(root, i, new Vector3(Mathf.Cos(phi) * TowerArcR, 0, Mathf.Sin(phi) * TowerArcR), bowlMat, frame, rail, glass);
            }

            // ---- カメラ・照明・進行役 ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); camGo.AddComponent<AudioListener>();
            cam.nearClipPlane = 0.05f; cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f); cam.clearFlags = CameraClearFlags.SolidColor;
            var camMachine = Anchor(root, "Cam_Machine", new Vector3(-0.7f, 3.4f, -9.0f), new Vector3(-0.7f, 3.0f, 0));   // 15 層の篩（高さ ≈ 5.4m）と 4 層の篩を横並びで収める
            var camOverview = Anchor(root, "Cam_Overview", new Vector3(0, 8.0f, -12.0f), new Vector3(0, 4.0f, 1.5f));   // 扇状配置の全景（中央奥の 11 段塔 ≈ 15m は入り切らない。塔の全景は今後 Blender 化と合わせて再検討）
            camGo.transform.SetPositionAndRotation(camMachine.position, camMachine.rotation);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);

            var director = new GameObject("Director").AddComponent<LotoDirector>();
            director.gameObject.AddComponent<DerailWatch>();   // 脱線監視（球ごとに 1 回警告）
            director.transform.SetParent(root, false);
            director.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath)?.GetComponent<NumberBall>();
            if (!director.ballPrefab) Debug.LogError($"NumberBall prefab が見つからない: {BallPrefabPath}（Packages/manifest.json の LotteryBallKit を確認）");
            director.upper = upper; director.lower = lower; director.towers = towers;
            director.cam = cam; director.camMachine = camMachine; director.camOverview = camOverview;
            director.hudFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/PenchantManufacture.otf");
            if (!director.hudFont) Debug.LogWarning("PenchantManufacture.otf が無い（scripts/setup-submodule で同期）。HUD は既定フォントで描く");
            director.skins = SkinTable();

            // シュート・筒・チャンネルは摩擦ゼロ・反発ゼロ（Slick）。皿・ボウル・コレクタは FBX 既定（摩擦 0.6）
            var slick = Slick();
            foreach (var col in root.GetComponentsInChildren<Collider>())
            {
                string nm = col.name;
                if (nm == "Sill" || nm == "SideL" || nm == "SideR" || nm == "Back" || nm == "Housing" || nm == "TrayFloor" || nm.StartsWith("Channel"))   // 漏斗は摩擦あり（周回減衰）
                    col.sharedMaterial = slick;
            }

            Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == ScenePath)) { scenes.Add(new EditorBuildSettingsScene(ScenePath, true)); EditorBuildSettings.scenes = scenes.ToArray(); }
            Debug.Log($"[LotoSceneBuilder] built: sieves={LowerLayers}+{UpperLayers} layers, towers={towers.Length}");
        }

        const int LowerLayers = 15, UpperLayers = 4;

        /// <summary>篩: 回転皿を layers 枚重ね、最下層の下に漏斗→受け口トリガー→レールへのシュート。dish は Assets/Models/&lt;dish&gt;.fbx（穴リング面が y=0）。</summary>
        static SieveMachine BuildSieve(Transform parent, string name, string dish, int layers, Vector3 pos, float rimR, float pitch, Vector3 railPoint, Material bowlMat, Material frame, Material glass)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/{dish}.fbx");
            var root = new GameObject(name).transform; root.SetParent(parent, false); root.localPosition = pos;
            var m = root.gameObject.AddComponent<SieveMachine>();
            if (!prefab) { Debug.LogError($"{dish}.fbx が無い（Blender で gen_kuruun.py を実行）"); return m; }
            const float BaseY = 0.80f;   // 最下層の穴リング面（喉 0.48 → シュート 0.30 → レール 0.05）
            m.dishes = new Rotator[layers];
            for (int i = 0; i < layers; i++)
            {
                float y = BaseY + (layers - 1 - i) * pitch;
                var d = Fbx(prefab, root, $"Dish{i:00}", new Vector3(0, y, 0), bowlMat);
                var rb = d.AddComponent<Rigidbody>(); rb.isKinematic = true;
                var rot = d.AddComponent<Rotator>(); m.dishes[i] = rot;
                d.transform.localRotation = Quaternion.AngleAxis(i * 37f, Vector3.up) * d.transform.localRotation;   // 穴の位相をずらす（真下に穴が並ぶと素通り）
            }
            float top = BaseY + (layers - 1) * pitch;
            // 透明筒。皿の縁との隙間は 0.03 ＜ d/2（球が入れない）。0.06 にしたら回転する皿と静止筒に挟まれた球が弾け飛んで 132m 先まで飛んだ（2026-09-03。RSC 罠48 の派生: 可動体と静止体の隙間は 0.5d 未満か 1.5d 超）
            Tube(root, "Housing", new Vector3(0, BaseY - 0.35f, 0), rimR - 0.01f, rimR - 0.01f, 0.02f, top - BaseY + 0.35f + 0.60f, glass, 72);   // 皿の縁と重ねる（隙間ゼロ。0.03 でも山に押された球が回転皿と静止筒に挟まれて突き抜けた）
            Tube(root, "Funnel", new Vector3(0, BaseY - 0.02f - 0.30f, 0), 0.12f, rimR - 0.05f, 0.02f, 0.30f, glass, 48);                    // 最下層の穴 → 喉 2.4d
            m.exit = MakeTrigger(Box(root, "Exit", new Vector3(0, BaseY - 0.38f, 0), new Vector3(0.30f, 0.06f, 0.30f), null));
            m.gate = Box(root, "Gate", new Vector3(0, BaseY - 0.31f, 0), new Vector3(0.30f, 0.02f, 0.30f), frame); m.gate.SetActive(false);   // 抽選が終わったら喉を塞ぐ（残りの球はレールへ出さない）
            // 喉の下のシュート: 受け口の真下から railPoint（レール上の点）へ 8° 下り
            var from = new Vector3(0, BaseY - 0.50f, 0); var to = new Vector3(railPoint.x - pos.x, 0, railPoint.z - pos.z);
            var dir = (to - new Vector3(0, 0, 0)).normalized; float len = Vector3.Distance(Vector3.zero, to) - 0.05f;
            var chute = new GameObject("Chute").transform; chute.SetParent(root, false);
            chute.localPosition = from; chute.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(8f, 0, 0);
            Box(chute, "Sill", new Vector3(0, -0.01f, len / 2), new Vector3(0.30f, 0.02f, len + 0.10f), frame);
            Box(chute, "SideL", new Vector3(-0.16f, 0.08f, len / 2), new Vector3(0.02f, 0.18f, len + 0.10f), glass);
            Box(chute, "SideR", new Vector3(0.16f, 0.08f, len / 2), new Vector3(0.02f, 0.18f, len + 0.10f), glass);
            Box(chute, "Back", new Vector3(0, 0.08f, -0.04f), new Vector3(0.30f, 0.18f, 0.02f), glass);
            Cylinder(root, "Pedestal", new Vector3(0, (BaseY - 0.55f) / 2, 0), 0.25f, BaseY - 0.55f, frame);
            var spawn = new GameObject("Spawn").transform; spawn.SetParent(root, false); spawn.localPosition = new Vector3(0, top + 0.45f, 0); m.spawnCenter = spawn; m.spawnRadius = rimR - 0.15f;
            m.camAnchor = Anchor(root, "Cam", root.TransformPoint(new Vector3(0, 0, -(rimR + 1.9f))), null);   // 正面。FOV 40 で縦 ≈ 1.9m（5〜6 層）。高さは最下の球に追従（SieveMachine.Follow）
            return m;
        }

        // ---- クルーン塔（Blender 生成メッシュ: Assets/Models/Kuruun_Bowl.fbx / Kuruun_Collector_<variant>.fbx。原本 BlenderSources/gen_kuruun.py + kuruun_params.json）----
        // FBX の角度規約: Unity 角 = Blender θ + 180°（2026-09-03 レイキャスト実測）。コレクタの樋出口は Blender 0° = Unity 180°（正面 -Z）。
        public const float BlenderDegOffset = 180f;
        const float BowlRimR = 1.00f, BowlRimH = 0.56f, ConeR0 = 0.65f, ConeH = 0.28f;   // kuruun_params.json の bowl と一致させる
        const float TowerStagger = 0.425f;   // 段ごとに x を ±0.425 交互 → 上段の喉（軸上）が下段コーン面 r=0.85 に落ちる
        // 穴リング面の段間隔。喉(−0.70)→次段コーン面(−1.45+0.16) = 0.59 の落差（LotoMonteCarlo.entryHeight と同じ値で校正する）。
        // 1.25 だと下段ボウルの縁の天端（−1.25+0.56 = −0.69）が上段の樋の床（出口側 −0.81〜）を突き抜け、樋を走ってきた球が出口の 12〜22° 手前で止まった（2026-09-03 録画・全塔で再現）。
        // 縁の天端が樋の最深部より球半径以上下（−0.87 以下）になる 1.45 以上にする
        const float TowerPitch = 1.45f;
        const float EntryR = 0.85f;
        const float GutterR = 0.94f, GutterExitDepth = 0.81f;   // collector.gutter_r_out / 出口での樋の床の深さ（top_z − drop − depth − fall）

        static float ConeY(float r) => ConeH * Mathf.Clamp01((r - ConeR0) / (BowlRimR - ConeR0));   // 穴リング面からのコーン高さ

        static KuruunTower BuildTower(Transform parent, int index, Vector3 pos, Material bowlMat, Material frame, Material rail, Material glass)
        {
            var s = LotoRules.Streaks[index];
            int n = s.max;
            var root = new GameObject($"Tower{index}_Ball{s.ball:00}_x{n}").transform; root.SetParent(parent, false);
            root.localPosition = pos;
            root.localRotation = Quaternion.LookRotation(new Vector3(pos.x, 0, pos.z).normalized);   // 局所 -Z（カメラ・排出側）を機械に向ける
            var k = root.gameObject.AddComponent<KuruunTower>();
            Vector3 W(Vector3 local) => root.TransformPoint(local);

            var bowlPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Kuruun_Bowl.fbx");
            var colPrefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Models/Kuruun_Collector_{s.variant}.fbx");
            if (!bowlPrefab || !colPrefab) { Debug.LogError($"Kuruun FBX が無い（bowl={bowlPrefab} collector={s.variant}）。Blender で BlenderSources/gen_kuruun.py を実行して"); return k; }

            float zEnd = -(GutterR + 0.45f);   // 落下チャンネル入口（塔の中心線 x=0・正面）
            float zCh = zEnd - 0.13f;
            k.levels = new KuruunTower.Level[n];
            for (int i = 0; i < n; i++)
            {
                float yb = TowerBaseY + (n - 1 - i) * TowerPitch;        // この段の穴リング面
                float xi = (i % 2 == 0 ? -1f : 1f) * TowerStagger;
                var L = new KuruunTower.Level();

                var bowl = Fbx(bowlPrefab, root, "Bowl" + i, new Vector3(xi, yb, 0), bowlMat);
                var rb = bowl.AddComponent<Rigidbody>(); rb.isKinematic = true;
                var rot = bowl.AddComponent<Rotator>(); rot.rpm = rot.targetRpm = k.bowlRpm;   // 回転で穴の位相を一様化（静止だと毎回同じ穴。RSC の罠）
                L.bowl = bowl.transform;
                Fbx(colPrefab, root, "Collector" + i, new Vector3(xi, yb, 0), frame);

                L.passTrigger = MakeTrigger(Box(root, $"Win{i}", new Vector3(xi, yb - 0.44f, 0), new Vector3(0.60f, 0.06f, 0.60f), null));   // コレクタ内縁（−0.36）から落ちた球
                Tube(root, $"Funnel{i}", new Vector3(xi, yb - 0.40f - FunnelH, 0), 0.12f, 0.42f, 0.02f, FunnelH, glass, 40);   // 45° 漏斗・喉 2.4d（ponytail: 配管はまだ ProcMesh。Blender 化は機構が固まってから）
                L.throat = Anchor(root, $"Throat{i}", W(new Vector3(xi, yb - 0.40f - FunnelH - 0.02f, 0)), null);

                // ハズレ: 樋の出口（正面 r=0.94・床 −0.81。kuruun_params.json の collector）→ 6° 下りの真っ直ぐなシュート → 幅 1.2 のチャンネル
                // 塔中心線（x=0）へ 46° 斜めに向けると、回した側板の端が樋の開口の中（r=0.85）に入って外壁沿いに来た球を止め、
                // 口では横向きの速度でチャンネルの開いた後面から反対側へ抜けた（2026-09-03 録画 ×2）。段の軸の真正面へ真っ直ぐ出す
                var origin = new Vector3(xi, 0, -(GutterR + 0.04f)); var mouth = new Vector3(xi, 0, zEnd);   // 樋の外壁（外面 0.96）の外から始める
                var dir = (mouth - origin).normalized;
                float Lc = Vector3.Distance(origin, mouth);
                var chute = new GameObject($"Chute{i}").transform; chute.SetParent(root, false);
                chute.localPosition = origin + new Vector3(0, yb - GutterExitDepth - 0.02f, 0);
                chute.localRotation = Quaternion.LookRotation(dir) * Quaternion.Euler(6f, 0, 0);
                Box(chute, "Sill", new Vector3(0, -0.01f, Lc / 2 - 0.05f), new Vector3(0.36f, 0.02f, Lc + 0.10f), frame);   // 出口の開口 20°（r=0.94 で 0.33）より広く、樋の床の下まで差し込む（縁から出た球を落とさない）
                Box(chute, "SideL", new Vector3(-0.19f, 0.07f, Lc / 2), new Vector3(0.02f, 0.16f, Lc), glass);
                Box(chute, "SideR", new Vector3(0.19f, 0.07f, Lc / 2), new Vector3(0.02f, 0.16f, Lc), glass);
                L.exitTrigger = MakeTrigger(Box(chute, "ExitTrigger", new Vector3(0, 0.07f, Lc * 0.5f), new Vector3(0.32f, 0.14f, 0.06f), null));
                k.levels[i] = L;
            }

            float top = TowerBaseY + (n - 1) * TowerPitch;
            float chH = top - 0.55f;
            // 落下チャンネル: 幅 1.2（x=±0.425 の両方のシュートを真っ直ぐ受ける）・奥行 0.24。球は前板（Slick・反発 0）に当たって真下へ落ち、ハズレトレイで止まる
            const float ChW = 1.24f;
            Box(root, "ChannelF", new Vector3(0, 0.1f + chH / 2, zEnd - 0.25f), new Vector3(ChW, chH, 0.02f), glass);
            Box(root, "ChannelL", new Vector3(-ChW / 2, 0.1f + chH / 2, zCh), new Vector3(0.02f, chH, 0.24f), glass);
            Box(root, "ChannelR", new Vector3(ChW / 2, 0.1f + chH / 2, zCh), new Vector3(0.02f, chH, 0.24f), glass);
            // トレイの側壁はチャンネルの側板より外に置く（側板の内側にあると、側板沿いに 15m/s で落ちてきた球が壁の天端に当たって 9m/s で横へ弾かれた 2026-09-03）
            k.loseTray = Tray(root, "LoseTray", new Vector3(0, 0.05f, zCh), rail, glass, ChW + 0.06f);
            k.winTray = Tray(root, "WinTray", new Vector3(((n - 1) % 2 == 0 ? -1f : 1f) * TowerStagger, 0.05f, 0), rail, glass);   // 最下段の喉の真下
            foreach (float sx in new[] { -(BowlRimR + TowerStagger + 0.12f), BowlRimR + TowerStagger + 0.12f }) Rod(root, W(new Vector3(sx, 0, 0)), W(new Vector3(sx, top + BowlRimH, 0)), 0.025f, frame);

            // 投入: 最上段ボウルのコーン面 r=EntryR・角度 135°（Unity 角）の上へ、下段と同じ落差（喉 −0.70 → 次段コーン面 = TowerPitch − 0.86）で静止落下
            // （MC の entryHeight と同じ条件。接線速度付き 0.30 上からの投入は p50/p53 で p が変わるのでやめた 2026-09-03）
            float a = 135f * Mathf.Deg2Rad;
            k.dropPoint = Anchor(root, "DropPoint", W(new Vector3(-TowerStagger + Mathf.Sin(a) * EntryR, top + ConeY(EntryR) + 0.05f + (TowerPitch - 0.86f), Mathf.Cos(a) * EntryR)), null);
            k.camAnchor = Anchor(root, "Cam", W(new Vector3(0, 0, -(BowlRimR + 2.6f))), null);
            return k;
        }

        static GameObject Fbx(GameObject prefab, Transform parent, string name, Vector3 localPos, Material mat)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = name; go.transform.localPosition = localPos;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                var mc = mf.gameObject.AddComponent<MeshCollider>(); mc.sharedMesh = mf.sharedMesh; mc.sharedMaterial = BallUtil.Machine;
                mf.GetComponent<MeshRenderer>().sharedMaterial = mat;
            }
            return go;
        }

        static BallTrigger MakeTrigger(GameObject go)
        {
            Object.DestroyImmediate(go.GetComponent<MeshRenderer>()); Object.DestroyImmediate(go.GetComponent<MeshFilter>());
            go.GetComponent<BoxCollider>().isTrigger = true;
            return go.AddComponent<BallTrigger>();
        }

        static Transform Tray(Transform parent, string name, Vector3 localPos, Material rail, Material glass, float width = 0.30f)
        {
            var tray = new GameObject(name).transform; tray.SetParent(parent, false); tray.localPosition = localPos;
            Box(tray, "TrayFloor", Vector3.zero, new Vector3(width, 0.02f, 0.30f), rail);   // Slick（反発 0）: 落下チャンネルから 15m/s で来た球を跳ね返さない
            Box(tray, "W0", new Vector3(0, 0.04f, 0.15f), new Vector3(width + 0.02f, 0.08f, 0.02f), glass);
            Box(tray, "W1", new Vector3(0, 0.04f, -0.15f), new Vector3(width + 0.02f, 0.08f, 0.02f), glass);
            Box(tray, "W2", new Vector3(width / 2, 0.04f, 0), new Vector3(0.02f, 0.08f, 0.32f), glass);
            Box(tray, "W3", new Vector3(-width / 2, 0.04f, 0), new Vector3(0.02f, 0.08f, 0.32f), glass);
            return tray;
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

        /// <summary>球スキン表。無ければ作り、足りない行だけ足す（既存行の手貼りテクスチャは保持）。既定テクスチャは LotteryBallKit のサンプル（仮）。</summary>
        static BallSkinTable SkinTable()
        {
            var t = AssetDatabase.LoadAssetAtPath<BallSkinTable>(SkinTablePath);
            if (!t)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SkinTablePath));
                t = ScriptableObject.CreateInstance<BallSkinTable>();
                t.defaultTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(SampleSkinPath);
                AssetDatabase.CreateAsset(t, SkinTablePath);
            }
            int added = t.Populate();
            if (added > 0) { EditorUtility.SetDirty(t); AssetDatabase.SaveAssets(); }
            Debug.Log($"[LotoSceneBuilder] skin table: {t.skins.Count} balls (+{added})");
            return t;
        }

        // Slick: 摩擦ゼロ・反発ゼロ（bounceCombine=Minimum で球側の 0.6 に勝つ）。フラップの上で跳ねた球が振り分けと逆側へ落ちる（81 の塔で当たり判定ズレ 2026-09-03）
        static PhysicsMaterial Slick() => PhysMat("Slick", 0f, PhysicsMaterialCombine.Minimum, 0f, PhysicsMaterialCombine.Minimum);

        static PhysicsMaterial PhysMat(string name, float friction, PhysicsMaterialCombine frictionCombine, float bounce, PhysicsMaterialCombine bounceCombine)
        {
            string path = $"{MatDir}/{name}.asset";   // .physicsMaterial だと CreateAsset が警告を出し LoadAssetAtPath も拾えない
            var pm = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (!pm) { Directory.CreateDirectory(MatDir); pm = new PhysicsMaterial(name); AssetDatabase.CreateAsset(pm, path); }
            pm.dynamicFriction = friction; pm.staticFriction = friction; pm.frictionCombine = frictionCombine; pm.bounciness = bounce; pm.bounceCombine = bounceCombine;   // 値はここが正（再ビルドで追従）
            EditorUtility.SetDirty(pm);
            return pm;
        }

        static Material Mat(string name, Color color, bool transparent = false)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m) { m.SetColor("_BaseColor", color); m.SetFloat("_Smoothness", transparent ? 0.15f : 0.3f); EditorUtility.SetDirty(m); return m; }   // 色・質感はここが正（再ビルドで追従）
            Directory.CreateDirectory(MatDir);
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Cull", (float)CullMode.Off);   // 生成メッシュの巻き方向に依存しない
            m.SetFloat("_Smoothness", transparent ? 0.15f : 0.3f);   // 0.9 だとガラス筒がハイライトで白飛びして中の球が見えない。0.5 でも近接カメラでは白い
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
