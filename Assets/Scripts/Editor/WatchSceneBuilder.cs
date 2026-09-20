using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NTsLotteryEngine.Watch;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// 観賞ビルド（docs/WATCH.md）のシーン生成（冪等）。Tools &gt; NTsLoto &gt; Build Watch Scene / Build Title Scene。
    /// WatchScene = 塔 1 本（LotoSceneBuilder.BuildTower の流用・Streaks[3]=ball 2・10 段）＋螺旋コースター（Coaster_Helix.fbx）＋カメラ＋WatchDirector。
    /// ついでに Pi 用 URP アセット Assets/Resources/Pi_RPAsset.asset（Mobile_RPAsset の複製から影・HDR・MSAA・深度/不透明テクスチャを落とす）を作る。
    /// </summary>
    public static class WatchSceneBuilder
    {
        const string BallPrefabPath = "Packages/net.numbertales-radiann.lotteryballkit/Prefabs/NumberBall.prefab";
        const string SkinTablePath = "Assets/Data/BallSkins.asset";
        const string FontPath = "Assets/Fonts/PenchantManufacture.otf";
        const int TowerIndex = 3;   // LotoRules.Streaks[3] = ball 2（p50・10 段）。段数が多く落下が長い

        [MenuItem("Tools/NTsLoto/Build Watch Scene")]
        public static void BuildWatch()
        {
            PiPipeline();
            var scene = Open(TitleMenu.Watch);
            var frame = LotoSceneBuilder.Mat("Frame", new Color(0.18f, 0.18f, 0.20f));
            var rail = LotoSceneBuilder.Mat("Rail", new Color(0.85f, 0.82f, 0.75f));
            var glass = LotoSceneBuilder.Mat("Glass", new Color(0.80f, 0.90f, 1f, 0.10f), true);
            var bowl = LotoSceneBuilder.Mat("Bowl", new Color(0.93f, 0.94f, 0.96f, 0.75f), true);
            var slick = LotoSceneBuilder.Slick();

            var root = new GameObject("Watch").transform;
            LotoSceneBuilder.Box(root, "Ground", new Vector3(0, -0.05f, 0), new Vector3(12, 0.1f, 12), frame);

            // ---- 塔 ----
            var towerRoot = new GameObject("TowerRoot").transform; towerRoot.SetParent(root, false);
            var tower = LotoSceneBuilder.BuildTower(towerRoot, TowerIndex, new Vector3(0, 0, 0.001f), bowl, frame, rail, glass);   // pos は正面向きを決めるだけ（ゼロだと LookRotation が警告）
            tower.name = "WatchTower";   // Output/drop_azimuth.csv の塔名が Loto の統計と混ざらないように
            foreach (var col in towerRoot.GetComponentsInChildren<Collider>())
                if (col.name == "Sill" || col.name == "SideL" || col.name == "SideR" || col.name == "TrayFloor" || col.name.StartsWith("Channel")) col.sharedMaterial = slick;

            // ---- コースター ----
            var coasterRoot = new GameObject("CoasterRoot").transform; coasterRoot.SetParent(root, false);
            var coaster = coasterRoot.gameObject.AddComponent<WatchCoaster>();
            var helix = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Coaster_Helix.fbx");
            if (!helix) Debug.LogError("Coaster_Helix.fbx が無い（Blender で BlenderSources/gen_coaster.py を実行）");
            else LotoSceneBuilder.Fbx(helix, coasterRoot, "Helix", Vector3.zero, rail);
            LotoSceneBuilder.Cylinder(coasterRoot, "Column", WatchCoaster.ColumnLocal + Vector3.up * (WatchCoaster.ZTop + 0.45f) / 2, 0.05f, WatchCoaster.ZTop + 0.45f, frame);
            var tray = new GameObject("Tray").transform; tray.SetParent(coasterRoot, false); tray.localPosition = WatchCoaster.TrayLocal;
            tray.localRotation = Quaternion.LookRotation(WatchCoaster.Tangent(WatchCoaster.EndDeg));   // 出口の接線を向く。手前（−z）の壁は無し＝球が入れる
            LotoSceneBuilder.Box(tray, "TrayFloor", Vector3.zero, new Vector3(0.6f, 0.02f, 0.6f), rail);
            foreach (var (n, p, sz) in new[] { ("W0", new Vector3(0, 0.1f, 0.3f), new Vector3(0.62f, 0.2f, 0.02f)),
                                               ("W2", new Vector3(0.3f, 0.1f, 0), new Vector3(0.02f, 0.2f, 0.62f)), ("W3", new Vector3(-0.3f, 0.1f, 0), new Vector3(0.02f, 0.2f, 0.62f)) })
                LotoSceneBuilder.Box(tray, n, p, sz, glass).GetComponent<Collider>().sharedMaterial = slick;   // 反発 0: 出口から来た球を跳ね返さない
            var lift = WatchAudio.Make3D(coasterRoot.gameObject, AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/SFX/Lift_Loop.wav"), true);
            lift.playOnAwake = true; lift.volume = WatchAudio.LiftVolume;

            // ---- カメラ・照明・進行役 ----
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.02f; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            camGo.transform.position = new Vector3(0, 1f, -2f);
            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(50, -30, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(0.38f, 0.39f, 0.44f);

            var d = root.gameObject.AddComponent<WatchDirector>();
            d.skins = AssetDatabase.LoadAssetAtPath<BallSkinTable>(SkinTablePath);
            d.ballPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath)?.GetComponent<NumberBall>();
            d.tower = tower; d.coaster = coaster; d.cam = cam; d.hudFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            if (!d.skins || !d.ballPrefab) Debug.LogError("[WatchSceneBuilder] BallSkins.asset か NumberBall prefab が無い");

            Physics.SyncTransforms();
            // 入口の樋の床を実測して FBX の角度規約（Unity 角 = Blender θ + 180°）を確かめる。期待 ≈ ZTop − Pitch·8/360 = 1.492
            var probe = WatchCoaster.Pt(8f, WatchCoaster.R, WatchCoaster.ZTop + 0.5f);
            if (Physics.Raycast(probe, Vector3.down, out var hit, 2f)) Debug.Log($"[WatchSceneBuilder] coaster entry floor y={hit.point.y:F3} (expect ≈{WatchCoaster.ZTop - WatchCoaster.Pitch * 8f / 360f:F3}) on {hit.collider.name}");
            else Debug.LogWarning("[WatchSceneBuilder] coaster entry probe missed the helix — check gen_coaster.py / Pt()");

            Save(scene, TitleMenu.Watch);
            Debug.Log("[WatchSceneBuilder] built WatchScene");
        }

        [MenuItem("Tools/NTsLoto/Build Title Scene")]
        public static void BuildTitle()
        {
            PiPipeline();
            var scene = Open(TitleMenu.Title);
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            camGo.AddComponent<AudioListener>();
            new GameObject("Title").AddComponent<TitleMenu>().hudFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            Save(scene, TitleMenu.Title);
            Debug.Log("[WatchSceneBuilder] built TitleScene");
        }

        static UnityEngine.SceneManagement.Scene Open(string path)
        {
            var scene = File.Exists(path) ? EditorSceneManager.OpenScene(path, OpenSceneMode.Single) : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);
            return scene;
        }

        static void Save(UnityEngine.SceneManagement.Scene scene, string path)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            foreach (var p in new[] { TitleMenu.Title, TitleMenu.Watch, TitleMenu.BallView })
                if (File.Exists(p) && !scenes.Exists(s => s.path == p)) scenes.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Pi 用 URP アセット（WatchBoot が実行時に QualitySettings.renderPipeline へ差す）。値はここが正（再実行で追従）。</summary>
        public static void PiPipeline()
        {
            const string src = "Assets/Settings/Mobile_RPAsset.asset", dst = "Assets/Resources/Pi_RPAsset.asset";
            var a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(dst);
            if (!a) { Directory.CreateDirectory("Assets/Resources"); AssetDatabase.CopyAsset(src, dst); a = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(dst); }
            var so = new SerializedObject(a);
            foreach (var (k, v) in new[] { ("m_MainLightShadowsSupported", 0), ("m_AdditionalLightShadowsSupported", 0), ("m_SupportsHDR", 0), ("m_MSAA", 1), ("m_RequireDepthTexture", 0), ("m_RequireOpaqueTexture", 0), ("m_AdditionalLightsRenderingMode", 0) })
            {
                var p = so.FindProperty(k);
                if (p == null) { Debug.LogWarning($"[PiPipeline] {k} が無い"); continue; }
                if (p.propertyType == SerializedPropertyType.Boolean) p.boolValue = v != 0; else p.intValue = v;
            }
            so.FindProperty("m_RenderScale").floatValue = 1f;
            so.FindProperty("m_ShadowDistance").floatValue = 0f;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(a); AssetDatabase.SaveAssets();
        }
    }
}
