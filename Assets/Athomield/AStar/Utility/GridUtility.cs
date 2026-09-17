using Unity.Mathematics;
using Unity.Transforms;
using Unity.Collections;
using Unity.Entities;

namespace Athomield.AStar
{
    /// <summary>
    /// 网格坐标工具类，提供网格坐标与世界坐标互转、节点距离计算、邻居查询等功能。
    /// 所有方法均为静态方法，可在 Job 内调用（不涉及托管类型）
    /// </summary>
    public static class GridUtility
    {
        /// <summary>
        /// 将网格坐标（列 x, 行 y）转换为世界坐标（XZ 平面中心点）。
        /// 公式：世界坐标 = 网格左下角坐标 + (x * NodeSize + NodeSize/2) * right + (y * NodeSize + NodeSize/2) * forward
        /// </summary>
        /// <param name="_x">网格列坐标（0 到 NodeXCount-1）</param>
        /// <param name="_y">网格行坐标（0 到 NodeYCount-1）</param>
        /// <param name="_grid">网格参数</param>
        /// <param name="_transform">网格 Entity 的世界变换（Grid 中心位置）</param>
        /// <returns>该节点在世界空间中的中心点坐标（Y=网格 Y）</returns>
        public static float3 GridCoordsToWorld(int _x, int _y, ASGrid _grid, LocalTransform _transform)
        {
            // 计算网格左下角（X-，Z- 方向）世界坐标
            float3 startCorner = new float3(_transform.Position.x, _transform.Position.y, _transform.Position.z)
                                 - math.right() * ((_grid.NodeXCount * _grid.NodeSize) / 2)
                                 - math.forward() * ((_grid.NodeYCount * _grid.NodeSize) / 2);

            // 加上节点偏移（节点中心，所以乘2再加1后除2 = x*NodeSize + NodeSize/2）
            return startCorner
                   + math.right() * ((_x * _grid.NodeSize * 2 + _grid.NodeSize) / 2)
                   + math.forward() * ((_y * _grid.NodeSize * 2 + _grid.NodeSize) / 2);
        }
    }
}