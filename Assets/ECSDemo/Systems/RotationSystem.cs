using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;

namespace ECSDemo
{
    // ============================================================
    // ECS 核心概念 3: System（系统）—— 最简单的写法：主线程 foreach
    // ============================================================
    //
    // ISystem（struct）vs SystemBase（class）：
    //   - ISystem   → struct，支持 Burst 编译，零堆内存分配，推荐
    //   - SystemBase → class，写起来稍方便，但有 GC 压力，旧 API
    //
    // partial 关键字：
    //   Unity Source Generator 会分析 Execute/foreach 参数，
    //   自动生成 EntityQuery 和迭代代码，不需要手写查询。
    //
    // [BurstCompile]：
    //   Burst 编译器把 C# 编译为高度优化的原生代码（接近 C++ 性能）。
    //   要求：不能使用托管类型（string、List 等），不能调用虚函数。
    // ============================================================
    [BurstCompile]
    public partial struct RotationSystem : ISystem
    {
        // OnCreate：系统首次创建时调用（类似 Start，但在 ECS 世界层面）
        // [BurstCompile] 是 Unity Burst 编译器的标记，用来把一部分 C# 代码编译成高性能的本机机器码。
        // 它主要用于 ECS、Job System、数学计算等大量重复执行的逻辑，例如大量单位移动、寻路、碰撞计算。相比普通 C# 执行，通常会更快
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // RequireForUpdate：场景中必须存在 RotationSpeedComponent，
            // 否则本系统每帧跳过，避免空循环开销
            state.RequireForUpdate<RotationSpeedComponent>();
        }

        // OnUpdate：每帧调用（类似 Update，但不挂在任何 GameObject 上）
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            // SystemAPI.Query<>()：查询同时拥有这些组件的所有 Entity
            // RefRW<T>：读写引用（可修改数据）
            // RefRO<T>：只读引用（不可修改，性能稍好）
            //
            // 这段 foreach 在主线程上单线程执行，简单清晰。
            // 对比 MoveSystem.cs 中的 IJobEntity，那是多线程并行版本。
            foreach (var (transform, speed) in
                SystemAPI.Query<RefRW<LocalTransform>, RefRO<RotationSpeedComponent>>())
            {
                // ValueRW：获取可写的组件值
                // ValueRO：获取只读的组件值
                transform.ValueRW = transform.ValueRO.RotateY(
                    speed.ValueRO.RadiansPerSecond * deltaTime);
                
            }
        }
    }
}
