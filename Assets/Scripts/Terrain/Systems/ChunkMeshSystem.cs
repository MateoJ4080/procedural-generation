using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Profiling;

// UpdateAfter to wait for the chunk data
[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial class ChunkMeshSystem : SystemBase
{

    private static readonly ProfilerMarker ApplyMeshMarker = new("ChunkMesh.ApplyMesh");

    private DynamicBuffer<Block> _leftChunkBuffer;
    private DynamicBuffer<Block> _rightChunkBuffer;
    private DynamicBuffer<Block> _backChunkBuffer;
    private DynamicBuffer<Block> _frontChunkBuffer;
    private NativeList<PendingMesh> _pendingMeshes;

    private struct MeshTask
    {
        public Entity Entity;
        public int Priority;
    }

    private Entity _globalChunkDataEntity;

    protected override void OnCreate()
    {
        _pendingMeshes = new NativeList<PendingMesh>(Allocator.Persistent);

        _globalChunkDataEntity = SystemAPI.GetSingletonEntity<ChunksGlobalData>();
    }

    protected override void OnUpdate()
    {
        CompletePendingMesh();
        ScheduleNextJob();
    }

    private void CompletePendingMesh()
    {
        var applySystem = World.GetExistingSystemManaged<ChunkMeshApplySystem>();

        // Loop is backwards so it doesn't miss a switched element after RemoveAtSwapBack
        for (int i = _pendingMeshes.Length - 1; i >= 0; i--)
        {
            var pending = _pendingMeshes[i];

            if (!pending.Handle.IsCompleted)
                continue;

            pending.Handle.Complete();

            ApplyMeshMarker.Begin();

            applySystem.Apply(pending);

            ApplyMeshMarker.End();
            DisposeMeshData(pending);

            _pendingMeshes.RemoveAtSwapBack(i);
        }
    }

    private void DisposeMeshData(PendingMesh pending)
    {
        pending.Blocks.Dispose();

        pending.Vertices.Dispose();
        pending.UVs.Dispose();
        pending.Triangles.Dispose();
        pending.Normals.Dispose();

        if (pending.LeftArray.IsCreated) pending.LeftArray.Dispose();
        if (pending.RightArray.IsCreated) pending.RightArray.Dispose();
        if (pending.FrontArray.IsCreated) pending.FrontArray.Dispose();
        if (pending.BackArray.IsCreated) pending.BackArray.Dispose();
    }

    private void ScheduleNextJob()
    {
        Entity player = SystemAPI.GetSingletonEntity<PlayerTag>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(player).Position;
        int2 playerChunk = new int2((int)(playerPos.x / 16), (int)(playerPos.z / 16));

        // For each entity, create a task to schedule its faces
        System.Collections.Generic.List<MeshTask> tasks = new();

        foreach (var (chunkData, entity) in SystemAPI.Query<RefRO<ChunkData>>()
            .WithEntityAccess()
            .WithNone<MaterialMeshInfo>())
        {
            int2 coord = chunkData.ValueRO.ChunkCoord;

            int dx = coord.x - playerChunk.x;
            int dz = coord.y - playerChunk.y;

            tasks.Add(new MeshTask
            {
                Entity = entity,
                Priority = dx * dx + dz * dz
            });
        }

        tasks.Sort((a, b) => a.Priority.CompareTo(b.Priority));

        int maxJobs = 2; // To-do: Try to raise after optimizations
        int jobsToSchedule = math.min(maxJobs, tasks.Count);

        for (int i = 0; i < jobsToSchedule; i++)
        {
            ScheduleMeshJob(tasks[i].Entity);
        }
    }

    private void ScheduleMeshJob(Entity entity)
    {
        var chunk = SystemAPI.GetComponent<Chunk>(entity);

        var buffer = SystemAPI.GetBuffer<Block>(entity);
        var blocksCopy = new NativeArray<Block>(buffer.Length, Allocator.TempJob);
        blocksCopy.CopyFrom(buffer.AsNativeArray());

        var width = chunk.Width;
        var height = chunk.Height;
        var depth = chunk.Depth;

        // Assign default value so in next iterations it doesn't give an incorrect one
        _leftChunkBuffer = default;
        _rightChunkBuffer = default;
        _backChunkBuffer = default;
        _frontChunkBuffer = default;

        NativeHashMap<int2, Entity> chunksMap = SystemAPI.GetComponent<ChunksGlobalData>(_globalChunkDataEntity).Chunks;
        int2 chunkPos = SystemAPI.GetComponent<ChunkData>(entity).ChunkCoord;
        chunksMap.TryAdd(chunkPos, entity);

        if (chunksMap.TryGetValue(chunkPos + new int2(-1, 0), out var leftChunk))
            _leftChunkBuffer = SystemAPI.GetBuffer<Block>(leftChunk);
        if (chunksMap.TryGetValue(chunkPos + new int2(1, 0), out var rightChunk))
            _rightChunkBuffer = SystemAPI.GetBuffer<Block>(rightChunk);
        if (chunksMap.TryGetValue(chunkPos + new int2(0, -1), out var backChunk))
            _backChunkBuffer = SystemAPI.GetBuffer<Block>(backChunk);
        if (chunksMap.TryGetValue(chunkPos + new int2(0, 1), out var frontChunk))
            _frontChunkBuffer = SystemAPI.GetBuffer<Block>(frontChunk);

        NativeArray<Block> _leftArr = ToNativeArray(_leftChunkBuffer);
        NativeArray<Block> _rightArr = ToNativeArray(_rightChunkBuffer);
        NativeArray<Block> _backArr = ToNativeArray(_backChunkBuffer);
        NativeArray<Block> _frontArr = ToNativeArray(_frontChunkBuffer);

        var vertices = new NativeList<float3>(Allocator.TempJob);
        var uvs = new NativeList<float2>(Allocator.TempJob);
        var triangles = new NativeList<int>(Allocator.TempJob);
        var normals = new NativeList<float3>(Allocator.TempJob);

        var addFacesJob = new AddFacesJob
        {
            BufferAsArray = blocksCopy,

            LeftArray = _leftArr,
            RightArray = _rightArr,
            BackArray = _backArr,
            FrontArray = _frontArr,

            Width = width,
            Height = height,
            Depth = depth,

            Vertices = vertices,
            UVs = uvs,
            Triangles = triangles,
            Normals = normals
        };

        var jobHandle = addFacesJob.Schedule();

        _pendingMeshes.Add(new PendingMesh
        {
            Entity = entity,
            Handle = jobHandle,

            Blocks = blocksCopy,

            Vertices = vertices,
            UVs = uvs,
            Triangles = triangles,
            Normals = normals,

            LeftArray = _leftArr,
            RightArray = _rightArr,
            BackArray = _backArr,
            FrontArray = _frontArr,
        });
    }

    private NativeArray<Block> ToNativeArray(DynamicBuffer<Block> buffer)
    {
        if (!buffer.IsCreated)
            return new NativeArray<Block>(0, Allocator.TempJob);

        var array = new NativeArray<Block>(buffer.Length, Allocator.TempJob);
        array.CopyFrom(buffer.AsNativeArray());
        return array;
    }

    protected override void OnDestroy()
    {
        if (_pendingMeshes.IsCreated) _pendingMeshes.Dispose();
    }
}

