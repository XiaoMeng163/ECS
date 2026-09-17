using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public class MyDrawGizmos : MonoBehaviour
{
    EntityManager entityManager;

    /// <summary> 可通行网格颜色 </summary>
    [SerializeField]
    Color GridColor = Color.white;

    /// <summary> 方块边框颜色 </summary>
    [SerializeField]
    Color WireColor = Color.black;

    /// <summary> 障碍物颜色 </summary>
    [SerializeField] 
    Color blockedColor = Color.red;

    //DynamicBuffer 不能直接从 GameObject 获取，需要先找到对应的 ECS Entity，再通过 EntityManager.GetBuffer 获取
    void OnDrawGizmos()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        //编辑器初始化时可能ECS世界还不存在
        if(world == null || !world.IsCreated)
        {
            return;
        }

        //获取ECS世界中的EntityManager
        if(entityManager == default)
        entityManager = world.EntityManager;

        //创建一个查询，查询所有包含MyASGrid, MyASGridNodeBuffer组件的实体
        EntityQuery entityQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MyASGrid>(), ComponentType.ReadOnly<MyASGridNodeBuffer>()); 

        //获取查询结果
        using NativeArray<Entity> entities = entityQuery.ToEntityArray(Allocator.Temp);

        //float3 gridPosition = new float3(0, 0, 0);

        foreach (Entity entity in entities)
        {
            //获取实体上的MyASGrid组件
            MyASGrid entity_MyASGrid = entityManager.GetComponentData<MyASGrid>(entity);

            LocalTransform gridPosition = entityManager.GetComponentData<LocalTransform>(entity);

            //获取实体上的MyASGridNodeBuffer组件, 里面存放了所有节点数据
            DynamicBuffer<MyASGridNodeBuffer> nodes = entityManager.GetBuffer<MyASGridNodeBuffer>(entity);

            //绘制节点
            foreach (MyASGridNodeBuffer node in nodes)
            {
                MyASNode singleNode = node.Node;

                //设置颜色
                Gizmos.color = singleNode.IsAccessible ? GridColor : blockedColor;
                //计算节点位置(中心)
                float3 nodePosition = MyGridUtility.GridToWorld(singleNode.X, singleNode.Y, entity_MyASGrid, gridPosition);
                //绘制实心方块
                Gizmos.DrawCube(nodePosition, new float3(entity_MyASGrid.NodeSize, 0.1f, entity_MyASGrid.NodeSize));

                Gizmos.color = WireColor;
                //绘制方块边框
                Gizmos.DrawWireCube(nodePosition, new float3(entity_MyASGrid.NodeSize, 0.1f, entity_MyASGrid.NodeSize));
            }
        }

    }
}
