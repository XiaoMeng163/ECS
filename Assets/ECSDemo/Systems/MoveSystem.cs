using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSDemo
{
    // ============================================================
    // ECS 核心概念 4: IJobEntity —— 多线程并行 Job
    // ============================================================
    //
    // 对比 RotationSystem（主线程 foreach）：
    //   主线程 foreach → 简单，单核，适合逻辑复杂/数量少的情况
    //   IJobEntity     → 并行，多核，适合数量多（数千~数万）的情况
    //
    // Job 系统原理：
    //   1. 主线程调度（Schedule）Job，把工作分成多个批次
    //   2. Job 线程池并行处理各批次 Entity
    //   3. ECS 的依赖系统（JobHandle）保证数据安全，不需要手写锁
    // ============================================================
    [BurstCompile]
    public partial struct MoveSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<MoveVelocityComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 创建 Job 结构体，传入不依赖 Entity 的外部数据（deltaTime）
            var job = new MoveJob { DeltaTime = SystemAPI.Time.DeltaTime };

            // ScheduleParallel：把 Job 分发到线程池并行执行
            // （如果改成 Schedule() 则单线程执行；Run() 则在主线程立即执行）
            job.ScheduleParallel();

            // 注意：ScheduleParallel 后不需要手动 Complete()，
            // ECS 会在下一个需要同步点（如另一个访问相同数据的系统）自动处理依赖
        }
    }

    // ============================================================
    // IJobEntity：定义"对每个匹配 Entity 执行什么操作"
    //
    // partial struct：Source Generator 根据 Execute 参数自动生成查询代码
    // Execute 参数决定查询条件：同时拥有 LocalTransform 和 MoveVelocityComponent
    //   ref  → 读写（可修改）
    //   in   → 只读（性能更好）
    // ============================================================
    [BurstCompile]
    public partial struct MoveJob : IJobEntity
    {
        public float DeltaTime;

        public void Execute(ref LocalTransform transform, in MoveVelocityComponent velocity)
        {
            var pos = transform.Position + velocity.Value * DeltaTime;

            // 边界环绕：超出 ±bound 范围时从对面穿出，形成无限空间感
            // math.select(a, b, cond)：逐分量选择，cond 为 true 时返回 b，否则返回 a
            // 这比 if/else 更 SIMD 友好，Burst 能编译为向量指令
            const float bound = 12f;
            pos = math.select(pos - 2f * bound, pos, pos < bound);  // 超上界 → 移到下界
            pos = math.select(pos + 2f * bound, pos, pos > -bound); // 超下界 → 移到上界

            transform.Position = pos;
        }
    }
}
