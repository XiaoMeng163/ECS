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

    /// <summary> 惩罚显示开关 </summary>
    [SerializeField] 
    bool showPenalties;

    /// <summary> 是否按岛屿显示颜色 </summary>
    [SerializeField]
    private bool showIslands;
    /// <summary> 岛屿颜色列表，数组下标对应 IslandID </summary>
    [SerializeField]
    private Color[] islandColors;

    /// <summary> 参考最大值 </summary>
    [SerializeField] 
    int maxPenalty = 50;

    // DynamicBuffer 不能直接从 GameObject 获取，需要先找到对应的 ECS Entity，再通过 EntityManager.GetBuffer 获取
    void OnDrawGizmos()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        // 编辑器初始化时可能ECS世界还不存在
        if(world == null || !world.IsCreated)
        {
            return;
        }

        // 获取ECS世界中的EntityManager
        if(entityManager == default)
        entityManager = world.EntityManager;

        // 创建一个查询，查询所有包含MyASGrid, MyASGridNodeBuffer组件的实体
        EntityQuery entityQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<MyASGrid>(), ComponentType.ReadOnly<MyASGridNodeBuffer>()); 

        // 获取查询结果
        using NativeArray<Entity> entities = entityQuery.ToEntityArray(Allocator.Temp);

        // float3 gridPosition = new float3(0, 0, 0);

        foreach (Entity entity in entities)
        {
            // 获取实体上的MyASGrid组件
            MyASGrid entity_MyASGrid = entityManager.GetComponentData<MyASGrid>(entity);

            LocalTransform gridPosition = entityManager.GetComponentData<LocalTransform>(entity);

            // 获取实体上的MyASGridNodeBuffer组件, 里面存放了所有节点数据
            DynamicBuffer<MyASGridNodeBuffer> nodes = entityManager.GetBuffer<MyASGridNodeBuffer>(entity);
            DynamicBuffer<MyASNodeIslandBuffer> nodeIslandIDs = entityManager.GetBuffer<MyASNodeIslandBuffer>(entity);

            // 绘制节点
            for (int i = 0; i < nodes.Length; i++)
            {
                MyASNode singleNode = nodes[i].Node;

                // 计算节点位置(中心)
                float3 nodePosition = MyGridUtility.GridToWorld(singleNode.X, singleNode.Y, entity_MyASGrid, gridPosition);

                // 优先绘制惩罚值 并设置颜色
                if (showPenalties && maxPenalty > 0)
                {
                    // Penalty = 0 → 黑色   Penalty = 25 → 灰色   Penalty = 50 → 白色
                    float value = math.saturate(singleNode.Penalty / (float)maxPenalty);

                    Gizmos.color = new Color(value, value, value);
                }
                // 绘制岛屿 并设置颜色
                // MyASNodeIslandBuffer 虽然已经由 Baker 添加到 Entity，但刚添加时长度仍为 0, 岛屿系统运行后才会初始化岛屿ID数组大小，所以要加一个长度检查
                else if(showIslands && singleNode.IsAccessible && Application.isPlaying && nodeIslandIDs.Length == nodes.Length)
                {
                    int islandID = nodeIslandIDs[i].IslandID;

                    if(islandColors != null && islandColors.Length > 0 && islandID >= 0)
                    {
                        // 颜色数量不够就循环使用
                        Gizmos.color = islandColors[islandID % islandColors.Length];
                    }
                    else
                    {
                        Debug.LogError("没有足够的岛屿颜色, 岛屿颜色数组长度为:" + islandColors.Length);
                        Debug.Log("岛屿ID:" + islandID);
                        Gizmos.color = GridColor;
                    }
                }
                else
                {
                    Gizmos.color = singleNode.IsAccessible ? GridColor : blockedColor;
                }

                // 绘制实心方块
                Gizmos.DrawCube(nodePosition, new float3(entity_MyASGrid.NodeSize, 0.1f, entity_MyASGrid.NodeSize));

                Gizmos.color = WireColor;
                // 绘制方块边框
                Gizmos.DrawWireCube(nodePosition, new float3(entity_MyASGrid.NodeSize, 0.1f, entity_MyASGrid.NodeSize));
            }
        }

    }
}
