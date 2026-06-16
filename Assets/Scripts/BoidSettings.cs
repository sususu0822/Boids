using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class BoidSettings : ScriptableObject {
    // 速度與感知範圍設定，控制 boid 移動速度與能看見多遠的鄰居。
    public float minSpeed = 2;
    public float maxSpeed = 5;
    public float perceptionRadius = 2.5f;
    public float avoidanceRadius = 1;
    public float maxSteerForce = 3;

    // Boids 三規則的權重：對齊、聚合、分離。
    public float alignWeight = 1;
    public float cohesionWeight = 1;
    public float seperateWeight = 1;

    // 若 Boid.Initialize 有指定 target，這個權重會控制朝目標移動的強度。
    public float targetWeight = 1;

    [Header ("Collisions")]
    // 避障使用的圖層、半徑、偵測距離與轉向權重。
    public LayerMask obstacleMask;
    public float boundsRadius = .27f;
    public float avoidCollisionWeight = 10;
    public float collisionAvoidDst = 5;

}
