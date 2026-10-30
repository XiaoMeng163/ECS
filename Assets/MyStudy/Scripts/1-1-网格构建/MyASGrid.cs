using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

/// <summary>
/// 网格组件，描述的是整个网格，挂载在全局唯一的GridEntity上
/// </summary>
public struct MyASGrid : IComponentData
{
    public int NodeXCount;
    public int NodeYCount;
    public float NodeSize;

    /// <summary> 障碍物层 </summary>
    public int InaccessibleLayerMask;
}

/// <summary>
/// 一格的数据结构
/// </summary>
public struct MyASNode
{
    public int X;
    public int Y;
    /// <summary>节点是否可通行（false 表示被障碍物占据）</summary>
    public bool IsAccessible;
}

// ECS 的 IComponentData 要求数据适合 ECS 的原生、非托管数据布局。
// 普通 C# 的 MyASNode[] Nodes 是一个托管数组引用，不适合作为普通 ECS Component 里的数据。
// 所以 ECS 提供了：IBufferElementData 专门解决：一个 Entity 需要挂很多个同类型数据这种情况。
[InternalBufferCapacity(0)]
public struct MyASGridNodeBuffer : IBufferElementData
{
    public MyASNode Node;
}
