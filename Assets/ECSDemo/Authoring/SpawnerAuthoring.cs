using Unity.Entities;
using UnityEngine;

namespace ECSDemo
{
    // ============================================================
    // 生成器 Authoring —— 演示 ECS Prefab 工作流
    // ============================================================
    //
    // 【编辑器配置步骤】
    //   1. 在 Hierarchy 窗口右键 → New Sub Scene → Empty Sub Scene，创建 SubScene
    //   2. 在 SubScene 内创建一个空 GameObject，挂上本脚本（SpawnerAuthoring）
    //   3. 创建一个 Cube Prefab，挂上 RotatorAuthoring 脚本
    //   4. 将 Prefab 拖到本脚本的 Prefab 字段
    //   5. 设置 Count（建议 1000+，体验 ECS 性能）
    //   6. Play！
    //
    // 【原理】
    //   Baker 把 UnityEngine.GameObject prefab 引用转为 ECS Entity 引用。
    //   运行时 SpawnerSystem 用这个 Entity 引用批量实例化。
    // ============================================================
    [DisallowMultipleComponent]
    public class SpawnerAuthoring : MonoBehaviour
    {
        [Tooltip("要批量生成的 Prefab，需挂有 RotatorAuthoring 组件")]
        public GameObject Prefab;

        [Tooltip("生成数量（试试 10000！）")]
        public int Count = 1000;

        [Tooltip("生成范围（立方体半边长，单位：米）")]
        public float SpawnRange = 10f;

        class SpawnerBaker : Baker<SpawnerAuthoring>
        {
            public override void Bake(SpawnerAuthoring authoring)
            {
                // 生成器本身不需要 Transform，用 None
                var entity = GetEntity(TransformUsageFlags.None);

                AddComponent(entity, new SpawnerComponent
                {
                    // GetEntity(go, flags)：把 GameObject 引用转换为 ECS Entity 引用
                    Prefab     = GetEntity(authoring.Prefab, TransformUsageFlags.Dynamic),
                    Count      = authoring.Count,
                    SpawnRange = authoring.SpawnRange,
                });
            }
        }
    }
}
