using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECSDemo
{
    // ============================================================
    // 生成系统 —— 演示 EntityCommandBuffer（命令缓冲区）
    // ============================================================
    //
    // 为什么需要 EntityCommandBuffer（ECB）？
    //
    //   "结构性变更"（创建/销毁 Entity、添加/删除 Component）在 ECS 中是危险操作：
    //   如果在遍历 Query 的同时直接创建 Entity，会破坏 Chunk 内存布局导致崩溃。
    //
    //   解决方案：ECB 先把命令"录制"下来，遍历结束后统一"回放"执行。
    //
    // ECB 核心操作：
    //   ecb.Instantiate(prefab)    → 延迟实例化（返回"延迟 Entity"占位符）
    //   ecb.SetComponent(e, data)  → 延迟设置组件（可以对延迟 Entity 操作）
    //   ecb.DestroyEntity(e)       → 延迟销毁
    //   ecb.Playback(em)           → 统一执行所有录制的命令
    // ============================================================
    [BurstCompile]
    public partial struct SpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<SpawnerComponent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Allocator.Temp：最廉价的分配方式，当前帧结束自动释放
            // Allocator.TempJob 给 Job 使用 可以存在更长时间
            // Allocator.Persistent 可以跨帧长期存在 需要手动释放
            var ecb = new EntityCommandBuffer(Allocator.Temp);

            // Unity.Mathematics.Random：Burst 兼容的随机数（值类型，非 System.Random）
            // CreateFromIndex 内部会对 index 做哈希，避免低种子值的相关性问题
            uint seed = (uint)math.max(1, (int)(SystemAPI.Time.ElapsedTime * 10000) + 1);   //把当前运行时间变成一个不断变化的整数。根据当前运行时间生成一个不断变化的随机种子，并保证种子至少为 1。
            var random = Random.CreateFromIndex(seed);

            // WithEntityAccess()：在 Query 结果中额外获取 Entity 本身（用于后续销毁）
            foreach (var (spawner, spawnerEntity) in
                SystemAPI.Query<RefRO<SpawnerComponent>>().WithEntityAccess())
            {
                float r = spawner.ValueRO.SpawnRange;

                for (int i = 0; i < spawner.ValueRO.Count; i++)
                {
                    // ★ 关键：ecb.Instantiate 返回"延迟 Entity"
                    //   这个 Entity 在 Playback 之前不真实存在，只是一个占位符 ID
                    //   但可以对它调用 ecb.SetComponent，Playback 时会正确应用
                    var newEntity = ecb.Instantiate(spawner.ValueRO.Prefab);

                    // 设置随机位置（覆盖 Prefab 默认位置）
                    var pos = new float3(
                        random.NextFloat(-r, r),
                        random.NextFloat(-r, r),
                        random.NextFloat(-r, r));

                    ecb.SetComponent(newEntity, LocalTransform.FromPosition(pos));

                    // 设置随机速度（覆盖 Baker 赋的零速度） 
                    //setcomponent 给刚刚生成的 Entity 设置 MoveVelocityComponent 的数据。
                    ecb.SetComponent(newEntity, new MoveVelocityComponent
                    {
                        //NextFloat3Direction返回一个：随机的三维方向向量。
                        Value = random.NextFloat3Direction() * random.NextFloat(2f, 6f)
                    });
                }

                // 生成完毕后销毁 Spawner Entity 自身，避免每帧重复生成
                ecb.DestroyEntity(spawnerEntity);
            }

            // Playback：把所有录制的命令一次性执行到 EntityManager
            ecb.Playback(state.EntityManager);

            // Dispose：释放 ECB 占用的内存（Allocator.Temp 也会自动释放，但显式更清晰）
            ecb.Dispose();
        }
    }
}
