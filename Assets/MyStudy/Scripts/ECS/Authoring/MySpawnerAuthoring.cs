using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class MySpawnerAuthoring : MonoBehaviour
{
    public GameObject prefab_obj;

    //Spawner GameObject
    //       ↓ Baker
    // Spawner Entity
    //       ↓
    // MySpawnerComponent
    //       ↓
    // prefab = 某个 Prefab 对应的 Entity

    public int Atr_count = 10; // 生成数量
    public float Atr_spawnRange = 5f; // 生成范围（立方体半边长）

    public class MySpawnerBaker : Baker<MySpawnerAuthoring>
    {
        public override void Bake(MySpawnerAuthoring authoring)
        {
            Entity entity = GetEntity(authoring, TransformUsageFlags.None);

            AddComponent(entity, new MySpawnerComponent
            {
                prefab = GetEntity(authoring.prefab_obj, TransformUsageFlags.Renderable),
                count = authoring.Atr_count,
                spawnRange = authoring.Atr_spawnRange
            });

        }
    }
}
