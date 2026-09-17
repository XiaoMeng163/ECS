using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

public static class MyGridUtility
{
    /// <summary>
    /// 将单个节点物体的网格坐标转化成世界坐标
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="myASGrid"></param>
    /// <param name="localTransform"></param>
    /// <returns></returns>
    public static float3 GridToWorld(int x, int y, MyASGrid myASGrid, LocalTransform localTransform)
    {
        float3 bottomLeft = localTransform.Position - new float3(myASGrid.NodeXCount * myASGrid.NodeSize / 2,
                0, 
                myASGrid.NodeYCount * myASGrid.NodeSize / 2);

        float3 nodePosition = bottomLeft + new float3((x + 0.5f) * myASGrid.NodeSize, 0, (y + 0.5f) * myASGrid.NodeSize);

        return nodePosition; 
    }
}
