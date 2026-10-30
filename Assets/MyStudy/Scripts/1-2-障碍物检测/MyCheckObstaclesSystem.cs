using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;

public partial struct MyCheckObstaclesSystem : ISystem
{
    /// <summary> 是否已经扫描过 </summary>
    private bool haveScanned;

    /// <summary> 缓存：按 Entity 快速查找 ASObstacle / ASPenalty 组件, 本质上可以理解为一个查询工具 </summary>
    private ComponentLookup<MyASObstacle> obstacleLookup;

    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<MyASGrid>();
        state.RequireForUpdate<PhysicsWorldSingleton>();

        // ComponentLookup 缓存：按 Entity 快速查找 ASObstacle / ASPenalty 组件
        // 比 EntityManager.GetComponentData 更高效，内部使用 TypeIndex 直接寻址
        obstacleLookup = state.GetComponentLookup<MyASObstacle>(true);

        haveScanned = false;
    }

    public void OnUpdate(ref SystemState state)
    {
        if (haveScanned)
            return;
        
        // 让 obstacleLookup 更新到当前这一帧的 ECS 世界状态，这样它才能正确地通过 Entity 查找最新的 MyASObstacle
        obstacleLookup.Update(ref state);


        PhysicsWorld physicsWorld = SystemAPI.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
    
        // 遍历网格 Entity（通常只有一个，但框架支持多个网格共存）
        foreach (var (gridRef, transformRef, entity) in SystemAPI.Query<RefRO<MyASGrid>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            MyASGrid myASGrid = gridRef.ValueRO;
            LocalTransform localTransform = transformRef.ValueRO;

            // MyDrawGizmos 里也有一个 entityManager.GetBuffer
            // SystemAPI.Query 适合 System
            // World → EntityManager 常用于普通 MonoBehaviour 与 ECS 数据交互。
            DynamicBuffer<MyASGridNodeBuffer> nodeBuffers = SystemAPI.GetBuffer<MyASGridNodeBuffer>(entity);

            CheckGrid(myASGrid, localTransform, nodeBuffers, physicsWorld);
        }

        haveScanned = true;
    }

    private void CheckGrid(MyASGrid myASGrid, LocalTransform localTransform, DynamicBuffer<MyASGridNodeBuffer> nodeBuffers, PhysicsWorld physicsWorld)
    {
        //创建碰撞结果和过滤规则
        // DistanceHit 是碰撞结果 hits：保存检测命中的碰撞体
        NativeList<DistanceHit> hits = new NativeList<DistanceHit>(Allocator.Temp);
        try
        { 
            // 从Buffer里取出节点
            for(int i = 0; i < nodeBuffers.Length; i++)
            {
                //清空上一次检测的结果
                hits.Clear();

                // 结构体，此时取出的是副本，修改完成后还要写回。
                MyASGridNodeBuffer element = nodeBuffers[i];
                MyASNode node = element.Node;

                // 计算检测位置和范围, worldPosition是格子中心
                float3 worldPosition = MyGridUtility.GridToWorld(node.X, node.Y, myASGrid, localTransform);
                // halfExtents 是检测盒的半尺寸
                float3 halfExtents = new float3(myASGrid.NodeSize * 0.45f, myASGrid.NodeSize * 0.5f, myASGrid.NodeSize * 0.45f);
                
                CollisionFilter filter = new CollisionFilter
                {
                    BelongsTo = ~0u,
                    // CollidesWith：只检测 Grid 配置的障碍物 Layer
                    CollidesWith = (uint)myASGrid.InaccessibleLayerMask,
                    GroupIndex = 0
                };

                // 执行OverlapBox
                // 这相当于在每个网格中心放一个检测盒，查看它与哪些碰撞体重叠。
                physicsWorld.OverlapBox(worldPosition, quaternion.identity, halfExtents, ref hits, filter);

                // 确认命中的是障碍物Entity
                bool blocked = false;
                for (int hitIndex = 0; hitIndex < hits.Length; hitIndex++)
                {
                    Entity hitEntity = hits[hitIndex].Entity;

                    // 两层筛选
                    // CollisionFilter → 筛选障碍物 Layer
                    // ComponentLookup → 确认 Entity 拥有 MyASObstacle
                    if(obstacleLookup.HasComponent(hitEntity))
                    {
                        blocked = true;
                        break;
                    }
                }

                // 修改并写回 Buffer
                node.IsAccessible = !blocked;
                element.Node = node;
                nodeBuffers[i] = element; 
            }
        }
        finally
        {
            hits.Dispose();
        }
    }

}
