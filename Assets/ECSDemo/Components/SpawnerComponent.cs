using Unity.Entities;

namespace ECSDemo
{
    // 生成器组件
    //
    // 注意 Entity 类型的字段：
    //   ECS 中 Prefab 也是一个 Entity，可以被其他 Entity 引用。
    //   Baker 负责把 UnityEngine.GameObject 引用转换为 Entity 引用（见 SpawnerAuthoring.cs）。
    public struct SpawnerComponent : IComponentData
    {
        public Entity Prefab;    // 要批量生成的 ECS Prefab
        public int    Count;     // 生成数量
        public float  SpawnRange; // 生成范围（立方体半边长）
    }
}
