using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class MyASObstacleAuthoring : MonoBehaviour
{
    public class MyASObstacleBaker : Baker<MyASObstacleAuthoring>
    {
        public override void Bake(MyASObstacleAuthoring authoring)
        {
            //创建一个实体
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            //创建一个组件
            AddComponent<MyASObstacle>(entity);

            //设置组件的值
            
        }
    }
}
