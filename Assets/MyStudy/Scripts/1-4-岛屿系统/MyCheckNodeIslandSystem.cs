using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// 岛屿检测系统
/// </summary>
public partial struct MyCheckNodeIslandSystem : ISystem
{
    public void OnCreate(ref SystemState state)
    {
        // 需要MyASGrid组件
        state.RequireForUpdate<MyASGrid>();
    }

    public void OnUpdate(ref SystemState state)
    {
        foreach (var (gridRef, entity) in SystemAPI.Query<RefRW<MyASGrid>>().WithEntityAccess())
        {
            // 如果没有刷新请求就跳过
            if(!gridRef.ValueRO.RefreshIslands)
            {
                continue;   
            }

            gridRef.ValueRW.RefreshIslands = false;

            DynamicBuffer<MyASGridNodeBuffer> myASGridNodeBuffers = SystemAPI.GetBuffer<MyASGridNodeBuffer>(entity);

            DynamicBuffer<MyASNodeIslandBuffer> myASNodeIslandBuffers = SystemAPI.GetBuffer<MyASNodeIslandBuffer>(entity);

            CalculateIslands(gridRef.ValueRO, myASGridNodeBuffers, myASNodeIslandBuffers);
        }
    }

    /// <summary>
    /// 计算岛屿
    /// </summary>
    /// <param name="myASGrid"></param>
    /// <param name="nodes"></param>
    /// <param name="islandIDs"></param>
    public void CalculateIslands(MyASGrid myASGrid, DynamicBuffer<MyASGridNodeBuffer> nodes, DynamicBuffer<MyASNodeIslandBuffer> islandIDs)
    {
        islandIDs.ResizeUninitialized(nodes.Length);

        for (int i = 0; i < islandIDs.Length; i++)
        {
            // 最终该节点属于哪个连通区域
            islandIDs[i] = new MyASNodeIslandBuffer 
            {
                // -1表示没有所属岛屿或者不可通行 
                IslandID = -1 
            };
        }

        // 本轮 BFS 是否已经处理过该节点
        NativeArray<bool> visited = new NativeArray<bool>(nodes.Length, Allocator.Temp);

        try
        {
            int currentIslandID = 0;
            
            // 外层循环的作用不是遍历整个岛屿，而是寻找每个岛屿的第一个节点
            for (int i = 0; i < nodes.Length; i++)
            {   
                // 如果该节点已经处理过，就跳过
                if(visited[i]) continue;

                // 如果该节点不可通行，就跳过
                if(!nodes[i].Node.IsAccessible) continue;

                // 开始 BFS
                FloodFillIsland(myASGrid, nodes, islandIDs, visited, i, currentIslandID);

                currentIslandID++;
            }
        }
        finally
        {
            visited.Dispose();    
        }
    }

    /// <summary>
    /// BFS填充岛屿
    /// </summary>
    /// <param name="myASGrid"></param>
    /// <param name="nodes"></param>
    /// <param name="islandIDs"></param>
    /// <param name="visited"></param>
    /// <param name="nodeIndex"></param>
    /// <param name="islandID"></param>
    private void FloodFillIsland(MyASGrid myASGrid, DynamicBuffer<MyASGridNodeBuffer> nodes, DynamicBuffer<MyASNodeIslandBuffer> islandIDs, NativeArray<bool> visited, int nodeIndex, int islandID)
    {
        NativeQueue<int> queue = new NativeQueue<int>(Allocator.Temp);

        try
        {
            visited[nodeIndex] = true;
            queue.Enqueue(nodeIndex);

            while (queue.Count > 0)
            {
                int currentNodeIndex = queue.Dequeue();

                islandIDs[currentNodeIndex] = new MyASNodeIslandBuffer{ IslandID = islandID };

                // 通过一维索引还原二维坐标
                int x = currentNodeIndex % myASGrid.NodeXCount;
                int y = currentNodeIndex / myASGrid.NodeXCount;

                // 遍历上下左右四个节点
                TryGetNeighbor(x - 1, y, myASGrid, nodes, visited, ref queue);
                TryGetNeighbor(x + 1, y, myASGrid, nodes, visited, ref queue);
                TryGetNeighbor(x, y - 1, myASGrid, nodes, visited, ref queue);
                TryGetNeighbor(x, y + 1, myASGrid, nodes, visited, ref queue);
            }
        }
        finally
        {
            queue.Dispose();
        }
    }

    /// <summary>
    /// 尝试获取邻居节点
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="grid"></param>
    /// <param name="nodes"></param>
    /// <param name="visited"></param>
    /// <param name="queue"></param>
    private void TryGetNeighbor(int x, int y, MyASGrid grid, DynamicBuffer<MyASGridNodeBuffer> nodes, NativeArray<bool> visited, ref NativeQueue<int> queue)
    {
        if(x < 0 || x > grid.NodeXCount - 1 || y < 0 || y > grid.NodeYCount - 1)
        {
            return;
        }

        int neighborIndex = x + y * grid.NodeXCount;

        // 如果该节点已经处理过或者不可通行，就跳过
        if(!visited[neighborIndex] && nodes[neighborIndex].Node.IsAccessible)
        {
            visited[neighborIndex] = true;
            queue.Enqueue(neighborIndex);
        }
    }
}
