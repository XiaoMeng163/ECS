using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
//using UnityEngine;

// Authoring 配置
//     ↓
// Baker 烘焙 Prefab 和 Spawner Entity
//     ↓
// SpawnerSystem 实例化并覆盖随机数据
//     ↓
// ECB Playback 真正执行结构变化
//     ↓
// RotationSystem 更新旋转
// MoveSystem 更新位置
//     ↓
// 每帧重复移动和旋转

[BurstCompile]
public partial struct MySpawnerSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MySpawnerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        //根据时间戳生成随机种子，保证每次运行结果不同
        uint seed = (uint)math.max(1, (int)SystemAPI.Time.ElapsedTime * 1000 + 1);

        var random = Random.CreateFromIndex(seed);

        // WithEntityAccess()：查询结果中同时返回 Entity 和组件数据。
        foreach (var (spawner, entity) in SystemAPI.Query<MySpawnerComponent>().WithEntityAccess())
        {
            float r = spawner.spawnRange;

            for (int i = 0; i < spawner.count; i++)
            {
                Entity ecbEntity = ecb.Instantiate(spawner.prefab);

                float3 pos = new float3(
                    random.NextFloat(-r, r),
                    random.NextFloat(-r, r),
                    random.NextFloat(-r, r)
                );

                //EntityManager.SetComponentData 是“现在立刻改”，
                //ecb.SetComponent 是“先记下来，之后统一改”。
                ecb.SetComponent(ecbEntity, LocalTransform.FromPosition(pos));

                //给新生成的 Entity 添加移动速度组件和旋转速度组件
                ecb.SetComponent(ecbEntity, new MyMoveVelocityComponent
                {
                    value = random.NextFloat3Direction() * random.NextFloat(2f, 6f)
                });

                // ecb.SetComponent(ecbEntity, new MyRotationSpeedCompent
                // {
                //     RadianPerSecond = random.NextFloat(-math.PI, math.PI)
                // });
            }

            ecb.DestroyEntity(entity);
        }

        // Playback：把所有录制的命令一次性执行到 EntityManager
        ecb.Playback(state.EntityManager);

        //// Dispose：释放 ECB 占用的内存（Allocator.Temp 也会自动释放，但显式更清晰）
        ecb.Dispose();
    } 
}

