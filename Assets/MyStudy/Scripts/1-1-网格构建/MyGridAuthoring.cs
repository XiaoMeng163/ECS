using System.Collections;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;

public class MyGridAuthoring : MonoBehaviour
{
    public Vector2 GridSize = new Vector2(10, 10);

    public float NodeSize = 1f;

    [Header("不可达节点层级")]
    public LayerMask InaccessibleNodes;     

    public class MyGridBaker : Baker<MyGridAuthoring>
    {
        public override void Bake(MyGridAuthoring authoring)
        {
            Entity entity = GetEntity(TransformUsageFlags.Dynamic);

            if(authoring.NodeSize <= 0)
            {
                Debug.LogError("NodeSize 不得小于等于0.");
                return;
            }

            MyASGrid grid = new MyASGrid
            {
                NodeXCount = (int)(authoring.GridSize.x / authoring.NodeSize),
                NodeYCount = (int)(authoring.GridSize.y / authoring.NodeSize),
                NodeSize = authoring.NodeSize,
                InaccessibleLayerMask = authoring.InaccessibleNodes.value   //保存不可达层级的位掩码
            };

            AddComponent(entity, grid);

            //给 entity 添加一个 DynamicBuffer<MyASGridNodeBuffer>，并把这个 Buffer 返回出来。
            DynamicBuffer<MyASGridNodeBuffer> myASGridNodeBuffers = AddBuffer<MyASGridNodeBuffer>(entity);
            
            //得到网格的总节点数量
            int count = grid.NodeXCount * grid.NodeYCount;

            //给这个 Buffer 分配内存空间，长度为 count，后面要自己初始化数据
            myASGridNodeBuffers.ResizeUninitialized(count);

            //给每个Node节点初始化数据
            for(int x = 0; x < grid.NodeXCount; x++)
            {
                for(int y = 0; y < grid.NodeYCount; y++)
                {
                    myASGridNodeBuffers[x + y * grid.NodeXCount] = new MyASGridNodeBuffer
                    {
                        Node = new MyASNode
                        {
                            X = x,
                            Y = y,
                            IsAccessible = true
                        }
                    };
                }
            }

        }

    }
}
