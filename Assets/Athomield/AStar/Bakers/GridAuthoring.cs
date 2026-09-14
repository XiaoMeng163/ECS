using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace Athomield.AStar
{
    /// <summary>
    /// 导航网格的 Authoring 组件（挂载在 Unity GameObject 上）。
    /// 在 SubScene 烘焙阶段，GridBaker 将 Inspector 参数转换为 ECS 组件，
    /// 并初始化所有网格节点（默认全部可通行）
    /// </summary>
    public class GridAuthoring : MonoBehaviour
    {

        [Header("Grid")]
        /// <summary>网格总尺寸（X=宽度, Y=深度，单位：世界空间）</summary>
        public Vector2 mGridSize;

        /// <summary>每个节点的边长（越小精度越高，但节点数量呈平方增长）</summary>
        public float mNodeSize;

        /// <summary>
        /// Baker 内部类：将 GridAuthoring 参数烘焙为 ECS 单例组件，并初始化网格节点数组
        /// </summary>
        public class GridBaker : Baker<GridAuthoring>
        {
            public override void Bake(GridAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                // 根据网格尺寸和节点大小计算节点数量
                ASGrid grid = new ASGrid
                {
                    NodeXCount = (int)math.round(authoring.mGridSize.x / authoring.mNodeSize),
                    NodeYCount = (int)math.round(authoring.mGridSize.y / authoring.mNodeSize),
                    NodeSize = authoring.mNodeSize,
                };

                // 添加 Grid 核心组件
                AddComponent(entity, grid);
                // 初始化节点数组（NodeXCount × NodeYCount 个节点，全部默认可通行）
                DynamicBuffer<ASGridNodesBufferComponent> nodes = AddBuffer<ASGridNodesBufferComponent>(entity);
                nodes.Length = grid.NodeXCount * grid.NodeYCount;

                // d = max(NodeXCount, NodeYCount)，用于一维索引计算
                int d = grid.NodeXCount > grid.NodeYCount ? grid.NodeXCount : grid.NodeYCount;

                for (int x = 0; x < grid.NodeXCount; x++)
                {
                    for (int y = 0; y < grid.NodeYCount; y++)
                    {
                        // 创建默认节点（可通行，无惩罚，G/H 代价为0）
                        ASNode newNode = new ASNode()
                        {
                            X = x,
                            Y = y,
                            IsAccessible = true,
                        };

                        // 根据主轴选择索引方式（保证数组紧凑）
                        int index;
                        if (grid.NodeXCount > grid.NodeYCount)
                            index = x + y * d;
                        else index = y + x * d;

                        nodes[index] = new ASGridNodesBufferComponent() { Node = newNode };
                    }
                }
                // 注：CheckObstaclesSystem 在运行时会更新节点的 IsAccessible 和 Penalty
            }

        }
    }
}