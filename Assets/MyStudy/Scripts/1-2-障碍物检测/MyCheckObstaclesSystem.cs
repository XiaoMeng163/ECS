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
        foreach (var (gridRef, transformRef, entity) in SystemAPI.Query<RefRW<MyASGrid>, RefRO<LocalTransform>>().WithEntityAccess())
        {
            MyASGrid myASGrid = gridRef.ValueRO;
            LocalTransform localTransform = transformRef.ValueRO;

            // MyDrawGizmos 里也有一个 entityManager.GetBuffer
            // SystemAPI.Query 适合 System
            // World → EntityManager 常用于普通 MonoBehaviour 与 ECS 数据交互。
            DynamicBuffer<MyASGridNodeBuffer> nodeBuffers = SystemAPI.GetBuffer<MyASGridNodeBuffer>(entity);

            CheckGrid(myASGrid, localTransform, nodeBuffers, physicsWorld);

            BlurPenalties(myASGrid, nodeBuffers, 2);

            // 障碍信息已经检测完毕，请求刷新岛屿
            gridRef.ValueRW.RefreshIslands = true;;
        }

        haveScanned = true;
    }

    /// <summary>
    /// 检测网格节点里哪些是障碍物
    /// </summary>
    /// <param name="myASGrid"></param>
    /// <param name="localTransform"></param>
    /// <param name="nodeBuffers"></param>
    /// <param name="physicsWorld"></param>
    private void CheckGrid(MyASGrid myASGrid, LocalTransform localTransform, DynamicBuffer<MyASGridNodeBuffer> nodeBuffers, PhysicsWorld physicsWorld)
    {
        // 创建碰撞结果和过滤规则
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

                // 每次检测前清理上一次检测残留的标志
                node.IsAccessible = true;
                node.Penalty = 0;

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
                        node.Penalty = obstacleLookup[hitEntity].PenaltyOnObstacleValue;
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

    /// <summary>
    /// 高斯模糊(箱式滤波处理) 
    /// </summary>
    /// <param name="myASGrid">网格整体</param>
    /// <param name="nodeBuffers">格子数组</param>
    /// <param name="blurRadius">模糊半径</param>
    private void BlurPenalties(MyASGrid myASGrid, DynamicBuffer<MyASGridNodeBuffer> nodeBuffers, int blurRadius)
    {
        if(blurRadius <= 0)
            return;
        
        // 创建临时数组，用于存储一维模糊后的Penalty值(水平)
        NativeArray<int> horizontalPenalties = new NativeArray<int>(nodeBuffers.Length, Allocator.Temp);
        // 垂直模糊 x 水平模糊后的二维模糊 Penalty值 临时数组
        NativeArray<int> sumPenalties = new NativeArray<int>(nodeBuffers.Length, Allocator.Temp);
        // 模糊半径
        int kernelSize = (blurRadius * 2) + 1;
        // 宽
        int width = myASGrid.NodeXCount;
        // 高
        int height = myASGrid.NodeYCount;

        try
        {
            // 遍历网格节点，根据原始Penalty值计算每个节的平均值
            // 二维卷积可以拆成两次一维卷积，先对所有行做水平方向的1D模糊
            for (int y = 0; y < height; y++)
            {
                // 滑动窗口
                int windowSum = 0;

                // 先计算当前行的 x = 0 的窗口
                for (int offsetX = -blurRadius; offsetX <= blurRadius; offsetX++)
                {   
                    // 限定窗口范围 比如 blurRadius 为 1 的窗口，x = 0 时，窗口范围是 [0, 0, 1]
                    int sampleIndex = math.clamp(offsetX, 0, width - 1) + y * width;
                    // 相当于边界的惩罚值重复填装
                    // 影响：边界节点的模糊值偏小（因为窗口内有重复的边界节点，“稀释”了惩罚值）
                    // 对寻路的影响极小，因为网格边界通常本身就是不可通行区域
                    windowSum += nodeBuffers[sampleIndex].Node.Penalty;
                }

                // 记录当前行的第一个节点的模糊Penalty值(水平方向)
                horizontalPenalties[y * width] = windowSum;

                for (int x = 1; x < width; x++)
                {
                    // 移动窗口
                    // 先减去最左边的值，再加上最右边的值
                    int removeX = math.clamp(x - blurRadius - 1, 0, width - 1);
                    int addX =  math.clamp(x + blurRadius, 0, width - 1);

                    int removeIndex = removeX + y * width;
                    int addIndex = addX + y * width;

                    // 更新窗口
                    windowSum = windowSum - nodeBuffers[removeIndex].Node.Penalty + nodeBuffers[addIndex].Node.Penalty;

                    // 计算当前节点的模糊Penalty值(水平方向)
                    int currentNodeIndex = x + y * width;
                    horizontalPenalties[currentNodeIndex] = windowSum;
                }
            }
            // 再对所有列做垂直方向的1D模糊
            for (int x = 0; x < width; x++)
            {
                int windowSum = 0;

                // 先计算当前列的 y = 0 的窗口
                for (int offsetY = -blurRadius; offsetY <= blurRadius; offsetY++)
                {
                    int sampleIndex = math.clamp(offsetY, 0, height - 1) * width + x;
                    // 后一个一维的模糊计算受到前一个一维模糊的影响
                    windowSum += horizontalPenalties[sampleIndex];
                }

                sumPenalties[x] = (int)math.round(windowSum / (float)(kernelSize * kernelSize));
                
                for (int y = 1; y < height; y++)
                {
                    // 移动窗口
                    // 先减去最下边的值，再加上最上边的值
                    int removeY = math.clamp(y - blurRadius - 1, 0, height - 1);
                    int addY =  math.clamp(y + blurRadius, 0, height - 1);

                    int removeIndex = removeY * width + x;
                    int addIndex = addY * width + x;

                    // 更新窗口
                    windowSum = windowSum - horizontalPenalties[removeIndex] + horizontalPenalties[addIndex];

                    // 归一化
                    int currentNodeIndex = x + y * width;
                    sumPenalties[currentNodeIndex] = (int)math.round(windowSum / (float)(kernelSize * kernelSize));
                }
                
            }

            // 写回Buffer
            for(int i = 0; i < nodeBuffers.Length; i++)
            {   
                // 取出副本
                MyASGridNodeBuffer element = nodeBuffers[i];
                // 修改副本
                element.Node.Penalty = sumPenalties[i];
                // 把完整元素写回Buffer
                nodeBuffers[i] = element;
            }
        }
        finally
        {
            horizontalPenalties.Dispose();
            sumPenalties.Dispose();
        }
    }
}
