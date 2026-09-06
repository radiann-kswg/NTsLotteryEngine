using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>
    /// ボールテクスチャ確認シーンの生成（冪等）。Tools &gt; NTsLoto &gt; Build Ball View Scene。
    /// 球 1 個＋カメラ＋ライトだけ。姿勢・角速度は BallSkinViewer に生のクォータニオンで指定する。
    /// ついでに Assets/Textures/BallSkins/BallTex_NTS-{Num_Badge}.png を BallSkins.asset の空き行へ割り当てる
    /// （命名規約は同フォルダの README.md。手貼り済みの行は触らない）。
    /// </summary>
    public static class BallViewSceneBuilder
    {
        const string ScenePath = "Assets/Scenes/BallViewScene.unity";
        const string BallPrefabPath = "Packages/net.numbertales-radiann.lotteryballkit/Prefabs/NumberBall.prefab";
        const string SkinTablePath = "Assets/Data/BallSkins.asset";
        const string SkinDir = "Assets/Textures/BallSkins";

        [MenuItem("Tools/NTsLoto/Build Ball View Scene")]
        public static void Build()
        {
            var table = AssetDatabase.LoadAssetAtPath<BallSkinTable>(SkinTablePath);
            if (!table) Debug.LogWarning($"{SkinTablePath} が無い（Tools > NTsLoto > Build Loto Scene で作られる）。白球で開く");
            int linked = table ? LinkTextures(table) : 0;

            var scene = File.Exists(ScenePath)
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects()) Object.DestroyImmediate(go);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BallPrefabPath);
            if (!prefab) { Debug.LogError($"NumberBall prefab が見つからない: {BallPrefabPath}"); return; }

            var ball = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            ball.name = "Ball";
            ball.transform.position = Vector3.zero;
            var rb = ball.GetComponent<Rigidbody>();
            if (rb) { rb.isKinematic = true; rb.useGravity = false; }   // 落とさない。姿勢は BallSkinViewer が直接書く
            var viewer = ball.AddComponent<BallSkinViewer>();
            viewer.skins = table;
            viewer.number = 6;
            viewer.ApplySkin();   // AddComponent 直後の OnEnable は skins=null で走っている（フィールド代入では OnValidate が飛ばない）

            // 球径 0.1m。0.20m 手前から fov 60° で撮ると球が画面の約半分
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.01f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.09f, 0.09f, 0.11f);
            camGo.transform.SetPositionAndRotation(new Vector3(0, 0, -0.20f), Quaternion.identity);

            var lightGo = new GameObject("Directional Light");
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.None;
            lightGo.transform.rotation = Quaternion.Euler(35, -25, 0);

            // 環境光が無いと陰側が真っ黒でテクスチャが読めない
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.38f, 0.39f, 0.44f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[BallViewSceneBuilder] built: {ScenePath}（テクスチャ自動リンク +{linked}）");
        }

        /// <summary>空の行だけ命名規約の PNG で埋める（冪等・手貼りは保持）。割り当てた行数を返す。</summary>
        static int LinkTextures(BallSkinTable table)
        {
            int n = 0;
            foreach (var s in table.skins)
            {
                if (s.texture) continue;
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{SkinDir}/BallTex_NTS-{s.DbNum}.png");
                if (!tex) continue;
                s.texture = tex;
                n++;
            }
            if (n > 0) { EditorUtility.SetDirty(table); AssetDatabase.SaveAssets(); }
            return n;
        }
    }
}
