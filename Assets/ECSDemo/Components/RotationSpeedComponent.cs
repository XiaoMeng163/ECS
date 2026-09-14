using Unity.Entities;

namespace ECSDemo
{
    // ============================================================
    // ECS 核心概念 1: Component（组件）
    // ============================================================
    // IComponentData 就是 ECS 里的"C"。
    //
    // 规则：
    //   - 只存数据，绝对不写方法和逻辑
    //   - 必须是 unmanaged struct（不能有 string、List<> 等托管类型字段）
    //   - 同类型组件在内存中连续排列（Chunk），CPU 缓存命中率极高
    //
    // 和传统 MonoBehaviour 的区别：
    //   MonoBehaviour = 数据 + 逻辑（耦合在一起）
    //   ECS Component  = 纯数据
    //   ECS System     = 纯逻辑（另一个文件，见 Systems 目录）
    // ============================================================
    public struct RotationSpeedComponent : IComponentData
    {
        public float RadiansPerSecond; // 每秒旋转弧度
    }
}
