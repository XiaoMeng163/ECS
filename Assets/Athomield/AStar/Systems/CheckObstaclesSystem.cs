using Unity.Burst;
using Unity.Entities;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Physics;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Physics.Systems;
using Unity.Transforms;

namespace Athomield.AStar
{
    /// <summary>
    /// 障碍物和地形惩罚检测系统（网格初始化的核心系统）。
    ///
    /// ──── 完整执行流程 ────
    ///
    ///  RefreshGrid = true（由 GridAuthoring 初始烘焙或调试 A 键触发）
    ///         ↓
    ///  OnUpdate 检测到标志 → 重置扫描状态
    ///         ↓
    ///  CheckForObstacles：对每个节点执行 OverlapBox 物理查询
    ///    ├─ 命中 ASObstacle → IsAccessible = false，写入障碍物边缘惩罚
    ///    └─ 命中 ASPenalty → Penalty += 地形惩罚值
    ///         ↓
    ///  BlurPenalties：对惩罚值做可分离高斯模糊（水平 + 垂直两次箱式滤波）
    ///         ↓
    ///  RefreshIslands = true → 触发 CheckNodeIslandSystem 重新计算连通区域
    ///
    /// ──── 关键设计说明 ────
    /// • 此系统不用 Job，直接在主线程运行。原因：每帧节点数量可能非常大，
    ///   但此系统只在网格刷新时执行一次，性能可接受；且 OverlapBox 需要访问
    ///   PhysicsWorld，在 Job 内需要额外的安全标记。
    /// • ComponentLookup 在 OnCreate 时创建，每帧 Update 刷新引用，
    ///   避免重复创建带来的堆分配开销。
    /// </summary>
    partial struct CheckObstaclesSystem : ISystem
    {
        // 障碍物扫描完成标志（true = 本轮已扫描，无需重复执行）
        private bool mHasCheckedForObstacles;
        // 惩罚扫描完成标志（CheckForPenalties 已合并入 CheckForObstacles，此标志暂未使用）
        private bool mHasCheckedForPenalties;

        // ComponentLookup 缓存：按 Entity 快速查找 ASObstacle / ASPenalty 组件
        // 比 EntityManager.GetComponentData 更高效，内部使用 TypeIndex 直接寻址
        ComponentLookup<ASObstacle> mAllObstacles;
        ComponentLookup<ASPenalty> mAllPenalties;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            mHasCheckedForObstacles = false;
            mHasCheckedForPenalties = false;

            // 初始注册组件类型，GetComponentLookup 记录 TypeIndex 但不立即读数据
            mAllObstacles = state.GetComponentLookup<ASObstacle>();
            mAllPenalties = state.GetComponentLookup<ASPenalty>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // 每帧刷新 Lookup 缓存，使其指向当前帧的 Chunk 数据
            // （Structural Change改变实体，比如增加删除之类的会导致Chunk重组，不刷新会读到过期指针）
            mAllObstacles.Update(ref state);
            mAllPenalties.Update(ref state);

            // 遍历网格 Entity（通常只有一个，但框架支持多个网格共存）
            foreach (var (grid, transform, entity) in SystemAPI.Query<RefRW<ASGrid>, LocalTransform>().WithEntityAccess())
            {
                bool gridRefreshed = false;

                // ── 检测刷新请求 ──
                // RefreshGrid 由外部设置（如按 A 键、障碍物移动后）
                // 重置后重新执行一次完整的障碍物扫描
                if (grid.ValueRO.RefreshGrid)
                {
                    grid.ValueRW.RefreshGrid = false;
                    mHasCheckedForObstacles = false;
                    mHasCheckedForPenalties = false;
                    gridRefreshed = true;
                }

                // ── 执行障碍物 + 惩罚扫描 ──
                // 只在 mHasCheckedForObstacles = false 时执行，防止每帧重复扫描
                if (!mHasCheckedForObstacles)
                {
                    DynamicBuffer<ASGridNodesBufferComponent> nodes = SystemAPI.GetBuffer<ASGridNodesBufferComponent>(entity);
                    CheckForObstacles(grid.ValueRO, nodes, transform, ref state);

                    mHasCheckedForObstacles = true;
  
                }

                // ── 网格刷新后强制所有代理重新寻路 ──

            }
        }

        /// <summary>
        /// 遍历每个网格节点，通过 OverlapBox 物理查询检测障碍物和惩罚区域。
        ///
        /// ─── OverlapBox 工作原理 ───
        /// Unity Physics 的 OverlapBox 以节点中心为原点，以 NodeSize/2 为半径，
        /// 投出一个轴对齐盒子，返回所有与该盒子相交的碰撞体列表（DistanceHit）。
        /// CollisionFilter 通过位掩码（LayerMask）过滤只关心的层（障碍物层 | 惩罚层），
        /// 避免匹配到不相关的物体（如地面、代理自身）。
        ///
        /// ─── 结果处理逻辑 ───
        /// 对每个命中的碰撞体：
        ///   1. 如果其 Entity 有 ASObstacle 组件 → 节点标记为不可通行
        ///      （OverlapBox 的 bool 返回值本身代表"是否有碰撞" = IsAccessible 的反值）
        ///   2. 如果其 Entity 有 ASPenalty 组件 → 节点惩罚值 += PenaltyValue
        ///      （可以和障碍物惩罚叠加，允许一个区域同时是难走地形 + 障碍物边缘）
        ///
        /// 最后调用 BlurPenalties 对所有惩罚值做平滑扩散。
        /// </summary>
        private void CheckForObstacles(ASGrid _grid, DynamicBuffer<ASGridNodesBufferComponent> _nodes, LocalTransform _transform, ref SystemState _state)
        {
            // d = max(NodeXCount, NodeYCount)，用于一维索引计算
            // 一维数组索引公式（主轴对齐）：
            //   NodeXCount > NodeYCount（宽>高）→ index = x + y * NodeXCount
            //   NodeYCount >= NodeXCount（高>=宽）→ index = y + x * NodeYCount
            // 统一用 d 代替较大的那个值，公式化简为：
            //   宽>高：x + y * d     高>=宽：y + x * d
            int d = _grid.NodeXCount > _grid.NodeYCount ? _grid.NodeXCount : _grid.NodeYCount;

            for (int x = 0; x < _grid.NodeXCount; x++)
            {
                for (int y = 0; y < _grid.NodeYCount; y++)
                {
                    // 计算当前节点在一维缓冲区中的索引
                    int iindex;
                    if (_grid.NodeXCount > _grid.NodeYCount)
                        iindex = x + y * d;
                    else iindex = y + x * d;

                    // 每次刷新前先将节点还原为默认状态（可通行、无惩罚）
                    // 否则旧障碍物移走后，节点仍保留上次扫描留下的 IsAccessible = false
                    var nn = _nodes[iindex];
                    nn.Node.IsAccessible = true;
                    nn.Node.Penalty = 0;
                    _nodes[iindex] = nn;

                    // ── 获取当前帧的 Physics World ──
                    // PhysicsWorldSingleton 每帧由 Unity Physics 系统更新，
                    // 必须在此处重新查询而不能缓存（缓存的引用在下一帧可能失效）
                    EntityQueryBuilder builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PhysicsWorldSingleton>();
                    EntityQuery singletonQuery = _state.EntityManager.CreateEntityQuery(builder);
                    var physicsWorld = singletonQuery.GetSingleton<PhysicsWorldSingleton>().PhysicsWorld;
                    singletonQuery.Dispose();

                    // ── 构造 OverlapBox 参数 ──
                    // extents：盒子的半边长（等于节点半径），使检测盒与节点格子完全重合
                    // worldCoords：节点中心的世界坐标（GridUtility 将网格坐标转换为世界坐标）
                    float3 extents = new float3(1, 1, 1) * (_grid.NodeSize / 2);
                    float3 worldCoords = GridUtility.GridCoordsToWorld(x, y, _grid, _transform);

                    NativeList<DistanceHit> results = new NativeList<DistanceHit>(Allocator.Temp);

                    // ── OverlapBox 物理查询 ──
                    // 返回值：true = 有碰撞体与盒子相交（即有障碍物），false = 节点区域内无障碍
                    // CollisionFilter 说明：
                    //   BelongsTo = ~0u        → 本次查询不属于任何特定层（全 1 = 与任意层都能交互）
                    //   CollidesWith = 障碍物层 | 惩罚层  → 只检测这两个层上的碰撞体
                    //   GroupIndex = 0         → 不使用分组过滤
                    // 注意：OverlapBox 的 bool 返回值只要 CollidesWith 内任意层有碰撞就返回 true，
                    //       即使命中的碰撞体只是惩罚层而非障碍物层，也会返回 true。
                    //       所以不能直接用返回值决定 IsAccessible，需要再遍历 results 区分
                    bool isAccessible = !physicsWorld.OverlapBox(worldCoords, quaternion.identity, extents, ref results, new CollisionFilter()
                    {
                        BelongsTo = ~0u,
                        CollidesWith = (uint)_grid.InaccessibleLMIndex | (uint)_grid.PenaltyLMIndex,
                        GroupIndex = 0
                    });

                    // ── 处理所有命中的碰撞体 ──
                    for (int i = 0; i < results.Length; i++)
                    {
                        // ── 障碍物处理 ──
                        // 只有碰撞体对应的 Entity 上有 ASObstacle 才标记为不可通行
                        // 此时 isAccessible = false（因为障碍物层命中导致 OverlapBox 返回 true）
                        if (mAllObstacles.HasComponent(results[i].Entity))
                        {
                            int index;
                            if (_grid.NodeXCount > _grid.NodeYCount)
                                index = x + y * d;
                            else index = y + x * d;

                            ASObstacle obstacle = mAllObstacles[results[i].Entity];
                            var newNodes = _nodes[index];
                            // IsAccessible 由 OverlapBox 的 bool 反值决定（有障碍物命中 → false）
                            newNodes.Node.IsAccessible = isAccessible;
                            // PenaltyOnObstacleValue：障碍物边缘惩罚（经过高斯模糊后会向外扩散，
                            // 让路径自然远离障碍物边缘，而不是贴着边走）
                            newNodes.Node.Penalty = obstacle.PenaltyOnObstacleValue;
                            _nodes[index] = newNodes;
                        }

                        // ── 地形惩罚处理 ──
                        // ASPenalty 不影响可通行性（IsAccessible 保持 true），只增加寻路代价
                        // 用 += 叠加，支持一个节点同时被多个惩罚区域覆盖（代价累加）
                        if (mAllPenalties.HasComponent(results[i].Entity))
                        {
                            int index;
                            if (_grid.NodeXCount > _grid.NodeYCount)
                                index = x + y * d;
                            else index = y + x * d;

                            ASPenalty penalty = mAllPenalties[results[i].Entity];
                            var newNodes = _nodes[index];
                            // 叠加：惩罚值 = 原有惩罚（如障碍物边缘）+ 地形惩罚
                            newNodes.Node.Penalty = penalty.PenaltyValue + newNodes.Node.Penalty;
                            _nodes[index] = newNodes;
                        }
                    }

                    results.Dispose();
                }
            }
        }
    }
}
