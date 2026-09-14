using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using System;
using UnityEngine;

namespace Athomield.AStar
{
    /// <summary>
    /// 寻路网格单例组件，描述整个导航网格的全局参数。
    /// 整个场景只有一个 Grid Entity 拥有此组件
    /// </summary>
    public struct ASGrid : IComponentData
    {
        /// <summary>X 方向节点数量 = Width / NodeSize（四舍五入）</summary>
        public int NodeXCount;

        /// <summary>Y 方向节点数量 = Height / NodeSize（四舍五入）</summary>
        public int NodeYCount;
        
        /// <summary>每个节点的边长（世界单位），决定网格精度</summary>
        public float NodeSize;
        
        /// <summary>
        /// 网格刷新触发标志。添加或移动障碍物/惩罚区域后，
        /// 将此值设为 true 以重新扫描网格节点可通行性
        /// </summary>
        public bool RefreshGrid;
    }

    /// <summary>
    /// 网格节点缓冲组件，以一维数组存储二维网格的所有节点。
    /// 索引计算：若 NodeXCount > NodeYCount，则 index = x + y * d；否则 index = y + x * d（d = max(NodeXCount, NodeYCount)）
    /// InternalBufferCapacity(0) 表示数据存储在堆内存中，避免影响 Chunk 布局
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct ASGridNodesBufferComponent : IBufferElementData
    {
        /// <summary>该位置的网格节点数据</summary>
        public ASNode Node;
    }

    /// <summary>
    /// 单个网格节点的数据结构，实现了 IHeapItem 接口以支持最小堆排序（A* open set）
    /// </summary>
    public struct ASNode : IEquatable<ASNode>
    {
        /// <summary>节点是否可通行（false 表示被障碍物占据）</summary>
        public bool IsAccessible;

        /// <summary>节点在网格中的 X 坐标（列）</summary>
        public int X;

        /// <summary>节点在网格中的 Y 坐标（行）</summary>
        public int Y;

        /// <summary>
        /// G 代价：从起点到此节点的实际移动代价（含地形惩罚）。
        /// 直线移动代价=10，斜线移动代价=14
        /// </summary>
        public int GCost;

        /// <summary>
        /// H 代价：此节点到目标节点的启发式估计代价（曼哈顿/切比雪夫混合启发）。
        /// H = 14 * min(dx,dy) + 10 * abs(dx-dy)
        /// </summary>
        public int HCost;
        
        /// <summary>节点相等判断：坐标相同即视为同一节点</summary>
        public bool Equals(ASNode other)
        {
            return X == other.X &&
                   Y == other.Y;
        }

        /// <summary>调试输出：显示该节点的 F 代价（G+H）</summary>
        public override string ToString()
        {
            return (GCost + HCost).ToString();
        }

        /// <summary>
        /// 哈希值：将 X、Y 坐标编码为单个整数，用于 NativeHashSet 的快速查找。
        /// 通过位运算将 Y 的高低16位分别异或到不同位置，降低碰撞概率
        /// </summary>
        public override int GetHashCode()
        {
            return X ^ (Y << 16) ^ (Y >> 16);
        }
    }

}