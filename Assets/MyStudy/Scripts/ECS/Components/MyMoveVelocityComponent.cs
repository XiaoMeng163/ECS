using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public partial struct MyMoveVelocityComponent : IComponentData
{
    public float3 value; // xyz 各轴速度（单位：米/秒）
}
