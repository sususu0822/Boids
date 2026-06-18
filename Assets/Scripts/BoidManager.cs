using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour {

    // 需與 BoidCompute.compute 裡的 numthreads 數量一致。
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader compute;
    public Transform endPoint;
    public float arriveDistance = .5f;
    List<Boid> boids;

    void Awake () {
        boids = new List<Boid> ();
    }

    void Start () {
        // 場景開始時收集既有 boid，動態生成的 boid 則由 Spawner 呼叫 RegisterBoid。
        Boid[] sceneBoids = FindObjectsOfType<Boid> ();
        foreach (Boid b in sceneBoids) {
            RegisterBoid (b);
        }

    }

    public void RegisterBoid (Boid boid) {
        if (boid == null) {
            return;
        }

        if (boids == null) {
            boids = new List<Boid> ();
        }

        if (boids.Contains (boid)) {
            return;
        }

        // 新生成的 boid 必須初始化並加入清單，否則不會被 Update 推進。
        boid.Initialize (settings, endPoint, arriveDistance);
        boids.Add (boid);
    }

    void Update () {
        if (boids != null) {
            // Destroy 後的 boid 會在下一幀變成 null，先清掉再送進 GPU。
            for (int i = boids.Count - 1; i >= 0; i--) {
                if (boids[i] == null) {
                    boids.RemoveAt (i);
                }
            }

            if (boids.Count == 0) {
                return;
            }

            int numBoids = boids.Count;
            var boidData = new BoidData[numBoids];

            // 將每隻 boid 的位置與朝向打包，準備送到 GPU 計算鄰居資訊。
            for (int i = 0; i < boids.Count; i++) {
                boidData[i].position = boids[i].position;
                boidData[i].direction = boids[i].forward;
            }

            // ComputeBuffer 的 stride 必須和 BoidData.Size 對齊，否則 shader 會讀錯欄位。
            var boidBuffer = new ComputeBuffer (numBoids, BoidData.Size);
            boidBuffer.SetData (boidData);

            // 傳入本幀 boid 資料與感知半徑，讓 compute shader 平行累加鄰居統計。
            compute.SetBuffer (0, "boids", boidBuffer);
            compute.SetInt ("numBoids", boids.Count);
            compute.SetFloat ("viewRadius", settings.perceptionRadius);
            compute.SetFloat ("avoidRadius", settings.avoidanceRadius);

            // 根據 boid 數量計算需要派發幾組 thread group。
            int threadGroups = Mathf.CeilToInt (numBoids / (float) threadGroupSize);
            compute.Dispatch (0, threadGroups, 1, 1);

            // 從 GPU 取回每隻 boid 的平均方向、中心點、分離方向與鄰居數。
            boidBuffer.GetData (boidData);

            for (int i = 0; i < boids.Count; i++) {
                boids[i].avgFlockHeading = boidData[i].flockHeading;
                boids[i].centreOfFlockmates = boidData[i].flockCentre;
                boids[i].avgAvoidanceHeading = boidData[i].avoidanceHeading;
                boids[i].numPerceivedFlockmates = boidData[i].numFlockmates;

                boids[i].UpdateBoid ();
            }

            // 每幀建立的 ComputeBuffer 必須釋放，避免 GPU 記憶體累積。
            boidBuffer.Release ();
        }
    }

    void OnDrawGizmos () {
        if (endPoint == null) {
            return;
        }

        // 顯示終點抵達判定範圍，半徑與 arriveDistance 相同。
        Gizmos.color = new Color (0f, 1f, 0.2f, 0.25f);
        Gizmos.DrawSphere (endPoint.position, arriveDistance);
        Gizmos.color = new Color (0f, 1f, 0.2f, 1f);
        Gizmos.DrawWireSphere (endPoint.position, arriveDistance);
    }

    // 與 BoidCompute.compute 中的 Boid struct 對應，用來在 CPU/GPU 間傳遞資料。
    public struct BoidData {
        public Vector3 position;
        public Vector3 direction;

        public Vector3 flockHeading;
        public Vector3 flockCentre;
        public Vector3 avoidanceHeading;
        public int numFlockmates;

        public static int Size {
            get {
                return sizeof (float) * 3 * 5 + sizeof (int);
            }
        }
    }
}
