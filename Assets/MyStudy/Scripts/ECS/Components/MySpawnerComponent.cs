using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public struct MySpawnerComponent : IComponentData
{
    public Entity prefab;

    public float spawnRange; // 生成范围（立方体半边长）

    public int count; // 生成数量
}
