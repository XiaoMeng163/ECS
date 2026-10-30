using System;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace Athomield.AStar
{
    /// <summary>
    /// 导航网格和代理的 Scene 视图可视化组件（Editor + Play Mode 均可使用）。
    ///
    /// 挂载此 MonoBehaviour 到任意 GameObject 后，在 Inspector 中选择需要显示的调试层：
    /// - ShowGrid：绘制每个网格节点（可通行=GridColor，不可通行=红色）
    /// - ShowGridIslands：用不同颜色区分连通区域（Island），需要 Play Mode 下才有岛屿数据
    /// - ShowPenalties：以灰度热力图显示节点的地形惩罚值（越白=惩罚越高）
    /// - ShowPaths：绘制每个代理当前的路径点连线（黄色折线）
    ///
    /// 此外在 Update 中监听 A 键，按下后触发一次网格刷新（调试用途）。
    ///
    /// 注意：OnDrawGizmos 每帧都调用，包含 EntityQuery，在节点数量大时可能有性能开销，
    /// 建议在 Profile 时关闭不需要的可视化选项。
    /// </summary>
    public class GridDrawGizmos : MonoBehaviour
    {
        /// <summary>ECS EntityManager 缓存，首次访问时从默认 World 获取</summary>
        private EntityManager mEntityManager;

        /// <summary>可通行节点的填充颜色（ShowGrid 开启时生效）</summary>
        [SerializeField] Color GridColor;

        /// <summary>节点边框的线框颜色（ShowGrid 开启时叠加绘制）</summary>
        [SerializeField] Color GridWireColor;

        /// <summary>是否绘制网格节点（true = 绘制所有节点的填充方块和线框）</summary>
        [SerializeField] bool ShowGrid;
        
        /// <summary>
        /// 是否以灰度热力图显示节点的惩罚值。
        /// 颜色计算：penC = Penalty / MaxPenalty，灰度 = (penC, penC, penC)。
        /// 惩罚越高越白，越低越黑。仅在 Play Mode 下有效（CheckObstaclesSystem 才会填充惩罚值）。
        /// </summary>
        [SerializeField]
        bool ShowPenalties;
        
        /// <summary>
        /// 惩罚热力图的参考最大值，用于归一化显示。
        /// 将 Penalty / MaxPenalty 映射到 [0,1] 作为灰度。
        /// 设置为场景中最大惩罚值可获得最佳对比度（过小则所有节点显示为白色）。
        /// </summary>
        [SerializeField] private int MaxPenalty;

        /// <summary>
        /// Unity 的 Gizmos 绘制回调，每帧（包括 Editor 中）均会调用。
        /// 根据 Inspector 中选择的选项，绘制网格、岛屿、惩罚热力图、路径和代理胶囊体。
        /// </summary>
        private void OnDrawGizmos()
        {
            // 懒初始化 EntityManager（在 Editor 中 World 可能尚未创建，需保护）
            if (mEntityManager == default)
                mEntityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
            
            // 防止 MaxPenalty 为负数导致除零或颜色异常
            if (MaxPenalty <= 0) MaxPenalty = 0;

            if (ShowGrid)
            {
                // 查询唯一的 Grid Entity
                var query = mEntityManager.CreateEntityQuery(ComponentType.ReadOnly<ASGrid>());
                var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);

                foreach (var entity in entities)
                {
                    ASGrid grid = mEntityManager.GetComponentData<ASGrid>(entity);
                    LocalTransform transform = mEntityManager.GetComponentData<LocalTransform>(entity);
                    DynamicBuffer<ASGridNodesBufferComponent> nodes =
                        mEntityManager.GetBuffer<ASGridNodesBufferComponent>(entity);

                    // 计算网格左下角（X-，Z- 方向）的世界坐标（节点绘制的起点参考）
                    Vector3 startCorner = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z)
                                          - Vector3.right * ((grid.NodeXCount * grid.NodeSize) / 2)
                                          - Vector3.forward * ((grid.NodeYCount * grid.NodeSize) / 2);

                    // d = max(NodeXCount, NodeYCount)，用于一维索引计算（与 GridAuthoring 一致）
                    int d = grid.NodeXCount > grid.NodeYCount ? grid.NodeXCount : grid.NodeYCount;


                    for (int x = 0; x < grid.NodeXCount; x++)
                    {
                        for (int y = 0; y < grid.NodeYCount; y++)
                        {
                            // 计算当前节点在一维缓冲区中的索引
                            int index = 0;
                            if (grid.NodeXCount > grid.NodeYCount)
                                index = x + y * d;
                            else index = y + x * d;

                            // 默认颜色：可通行=GridColor，不可通行=红色
                            Gizmos.color = nodes[index].Node.IsAccessible ? GridColor : Color.red;

                            
                            // 惩罚热力图颜色覆盖（Play Mode 下，CheckObstaclesSystem 运行后才有数据）
                            if(ShowPenalties && Application.isPlaying)
                            {
                                // 归一化：penC ∈ [0,1]，对应灰度（0=黑=无惩罚，1=白=最大惩罚）
                                float penC = nodes[index].Node.Penalty / (float)MaxPenalty;
                                Gizmos.color = new Color(penC, penC, penC);
                            }
                            
                            // 绘制节点填充方块（高度 0.1f 使其贴近地面但仍可见）
                            Gizmos.DrawCube(
                                startCorner + Vector3.right * ((x * grid.NodeSize * 2 + grid.NodeSize) / 2)
                                            + Vector3.forward * ((y * grid.NodeSize * 2 + grid.NodeSize) / 2),
                                new Vector3(grid.NodeSize, 0.1f, grid.NodeSize));

                            // 绘制节点线框（使用 GridWireColor，叠加在填充方块上方，便于区分边界）
                            Gizmos.color = GridWireColor;
                            Gizmos.DrawWireCube(
                                startCorner + Vector3.right * ((x * grid.NodeSize * 2 + grid.NodeSize) / 2)
                                            + Vector3.forward * ((y * grid.NodeSize * 2 + grid.NodeSize) / 2),
                                new Vector3(grid.NodeSize, 0.1f, grid.NodeSize));
                        }
                    }
                }
                entities.Dispose();
            }
        }
    }
}