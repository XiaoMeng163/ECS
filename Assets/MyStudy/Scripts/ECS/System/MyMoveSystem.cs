using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

[BurstCompile]
public partial struct MyMoveSystem : ISystem
{
    // RequireForUpdate：场景中必须存在 MyMoveVelocityComponent，
    // 否则本系统每帧跳过，避免空循环开销
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MyMoveVelocityComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var job = new MyMoveJob(){deltaTime = SystemAPI.Time.DeltaTime};

        // ScheduleParallel：把 Job 分发到线程池并行执行
        // 如果改成 Schedule() 则单线程执行；Run() 则在主线程立即执行
        job.ScheduleParallel(); 
    }
}

// IJobEntity：一种专门用来批量处理 ECS Entity 的 Job。你只需要定义“我要处理什么组件”，Unity 就会自动帮你生成查询和遍历代码。
[BurstCompile]
public partial struct MyMoveJob : IJobEntity
{
    public float deltaTime;
    public void Execute(ref LocalTransform transform, in MyMoveVelocityComponent velocity)
    {
        //transform.Position += velocity.Value * UnityEngine.Time.deltaTime;
        float3 pos = transform.Position + velocity.value * deltaTime;

        const float bound = 12f;

        // math.select(a, b, c)：如果 c 为 true，则返回 b，否则返回 a 性能比if/else更好，Burst 能编译为向量指令
        pos = math.select(pos - 2f * bound, pos, pos < bound);  // 超上界 → 移到下界 当 pos >= 12 时，把 pos 减去 24。
        pos = math.select(pos + 2f * bound, pos, pos > -bound); // 超下界 → 移到上界 pos <= -12，超过左边界，把它加 24，送回右边。

        transform.Position = pos;
    }
}
