using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public struct MyRotationSpeedCompent : IComponentData
{   
    // 每秒旋转的弧度数
    public float RadianPerSecond;
}
