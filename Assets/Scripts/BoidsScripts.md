# Boids Scripts Overview

本文件說明專案中與 Boids（群鳥）相關的所有腳本與 Compute Shader 的用途、主要欄位與行為，方便維護與快速上手。

---

## 檔案清單
- `Boid.cs` — 單一 Boid（個體）行為實作。
- `BoidCompute.compute` — Compute Shader，用於並行計算每隻 Boid 的感知（鄰居資訊）。
- `BoidHelper.cs` — 靜態輔助類，生成檢測方向向量陣列（避障用）。
- `BoidManager.cs` — 管理器，準備資料給 Compute Shader，並把結果回寫給每個 `Boid`，再呼叫更新。
- `BoidSettings.cs` — ScriptableObject，儲存所有可調參數（速度、感知半徑、權重、碰撞設定等）。
- `Spawner.cs` — 負責在場景中生成指定數量的 `Boid` 預置物，並設定顏色與初始位置/方向。

---

## Boid.cs
主要責任：代表單一 boid 的狀態與運動更新。

關鍵欄位：
- `BoidSettings settings`：引用設定物件。
- `Vector3 position, forward`：boid 的位置與朝向（公開以供管理器讀寫）。
- `avgFlockHeading, avgAvoidanceHeading, centreOfFlockmates, numPerceivedFlockmates`：由 `BoidManager`/Compute 端填入的感知結果。

主要方法：
- `Initialize(BoidSettings settings, Transform target)`：初始化位置、速度與目標（若有）。
- `SetColour(Color col)`：設定材質顏色，用於群組視覺化。
- `UpdateBoid()`：依據目標、隊形（對齊、凝聚、分離）與避障計算加速度、速度，並更新 transform。

避障：
- `IsHeadingForCollision()` 使用 `Physics.SphereCast` 檢測前方碰撞。
- `ObstacleRays()` 使用 `BoidHelper.directions`（世界空間方向）做多方向射線測試，選出一個可避開障礙的方向。

Steering：
- `SteerTowards(Vector3 vector)` 計算追趕向量並受 `maxSteerForce` 限制。

---

## BoidCompute.compute
主要責任：在 GPU 上並行計算每隻 boid 的鄰居資訊，降低 CPU 開銷。

重要資源與參數：
- `RWStructuredBuffer<Boid> boids`：讀寫的 Boid 結構陣列（position, direction 與累加的 flock 資料）。
- `int numBoids`、`float viewRadius`、`float avoidRadius`：由 `BoidManager` 傳入。
- Kernel：`CSMain`，以 `threadGroupSize = 1024` 啟動。

演算法大綱：
- 每個執行緒（對應一隻 boid）會遍歷所有其他 boid（簡單 O(N^2) 檢查），如果在 `viewRadius` 內則累加隊伍朝向與中心；若小於 `avoidRadius` 則累加分離向量。
- 結果直接寫回 `boids[id].flockHeading`、`flockCentre`、`separationHeading`、`numFlockmates`，之後由 `BoidManager` 取回並賦值給 `Boid`。

注意：目前的 Compute Shader 在資料存取上是原地累加，需注意在高並行度與同步上的限制，但 Unity 的 `RWStructuredBuffer` 與此用法在小型 demo 中是可用的。

---

## BoidHelper.cs
主要責任：產生一組均勻分佈在球面上的方向向量，用於避障射線偵測。

實作要點：
- 使用黃金角（golden ratio / Fibonacci sphere）方法生成 `numViewDirections = 300` 個方向，存於靜態 `directions` 陣列。
- `Boid` 使用這些向量（經 `cachedTransform.TransformDirection` 轉為世界空間）嘗試找到沒有碰撞的方向。

---

## BoidManager.cs
主要責任：在每一個 `Update()` 週期中，收集所有 `Boid` 的位置/方向資料，傳到 Compute Shader，接收計算結果，並把感知結果回填到各 `Boid`，最後呼叫 `Boid.UpdateBoid()` 做動態更新。

重要欄位：
- `BoidSettings settings`：設定。
- `ComputeShader compute`：指向 `BoidCompute.compute` 的 ComputeShader 資源。

關鍵流程：
1. 取得所有 `Boid`（`FindObjectsOfType<Boid>()`）並在 Start 中初始化。
2. 每幀建立 `BoidData[]`（包含 position, direction），並上傳到 `ComputeBuffer`。
3. 設定 shader 參數（`numBoids`, `viewRadius`, `avoidRadius`），計算 `threadGroups` 並呼叫 `Dispatch`。
4. `boidBuffer.GetData()` 回取計算結果，將 flockHeading、flockCentre、avoidanceHeading、numFlockmates 寫回對應的 `Boid` 實例。
5. 呼叫 `Boid.UpdateBoid()` 完成位置更新，最後釋放 buffer。

注意：
- `BoidData.Size` 必須與 Compute Shader 中 `Boid` 結構的記憶體排列與大小一致（目前為 5 個 Vector3 + 1 int），在 `BoidManager` 已按 float 數量計算 size。

---

## BoidSettings.cs
主要責任：以 `ScriptableObject` 的方式集中管理所有可調參數，於 Inspector 中可視化調整：

參數範例：
- 速度：`minSpeed`, `maxSpeed`。
- 感知：`perceptionRadius`, `avoidanceRadius`。
- 操控：`maxSteerForce`。
- 權重：`alignWeight`, `cohesionWeight`, `seperateWeight`, `targetWeight`。
- 碰撞：`obstacleMask`, `boundsRadius`, `avoidCollisionWeight`, `collisionAvoidDst`。

調整建議：在運行時調整這些值來觀察群體行為的變化，或建立多組 `BoidSettings` 以便在場景中快速替換。

---

## Spawner.cs
主要責任：在場景開始時自動生成指定數量的 `Boid`，設定初始位置、朝向與顏色。

重要欄位：
- `Boid prefab`：要實例化的 `Boid` 預置體。
- `spawnRadius`, `spawnCount`：生成範圍與數量。
- `colour`：設定每隻 boid 的材質顏色。
- `GizmoType showSpawnRegion`：在 Editor 中顯示生成球體的選項。

實作要點：
- 在 `Awake()` 內使用 `Instantiate(prefab)` 並隨機位置（`Random.insideUnitSphere * spawnRadius`）與方向，然後 `SetColour(colour)`。
- 提供 `OnDrawGizmos` / `OnDrawGizmosSelected` 以在 Editor 中顯示生成範圍。

---

## 使用建議與注意事項
- 若想用 GPU 計算量更大或更複雜的邏輯，可把更多行為（例如障礙檢測或鄰居篩選）移到 Compute Shader，但請注意資料同步與原子操作。
- `BoidManager` 的 `FindObjectsOfType<Boid>()` 在大型場景可能較慢，建議改為集中註冊（在 `Spawner` 建立時記錄）以提升效能。
- 確認 `BoidCompute.compute` 與 `BoidManager.BoidData` 的記憶體排列一致，避免資料錯配。

---

若要我把這份 md 移到不同路徑或用英文再產生一份，或把內容擴充為每個方法的逐行註解，告訴我下一步。