# Unity DOTS / ECS 入门教学

> 基于 Unity 2022 + com.unity.entities 1.3.x
> 配套示例工程：`Assets/ECSDemo/`

---

## 一、为什么要用 ECS？

传统 MonoBehaviour 的性能瓶颈来自两点：

1. **内存分散**：每个 GameObject 的数据散落在堆内存各处，CPU 读取时缓存命中率很低
2. **虚函数开销**：`Update()` 通过虚函数调度，大量对象时开销积累明显

ECS 的解法是**数据驱动**：把同类型的数据连续排列在内存中（Chunk），CPU 批量读取时缓存命中率极高，配合 Burst 编译器和 Job System，可以轻松处理**十万级别的对象**。

---

## 二、ECS 三个字母

| 字母 | 名称 | 职责 | 类比 |
|------|------|------|------|
| **E** | Entity | 纯 ID，没有数据没有行为 | 数据库的主键 |
| **C** | Component | 纯数据，没有方法 | 数据库的一行记录 |
| **S** | System | 纯逻辑，查询并处理 Entity | 数据库的查询操作 |

### Entity 长什么样

```csharp
// Entity 源码（极度简单）
public struct Entity
{
    public int Index;   // 唯一 ID
    public int Version; // 版本号，用于检测已销毁的 Entity
}
```

Entity **自身不存任何数据**，数据全部在 Component 里。

### 内存布局对比

```
传统 OOP（MonoBehaviour）       ECS（Component）
────────────────────────        ──────────────────────────────────
对象A：[位置][旋转][血量]...      Chunk（RotationSpeed 类型）：
对象B：[位置][旋转][血量]...      [speed0][speed1][speed2][speed3]...
对象C：[位置][旋转][血量]...                  ↑ 内存连续，缓存友好
散落在堆内存各处
```

---

## 三、项目文件结构

```
Assets/ECSDemo/
├── Components/                 ← "C"：纯数据
│   ├── RotationSpeedComponent.cs
│   ├── MoveVelocityComponent.cs
│   └── SpawnerComponent.cs
├── Authoring/                  ← 编辑器工作流（不是 ECS 本体）
│   ├── RotatorAuthoring.cs
│   └── SpawnerAuthoring.cs
└── Systems/                    ← "S"：纯逻辑
    ├── SpawnerSystem.cs
    ├── RotationSystem.cs
    └── MoveSystem.cs
```

---

## 四、Component：只存数据

### 示例代码

```csharp
// RotationSpeedComponent.cs
public struct RotationSpeedComponent : IComponentData
{
    public float RadiansPerSecond;
}

// MoveVelocityComponent.cs
public struct MoveVelocityComponent : IComponentData
{
    public float3 Value; // xyz 各轴速度
}
```

### 核心规则

- 实现 `IComponentData` 接口
- **只有字段，没有方法**
- 必须是 `unmanaged struct`（不能有 `string`、`List<>` 等引用类型）
- `float3` 来自 `Unity.Mathematics`，是 SIMD 友好的向量类型，比 `Vector3` 更快

### 和传统 OOP 的对比

```
传统 OOP                         ECS
────────────────────             ──────────────────────
class Enemy : MonoBehaviour      struct HealthComponent   → 只存 HP 值
{                                struct SpeedComponent    → 只存速度值
    float hp;                    struct PositionTag       → 标签（无字段）
    float speed;
    void TakeDamage() {...}      // 逻辑全在 System 里
    void Move() {...}
}
```

---

## 五、Authoring / Baker：连接编辑器与 ECS

### 为什么需要 Authoring？

ECS Entity 没有 Inspector，无法在编辑器里直接配置。
Authoring 是一个普通 MonoBehaviour，专门用来**在编辑器里填写参数**，运行时由 Baker 转换为真正的 Entity。

### 工作流程

```
【编辑器阶段】                     【进入 Play 模式】            【运行时】

GameObject                         Baker 烘焙                  纯 ECS 世界
 + RotatorAuthoring     ─────→    Entity #42                  被 System 处理
   DegreesPerSecond=90             + RotationSpeedComponent
                                   + MoveVelocityComponent
                                   + LocalTransform
```

**Authoring 进入 Play 模式后就消失了**，运行时只有 Entity + Component，没有 MonoBehaviour 的任何开销。

### 示例代码

```csharp
// RotatorAuthoring.cs
[DisallowMultipleComponent]
public class RotatorAuthoring : MonoBehaviour
{
    public float DegreesPerSecond = 90f;

    // Baker 写成内部类，泛型参数是对应的 Authoring 类型
    class RotatorBaker : Baker<RotatorAuthoring>
    {
        public override void Bake(RotatorAuthoring authoring)
        {
            // 获取当前 GameObject 对应的 Entity
            // Dynamic：告诉 ECS 这个 Entity 的 Transform 会变化
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // 添加组件并初始化数据
            AddComponent(entity, new RotationSpeedComponent
            {
                RadiansPerSecond = math.radians(authoring.DegreesPerSecond)
            });

            AddComponent(entity, new MoveVelocityComponent());
        }
    }
}
```

### TransformUsageFlags 说明

| 标志 | 含义 |
|------|------|
| `Dynamic` | Transform 每帧可能变化，保留完整 LocalTransform 组件 |
| `Renderable` | 只用于渲染，不需要动态更新 |
| `None` | 不需要 Transform（纯数据 Entity） |

---

## 六、System：主线程 foreach 写法

```csharp
// RotationSystem.cs
[BurstCompile]
public partial struct RotationSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // 场景中没有 RotationSpeedComponent 时，本系统跳过，避免空循环
        state.RequireForUpdate<RotationSpeedComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;

        // Query：查询同时拥有这两个组件的所有 Entity
        // RefRW = 读写引用，RefRO = 只读引用
        foreach (var (transform, speed) in
            SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeedComponent>>())
        {
            transform.ValueRW = transform.ValueRO.RotateY(
                speed.ValueRO.RadiansPerSecond * deltaTime);
        }
    }
}
```

### 关键概念

**`ISystem` vs `SystemBase`**

| | ISystem | SystemBase |
|-|---------|-----------|
| 类型 | struct | class |
| Burst 支持 | ✅ 完整支持 | ❌ 不支持 |
| 内存分配 | 零堆分配 | 有 GC 压力 |
| 推荐程度 | ★★★ 推荐 | 旧版，不推荐新代码使用 |

**`partial` 关键字**：Unity Source Generator 分析 `Query` 参数后，自动生成 EntityQuery 代码，不需要手写。

**`[BurstCompile]`**：Burst 编译器把 C# 编译为接近 C++ 的原生机器码。要求代码是"Burst 兼容"的——不能用托管类型（string、List 等），不能调用虚函数。

---

## 七、System：IJobEntity 并行 Job 写法

主线程 foreach 是单线程的。当 Entity 数量达到数千~数万时，需要用 **IJobEntity** 把工作分发到多个 CPU 核心并行处理。

```csharp
// MoveSystem.cs

[BurstCompile]
public partial struct MoveSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 创建 Job，传入不依赖 Entity 的外部数据
        new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime }.ScheduleParallel();
        // ScheduleParallel → 多线程并行
        // Schedule         → 单线程 Job
        // Run              → 主线程立即执行（等同于 foreach，但走 Job 接口）
    }
}

[BurstCompile]
public partial struct MoveJob : IJobEntity
{
    public float DeltaTime;

    // Execute 参数决定查询条件：
    // ref = 读写，in = 只读
    public void Execute(ref LocalTransform transform, in MoveVelocityComponent velocity)
    {
        var pos = transform.Position + velocity.Value * DeltaTime;

        // math.select(a, b, cond)：逐分量选择，cond 为 true 返回 b，否则返回 a
        // 比 if/else 更 SIMD 友好，Burst 可编译为向量指令
        const float bound = 12f;
        pos = math.select(pos - 2f * bound, pos, pos < bound);
        pos = math.select(pos + 2f * bound, pos, pos > -bound);

        transform.Position = pos;
    }
}
```

### 并行安全性

ECS Job System 通过 **JobHandle 依赖链**自动保证线程安全：

```
SpawnerSystem ──写→ SpawnerComponent
                         ↓ 依赖
RotationSystem ──读→ RotationSpeedComponent
MoveSystem     ──写→ LocalTransform
                         ↑
                   ECS 自动分析依赖，确保顺序正确
                   不需要手写任何锁
```

---

## 八、EntityCommandBuffer：结构性变更的正确姿势

### 为什么不能直接创建/销毁 Entity？

在遍历 Query 的同时创建 Entity，会破坏底层 Chunk 内存结构导致崩溃。

### 解决方案：先录制，后回放

```csharp
// SpawnerSystem.cs（核心片段）

var ecb = new EntityCommandBuffer(Allocator.Temp);

foreach (var (spawner, spawnerEntity) in
    SystemAPI.Query<RefRO<SpawnerComponent>>().WithEntityAccess())
{
    for (int i = 0; i < spawner.ValueRO.Count; i++)
    {
        // ecb.Instantiate 返回"延迟 Entity"（占位符 ID）
        // 这个 Entity 在 Playback 之前不真实存在
        var newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);

        // 可以对延迟 Entity 设置组件，Playback 时正确应用
        ecb.SetComponent(newEntity, LocalTransform.FromPosition(pos));
        ecb.SetComponent(newEntity, new MoveVelocityComponent { Value = vel });
    }

    // 生成完毕，销毁自身，避免每帧重复生成
    ecb.DestroyEntity(spawnerEntity);
}

// 统一执行所有录制的命令
ecb.Playback(state.EntityManager);
ecb.Dispose();
```

### ECB 常用操作

| 操作 | 说明 |
|------|------|
| `ecb.Instantiate(prefab)` | 实例化 Prefab，返回延迟 Entity |
| `ecb.CreateEntity()` | 创建空 Entity |
| `ecb.DestroyEntity(e)` | 销毁 Entity |
| `ecb.AddComponent<T>(e, data)` | 添加组件 |
| `ecb.SetComponent<T>(e, data)` | 设置组件值 |
| `ecb.RemoveComponent<T>(e)` | 删除组件 |
| `ecb.Playback(em)` | 执行所有命令 |

---

## 九、编辑器配置步骤

### 第一步：创建 ECS Prefab

1. 新建一个 **Cube** GameObject
2. 挂上 `RotatorAuthoring` 脚本，设置 `DegreesPerSecond`
3. 拖到 Project 窗口，创建为 **Prefab**

### 第二步：创建 SubScene

> DOTS 的必要容器。普通场景的 GameObject 不会被烘焙为 ECS Entity。

1. Hierarchy 右键 → **New Sub Scene** → **Empty Sub Scene**
2. 双击进入 SubScene 编辑模式

### 第三步：配置 Spawner

1. 在 SubScene 内新建空 GameObject
2. 挂上 `SpawnerAuthoring` 脚本
3. 将第一步的 Prefab 拖到 `Prefab` 字段
4. `Count` 设为 **1000**（或更大，感受 ECS 性能）
5. `SpawnRange` 设为 **10**

### 第四步：运行

点击 Play，应看到 1000 个旋转方块在空间中飞行，碰到边界从对面穿出。

---

## 十、调试工具

Unity 提供了专门的 ECS 调试窗口：

| 窗口 | 路径 | 用途 |
|------|------|------|
| Entities Hierarchy | Window → Entities → Hierarchy | 查看所有 Entity 和它们的 Component |
| Systems | Window → Entities → Systems | 查看所有 System 的执行耗时 |
| Archetypes | Window → Entities → Archetypes | 查看 Chunk 内存布局 |

---

## 十一、核心 API 速查

```csharp
// ── 查询 ──────────────────────────────────────────────────
SystemAPI.Query<RefRW<A>, RefRO<B>>()          // 主线程 foreach
SystemAPI.Query<...>().WithEntityAccess()       // 同时获取 Entity
SystemAPI.Query<...>().WithNone<T>()            // 排除含 T 组件的 Entity
SystemAPI.Query<...>().WithAny<A, B>()          // 含 A 或 B 任意之一

// ── System 生命周期 ────────────────────────────────────────
state.RequireForUpdate<T>()                     // 没有 T 时跳过本系统
SystemAPI.Time.DeltaTime                        // 帧间隔时间
SystemAPI.Time.ElapsedTime                      // 累计运行时间

// ── Baker（Authoring 内部类）──────────────────────────────
GetEntity(TransformUsageFlags.Dynamic)          // 获取当前 GO 的 Entity
GetEntity(go, TransformUsageFlags.Dynamic)      // 获取引用 GO 的 Entity
AddComponent(entity, data)                      // 添加组件

// ── 内存分配器 ────────────────────────────────────────────
Allocator.Temp                                  // 当前帧内有效，最快
Allocator.TempJob                               // Job 内有效（最多4帧）
Allocator.Persistent                            // 手动释放，长期存在
```

---

## 十二、概念速记

```
ECS = Entity（ID） + Component（数据） + System（逻辑）

Authoring  = 编辑器里配置数据的 MonoBehaviour（运行时消失）
Baker      = 把 Authoring → Entity + Component 的转换器
SubScene   = Authoring/Baker 的运行容器

ISystem    = struct 系统，支持 Burst，推荐
IJobEntity = 并行 Job，多核处理海量 Entity
ECB        = EntityCommandBuffer，延迟执行结构性变更
Burst      = 把 C# 编译为原生代码的高性能编译器
Chunk      = 同类型 Component 的连续内存块，缓存友好
```
