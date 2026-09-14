using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

//禁止同一个 GameObject 挂载多个 RotatorAuthoring，防止用户误操作
[DisallowMultipleComponent]
public class MyRotatorAuthoring : MonoBehaviour
{
    [Header("每秒旋转角度")]
    public float DegreesPerSecond = 90f;

    //烘焙器
    public class MyRotatorBaker : Baker<MyRotatorAuthoring>
    {
        public override void Bake(MyRotatorAuthoring authoring)
        {
            // 获取当前 GameObject 对应的 Entity
            //             | 字段               | 简单说明                                                               |
            // | ---------------- | ------------------------------------------------------------------ |
            // | `None`           | 不需要 Transform 功能。Entity 不需要位置、旋转、缩放等 Transform 数据。             |
            // | `Renderable`     | 用于渲染。通常表示这个 Entity 需要参与渲染，因此需要基本的 Transform 数据。                |
            // | `Dynamic`        | 运行时会移动/旋转/缩放。需要完整的动态 Transform 支持。最常用的动态物体一般使用这个。              |
            // | `WorldSpace`     | 使用世界坐标，不需要依赖父物体的 Transform 层级。                                 |
            // | `ManualOverride` | 手动控制 Transform 的烘焙方式。告诉 Baker 不要按照普通 Transform 规则自动处理，而由你自己控制。 |

            Entity entity = GetEntity(authoring, TransformUsageFlags.Dynamic);

            //Component 注意要是 struct，不能是 class，否则会报错
            AddComponent(entity, new MyRotationSpeedCompent {
                //math.radians(角度) → 弧度
                RadianPerSecond = math.radians(authoring.DegreesPerSecond)
            });

            //速度交给MySpawnerSystem运行时赋随机值
            AddComponent(entity, new MyMoveVelocityComponent());
        }
    }
}
