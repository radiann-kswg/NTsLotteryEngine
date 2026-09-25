using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using NTsLotteryEngine.Watch;
using Object = UnityEngine.Object;

namespace NTsLotteryEngine.EditorTools
{
    /// <summary>Runnable acceptance check: imported mesh orientation/budget and an actual physics journey from outlet to tray.</summary>
    public static class WatchGumballCheck
    {
        [MenuItem("Tools/NTsLoto/Validate Gumball")]
        public static void Validate()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play before validation");
            var d = Object.FindFirstObjectByType<WatchDirector>();
            if (!d) throw new InvalidOperationException("Open WatchScene first");
            var c = d.coaster;
            var previousMode = Physics.simulationMode;
            bool towerActive = d.tower.gameObject.activeSelf, coasterActive = c.gameObject.activeSelf;
            NumberBall ball = null;
            try
            {
                d.tower.gameObject.SetActive(false); c.gameObject.SetActive(true);
                if (!c.gate || !c.fillMesh || !c.fillMaterial) throw new Exception("Missing dispenser reference");
                if (!c.fillMesh.isReadable) throw new Exception("Fill mesh must allow runtime CombineMeshes");
                int triangles = c.GetComponentsInChildren<MeshFilter>().Sum(m => (int)m.sharedMesh.GetIndexCount(0)/3);
                if (triangles > 20000 || c.fillMesh.triangles.Length/3 > 300) throw new Exception("Mesh budget exceeded");
                if (c.GetComponentsInChildren<Rigidbody>().Length != 0) throw new Exception("Cabinet must be static");
                var chute = c.GetComponentsInChildren<MeshCollider>().Single(m => m.name == "Chute");
                var delta = chute.bounds.center - c.transform.TransformPoint(WatchCoaster.OutletLocal);
                if (Mathf.Abs(delta.x) > .005f || Mathf.Abs(delta.z) > .005f) throw new Exception("FBX outlet axis mismatch: " + delta);
                Physics.SyncTransforms();
                var ray = new Ray(c.transform.TransformPoint(WatchCoaster.Pt(8, WatchCoaster.R, WatchCoaster.ZTop+.06f)), Vector3.down);
                if (!Physics.Raycast(ray, out var hit, .15f) || hit.normal.y < .7f || Mathf.Abs(hit.point.y-(WatchCoaster.ZTop-WatchCoaster.Pitch*8/360)) > .02f)
                    throw new Exception("Helix entry floor/normal mismatch");
                ball = Object.Instantiate(d.ballPrefab);
                var rb = BallUtil.Prepare(ball);
                Physics.simulationMode = SimulationMode.Script;
                rb.position = ball.transform.position = c.transform.TransformPoint(WatchCoaster.OutletLocal);
                rb.rotation = BallSkinViewer.DefaultPose;
                rb.linearDamping = c.damping;
                rb.linearVelocity = c.transform.rotation * WatchCoaster.Tangent(8)*c.entrySpeed;
                Physics.SyncTransforms();
                float inTray = 0, seconds = 0;
                var tray = c.transform.TransformPoint(WatchCoaster.TrayLocal);
                for (; seconds < c.timeout && inTray < c.settle; seconds += Time.fixedDeltaTime)
                {
                    Physics.Simulate(Time.fixedDeltaTime);
                    var offset = rb.position-tray; offset.y=0;
                    inTray = offset.magnitude < .3f && rb.position.y < tray.y+.2f ? inTray+Time.fixedDeltaTime : 0;
                    if (rb.position.y < -.3f) throw new Exception("Ball escaped cabinet: " + rb.position);
                }
                if (inTray < c.settle) throw new Exception("Ball failed to reach tray: " + rb.position);
                Debug.Log($"[GumballCheck] PASS: outlet to tray {seconds:F2}s; static triangles={triangles}, fill ball={c.fillMesh.triangles.Length/3}; entry y={hit.point.y:F3}, normal={hit.normal}");
            }
            finally
            {
                Physics.simulationMode = previousMode;
                if (ball) Object.DestroyImmediate(ball.gameObject);
                d.tower.gameObject.SetActive(towerActive); c.gameObject.SetActive(coasterActive);
            }
        }

        [MenuItem("Tools/NTsLoto/Capture Gumball Preview")]
        public static void Capture()
        {
            if (!EditorApplication.isPlaying) throw new InvalidOperationException("Play WatchScene first");
            var d = Object.FindFirstObjectByType<WatchDirector>();
            if (WatchDirector.Current != WatchDirector.Machine.Coaster) throw new InvalidOperationException("Select Gumball first");
            var cam=d.cam; var pos=cam.transform.position; var rot=cam.transform.rotation; var fov=cam.fieldOfView;
            try
            {
                cam.transform.position=d.coaster.transform.TransformPoint(new Vector3(2.9f,2.6f,-4.8f));
                cam.transform.LookAt(d.coaster.transform.TransformPoint(new Vector3(0,1.48f,0))); cam.fieldOfView=38;
                LotoCapture.Shot(cam,"preview_gumball.png",960,960);
            }
            finally { cam.transform.SetPositionAndRotation(pos,rot); cam.fieldOfView=fov; }
        }
    }
}
