using Unity.Entities;
using Unity.Mathematics;

namespace ECSDemo
{
    // 移动速度组件（方向 + 大小合并为一个速度向量）
    // float3 是 Unity.Mathematics 提供的 SIMD 友好向量类型，比 Vector3 更快
    public struct MoveVelocityComponent : IComponentData
    {
        public float3 Value; // xyz 各轴速度（单位：米/秒）
    }
}
