using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
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
        for(int i = 0; i < nodeBuffers.Length; i++)
        {
            
        }
    }

}
