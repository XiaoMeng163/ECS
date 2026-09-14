using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECSDemo
{
    // ============================================================
    // ECS 核心概念 2: Authoring / Baker（烘焙工作流）
    // ============================================================
    // 问题：ECS Entity 没有 Inspector，怎么在编辑器里配置数据？
    // 答案：Authoring + Baker 两步走：
    //
    //   步骤 1 —— Authoring（编辑器阶段）
    //     写一个普通 MonoBehaviour 挂到 GameObject 上，
    //     用 Inspector 像往常一样配置参数。
    //
    //   步骤 2 —— Baker（烘焙/转换阶段）
    //     进入 Play 模式时，Unity 调用 Baker，
    //     Baker 把 GameObject + MonoBehaviour 数据转换为 ECS Entity + Component。
    //     转换后，运行时只剩纯 ECS 世界，不存在 GameObject 开销。
    //
    // 重要：Authoring 脚本必须放在 SubScene 里的 GameObject 上，
    //       普通场景的 GameObject 不会被烘焙。
    // ============================================================
    [DisallowMultipleComponent]
    public class RotatorAuthoring : MonoBehaviour
    {
        [Tooltip("每秒旋转角度")]
        public float DegreesPerSecond = 90f;

        // Baker 写成内部类，泛型参数指定对应的 Authoring 类型
        class RotatorBaker : Baker<RotatorAuthoring>
        {
            public override void Bake(RotatorAuthoring authoring)
            {
                // GetEntity：获取当前 GameObject 对应的 Entity
                // TransformUsageFlags.Dynamic：告诉 ECS 这个 Entity 的 Transform 会变化
                //   → ECS 会为它保留完整的 LocalTransform 组件
                var entity = GetEntity(TransformUsageFlags.Dynamic);

                // AddComponent：给 Entity 添加组件并初始化数据
                AddComponent(entity, new RotationSpeedComponent
                {
                    RadiansPerSecond = math.radians(authoring.DegreesPerSecond)
                });

                // MoveVelocityComponent 先设为零，由 SpawnerSystem 运行时赋随机值
                AddComponent(entity, new MoveVelocityComponent());
            }
        }
    }
}
