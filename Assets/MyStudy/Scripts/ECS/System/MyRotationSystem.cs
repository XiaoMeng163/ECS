using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

//BurstCompile：Burst 编译器把 C# 编译为高度优化的原生代码（接近 C++ 性能）。
[BurstCompile]
//partial关键字：Unity Source Generator 分析Query参数后，自动生成 EntityQuery 代码，不需要手写。
public partial struct MyRotationSystem : ISystem //Isystem 性能好 systembase 方便
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        // RequireForUpdate：场景中必须存在 MyRotationSpeedCompent，
        // 否则本系统每帧跳过，避免空循环开销
        state.RequireForUpdate<MyRotationSpeedCompent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 获取每帧的时间增量
        float deltaTime = SystemAPI.Time.DeltaTime;

        //Transform 是传统 GameObject 的 MonoBehaviour/Component 类，不是 ECS 的 IComponentData，而且是引用类型，所以 RefRW<T> 不接受它。
        //SystemAPI.Query找出同时拥有 LocalTransform 和 MyRotationSpeedCompent 的 Entity。
        //RefRW<LocalTransform> 表示：我要读写这个 Entity 的 Transform。
        //RefRO<MyRotationSpeedCompent> 表示：我只需要读取旋转速度。
        foreach(var (transform, speed) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<MyRotationSpeedCompent>>())
        {
            transform.ValueRW = transform.ValueRO.RotateY(speed.ValueRO.RadianPerSecond * deltaTime);
        }


    }
}
