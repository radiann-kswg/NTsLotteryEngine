using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NTsLotteryEngine.Watch
{
    /// <summary>
    /// ガムボール観賞機。タンクから1球を排出し、既存の螺旋樋を物理で転がす。
    /// 充填球はスキンごとに結合した描画専用メッシュ。動的 Rigidbody は排出球1個だけ。
    /// </summary>
    public class WatchCoaster : MonoBehaviour
    {
        // gen_coaster.py と同じ値
        public const float R = 0.55f, ZTop = 1.50f, Turns = 4f, Pitch = 0.36f, FlatDeg = 30f;
        public const float EndDeg = 360f * Turns + FlatDeg;
        /// <summary>Blender の P(θ, r, z) を Unity 座標へ（FBX 規約: Unity 角 = Blender θ + 180°）。</summary>
        public static Vector3 Pt(float deg, float r, float y) { float a = (deg + 180f) * Mathf.Deg2Rad; return new Vector3(r * Mathf.Sin(a), y, r * Mathf.Cos(a)); }
        public static Vector3 Tangent(float deg) { float a = (deg + 180f) * Mathf.Deg2Rad; return new Vector3(Mathf.Cos(a), 0, -Mathf.Sin(a)); }   // θ 増加方向
        public static readonly Vector3 TrayLocal = Pt(EndDeg, R, 0f) + Tangent(EndDeg) * 0.45f;   // 出口の接線上。床の天端 y=0.01
        public static readonly Vector3 OutletLocal = Pt(8f, R, 1.73f);

        public Transform gate;
        public Mesh fillMesh;
        public Material fillMaterial;
        public bool Dispensing { get; private set; }
        public int ReservoirBallCount { get; private set; }
        public int DispensedCount { get; private set; }
        public int CompletedCount { get; private set; }
        public int FaultCount { get; private set; }
        Vector3 gateClosed;
        readonly List<Object> owned = new List<Object>();

        [Tooltip("樋を転がる間の線形減衰（空気抵抗の代わり。終端速度 ≈ g·sinθ·5/7 ÷ この値。0.5 で ≈1.5 m/s）")] public float damping = 0.5f;

        [Tooltip("排出前に機械全体を見せる秒数")] public float dispensePause = 2f;
        [Tooltip("入口で樋に沿って与える初速 [m/s]")] public float entrySpeed = 0.4f;
        [Tooltip("トレイに居続けたら回収するまでの秒数")] public float settle = 3f;
        [Tooltip("これ以上経っても着かなければ詰まりとみなして回収")] public float timeout = 90f;

        void Awake() { if (gate) gateClosed = gate.localPosition; }

        public void Fill(IReadOnlyList<BallSkin> skins)
        {
            if (ReservoirBallCount != 0 || skins.Count == 0 || !fillMesh || !fillMaterial) return;
            var groups = new List<CombineInstance>[skins.Count];
            for (int i = 0; i < groups.Length; i++) groups[i] = new List<CombineInstance>();
            // ponytail: this fixed 123-ball packing covers the 106-row table. Expand the packing if the catalog outgrows it.
            for (int layer = 0; layer < 5; layer++)
            for (int z = -3; z <= 3; z++)
            for (int x = -3; x <= 3; x++)
            {
                var p = new Vector3(x * .17f + (layer % 2) * .085f, 2.025f + layer * .15f, z * .17f);
                var q = p - new Vector3(0, 2.34f, 0);
                if (q.x*q.x/(.56f*.56f) + q.y*q.y/(.44f*.44f) + q.z*q.z/(.52f*.52f) > 1f) continue;
                int i = ReservoirBallCount++;
                groups[i % skins.Count].Add(new CombineInstance { mesh = fillMesh,
                    transform = Matrix4x4.TRS(p, Quaternion.Euler(-70f + (i % 3)*12, i*137.5f, i%5*9), Vector3.one*1.6f) });
            }
            for (int i = 0; i < groups.Length; i++)
            {
                if (groups[i].Count == 0) continue;
                var mesh = new Mesh { name = "Reservoir_" + i };
                mesh.CombineMeshes(groups[i].ToArray(), true, true); mesh.UploadMeshData(true); owned.Add(mesh);
                var material = new Material(fillMaterial) { name = "Reservoir_" + i };
                material.SetTexture("_BaseMap", skins[i].texture); owned.Add(material);
                var go = new GameObject("StoredSkin_" + i); go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        void OnDisable() { Dispensing = false; if (gate) gate.localPosition = gateClosed; }
        void OnDestroy() { foreach (var obj in owned) if (obj) Destroy(obj); }

        /// <summary>待機→ゲート開放→螺旋→トレイ。回収は不透明なカラー内部で行い、外側のリフトは使わない。</summary>
        public IEnumerator Run(NumberBall ball)
        {
            var rb = ball.GetComponent<Rigidbody>();
            Dispensing = true;
            if (gate) gate.localPosition = gateClosed;
            rb.isKinematic = true; rb.detectCollisions = false;
            rb.position = W(OutletLocal + Vector3.up * .19f);
            rb.rotation = BallSkinViewer.DefaultPose;
            yield return new WaitForSeconds(dispensePause);
            // The single ball is metered out of the opaque collar, then released under gravity.
            yield return BallUtil.Carry(rb, new[] { W(OutletLocal) }, .25f);
            rb.isKinematic = true; rb.detectCollisions = false;
            float opening = 0;
            while (opening < .35f)
            {
                opening += Time.fixedDeltaTime;
                if (gate) gate.localPosition = gateClosed + gate.parent.InverseTransformVector(transform.right * (.20f * Mathf.Clamp01(opening/.35f)));
                yield return new WaitForFixedUpdate();
            }
            rb.isKinematic = false; rb.detectCollisions = true;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;   // isKinematic の往復で CCD が落ちる（AGENTS 罠 3）
            rb.linearDamping = damping;   // ponytail: 減衰で速度を抑える（実物は鈴やバンプで減速する）。KuruunTower.Run が自分の値に戻す
            rb.linearVelocity = transform.rotation * Tangent(8f) * entrySpeed;
            DispensedCount++;
            yield return new WaitForSeconds(.65f);
            if (gate) gate.localPosition = gateClosed;
            Dispensing = false;

            float t = 0f, inTray = 0f;
            var tray = W(TrayLocal);
            while (t < timeout)
            {
                t += Time.fixedDeltaTime;
                var d = rb.position - tray; d.y = 0;
                inTray = d.magnitude < 0.3f && rb.position.y < tray.y + 0.2f ? inTray + Time.fixedDeltaTime : 0f;
                if (inTray >= settle) { CompletedCount++; yield break; }
                if (rb.position.y < transform.position.y - .3f) break;
                yield return new WaitForFixedUpdate();
            }
            FaultCount++;
            Debug.LogWarning($"[WatchCoaster] ball {ball.number} did not reach the tray (y={rb.position.y:F2}); returning to dispenser");
        }

        Vector3 W(Vector3 local) => transform.TransformPoint(local);
    }
}
