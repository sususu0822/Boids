using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BoidManager : MonoBehaviour {

    // 需與 BoidCompute.compute 裡的 numthreads 數量一致。
    const int threadGroupSize = 1024;

    public BoidSettings settings;
    public ComputeShader compute;
    Boid[] boids;

    void Start () {
        // 場景開始時收集所有 boid，並套用共用設定。
        boids = FindObjectsOfType<Boid> ();
        foreach (Boid b in boids) {
            b.Initialize (settings, null);
        }

    }

    void Update () {
        if (boids != null) {

            int numBoids = boids.Length;
            var boidData = new BoidData[numBoids];

            // 將每隻 boid 的位置與朝向打包，準備送到 GPU 計算鄰居資訊。
            for (int i = 0; i < boids.Length; i++) {
                boidData[i].position = boids[i].position;
                boidData[i].direction = boids[i].forward;
            }

            // ComputeBuffer 的 stride 必須和 BoidData.Size 對齊，否則 shader 會讀錯欄位。
            var boidBuffer = new ComputeBuffer (numBoids, BoidData.Size);
            boidBuffer.SetData (boidData);

            // 傳入本幀 boid 資料與感知半徑，讓 compute shader 平行累加鄰居統計。
            compute.SetBuffer (0, "boids", boidBuffer);
            compute.SetInt ("numBoids", boids.Length);
            compute.SetFloat ("viewRadius", settings.perceptionRadius);
            compute.SetFloat ("avoidRadius", settings.avoidanceRadius);

            // 根據 boid 數量計算需要派發幾組 thread group。
            int threadGroups = Mathf.CeilToInt (numBoids / (float) threadGroupSize);
            compute.Dispatch (0, threadGroups, 1, 1);

            // 從 GPU 取回每隻 boid 的平均方向、中心點、分離方向與鄰居數。
            boidBuffer.GetData (boidData);

            for (int i = 0; i < boids.Length; i++) {
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
