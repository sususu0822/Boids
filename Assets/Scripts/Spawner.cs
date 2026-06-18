using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Spawner : MonoBehaviour {

    // 控制編輯器 Scene 視窗中是否顯示生成範圍。
    public enum GizmoType { Never, SelectedOnly, Always }

    public Boid prefab;
    public BoidManager boidManager;
    public Transform startPoint;
    public float spawnRadius = 10;
    public int spawnCount = 10;
    public Color colour;
    public GizmoType showSpawnRegion;

    void Awake () {
        // 在球形範圍內隨機生成 boid，並給每隻不同的初始朝向。
        for (int i = 0; i < spawnCount; i++) {
            SpawnBoid ();
        }
    }

    public void InstantiateBoid () {
        // 這個方法目前沒被呼叫，但可以在需要時用來動態生成更多 boid。
        for (int i = 0; i < spawnCount; i++) {
            SpawnBoid ();
        }
    }

    Boid SpawnBoid () {
        Transform spawnOrigin = startPoint != null ? startPoint : transform;
        Vector3 pos = spawnOrigin.position + Random.insideUnitSphere * spawnRadius;
        Boid boid = Instantiate (prefab);
        boid.transform.position = pos;
        boid.transform.forward = Random.onUnitSphere;

        boid.SetColour (colour);
        RegisterWithManager (boid);
        return boid;
    }

    void RegisterWithManager (Boid boid) {
        if (boidManager == null) {
            boidManager = FindObjectOfType<BoidManager> ();
        }

        if (boidManager != null) {
            boidManager.RegisterBoid (boid);
        }
    }

    private void OnDrawGizmos () {
        if (showSpawnRegion == GizmoType.Always) {
            DrawGizmos ();
        }
    }

    void OnDrawGizmosSelected () {
        if (showSpawnRegion == GizmoType.SelectedOnly) {
            DrawGizmos ();
        }
    }

    void DrawGizmos () {

        // 半透明球體只作為編輯器視覺化，不影響實際碰撞或生成。
        Gizmos.color = new Color (colour.r, colour.g, colour.b, 0.3f);
        Transform spawnOrigin = startPoint != null ? startPoint : transform;
        Gizmos.DrawSphere (spawnOrigin.position, spawnRadius);
    }

}
