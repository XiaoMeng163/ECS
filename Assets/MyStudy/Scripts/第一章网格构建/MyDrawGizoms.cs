using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class MyDrawGizoms : MonoBehaviour
{
    //DynamicBuffer 不能直接从 GameObject 获取，需要先找到对应的 ECS Entity，再通过 EntityManager.GetBuffer 获取
    void OnDrawGizmos()
    {
        World world = World.DefaultGameObjectInjectionWorld;

        //编辑器初始化时可能ECS世界还不存在
        if(world == null || !world.IsCreated)
        {
            return;
        }

        EntityManager entityManager = world.EntityManager;
        

    }
}
