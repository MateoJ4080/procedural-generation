using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Jobs;
using Unity.Transforms;
// UpdateAfter to wait for the chunk data
[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial class ChunkMeshSystem : SystemBase
{
    // "Shared" as with other scripts
    public NativeList<float3> SharedVertices;
    public NativeList<float2> SharedUVs;
    public NativeList<int> SharedTriangles;
    public NativeList<float3> SharedNormals;

    private NativeArray<Block> _emptyBlockArrayL; // Left
    private NativeArray<Block> _emptyBlockArrayR; // Right
    private NativeArray<Block> _emptyBlockArrayB; // Back
    private NativeArray<Block> _emptyBlockArrayF; // Front

    private DynamicBuffer<Block> _leftChunkBuffer;
    private DynamicBuffer<Block> _rightChunkBuffer;
    private DynamicBuffer<Block> _backChunkBuffer;
    private DynamicBuffer<Block> _frontChunkBuffer;

    private NativeArray<Block> _leftArr;
    private NativeArray<Block> _rightArr;
    private NativeArray<Block> _backArr;
    private NativeArray<Block> _frontArr;

    private struct PendingMesh
    {
        public Entity Entity;
        public int Width;
        public int Height;
        public int Depth;
    }
    private struct MeshTask
    {
        public Entity Entity;
        public int Priority;
    }
    /// <summary>
    /// Indicates if there's a job running from the previous frame
    /// </summary>
    private bool _hasPendingJob;
    /// <summary>
    /// Reference to the running job to call Complete() on it
    /// </summary>
    private JobHandle _pendingJobHandle;
    /// <summary>
    /// Stores entity data to use when the job finishes. This is to avoid the mesh and the 
    /// </summary>
    private PendingMesh _pendingMeshData;

    private Entity _globalChunkDataEntity;

    protected override void OnCreate()
    {
        SharedVertices = new NativeList<float3>(Allocator.Persistent);
        SharedUVs = new NativeList<float2>(Allocator.Persistent);
        SharedTriangles = new NativeList<int>(Allocator.Persistent);
        SharedNormals = new NativeList<float3>(Allocator.Persistent);

        _emptyBlockArrayL = new NativeArray<Block>(0, Allocator.Persistent);
        _emptyBlockArrayR = new NativeArray<Block>(0, Allocator.Persistent);
        _emptyBlockArrayB = new NativeArray<Block>(0, Allocator.Persistent);
        _emptyBlockArrayF = new NativeArray<Block>(0, Allocator.Persistent);

        _hasPendingJob = false;

        _globalChunkDataEntity = SystemAPI.GetSingletonEntity<ChunksGlobalData>();
    }

    protected override void OnUpdate()
    {
        CompletePendingMesh();
        DisposeNeighborArrays();
        ScheduleNextJob();
    }

    private void CompletePendingMesh()
    {
        if (_hasPendingJob)
        {
            var applySystem = World.GetExistingSystemManaged<ChunkMeshApplySystem>();
            applySystem.Apply(
                _pendingMeshData.Entity,
                _pendingJobHandle,
                SharedVertices,
                SharedTriangles,
                SharedNormals,
                SharedUVs
            );

            _hasPendingJob = false;
        }
    }

    private void DisposeNeighborArrays()
    {
        if (_leftArr.IsCreated && _leftArr != _emptyBlockArrayL) _leftArr.Dispose();
        if (_rightArr.IsCreated && _rightArr != _emptyBlockArrayR) _rightArr.Dispose();
        if (_backArr.IsCreated && _backArr != _emptyBlockArrayB) _backArr.Dispose();
        if (_frontArr.IsCreated && _frontArr != _emptyBlockArrayF) _frontArr.Dispose();
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

        if (tasks.Count > 0)
        {
            Entity entity = tasks[0].Entity;

            var chunk = SystemAPI.GetComponent<Chunk>(entity);
            var buffer = SystemAPI.GetBuffer<Block>(entity);

            var width = chunk.Width;
            var height = chunk.Height;
            var depth = chunk.Depth;

            SharedNormals.Clear();
            SharedUVs.Clear();
            SharedTriangles.Clear();
            SharedVertices.Clear();

            // Assign default value so in next iterations it doesn't give an incorrect value
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

            _leftArr = ToNativeArray(_leftChunkBuffer, _emptyBlockArrayL);
            _rightArr = ToNativeArray(_rightChunkBuffer, _emptyBlockArrayR);
            _backArr = ToNativeArray(_backChunkBuffer, _emptyBlockArrayB);
            _frontArr = ToNativeArray(_frontChunkBuffer, _emptyBlockArrayF);

            var addFacesJob = new AddFacesJob
            {
                BufferAsArray = buffer.AsNativeArray(),

                LeftArray = _leftArr,
                RightArray = _rightArr,
                BackArray = _backArr,
                FrontArray = _frontArr,

                Width = width,
                Height = height,
                Depth = depth,

                Vertices = SharedVertices,
                UVs = SharedUVs,
                Triangles = SharedTriangles,
                Normals = SharedNormals
            };

            // Schedules job to complete on the next frame
            _pendingJobHandle = addFacesJob.Schedule();

            // Makes sure to wait for the faces before creating the mesh right in the next line
            Dependency = _pendingJobHandle;

            _pendingMeshData = new PendingMesh
            {
                Entity = entity,
                Width = width,
                Height = height,
                Depth = depth
            };

            _hasPendingJob = true;
        }
    }

    private NativeArray<Block> ToNativeArray(DynamicBuffer<Block> buffer, NativeArray<Block> emptyArray)
    {
        if (!buffer.IsCreated)
            return emptyArray;

        var array = new NativeArray<Block>(buffer.Length, Allocator.TempJob);
        array.CopyFrom(buffer.AsNativeArray());
        return array;
    }

    protected override void OnDestroy()
    {
        if (_hasPendingJob)
        {
            _pendingJobHandle.Complete();
        }

        if (SharedVertices.IsCreated) SharedVertices.Dispose();
        if (SharedUVs.IsCreated) SharedUVs.Dispose();
        if (SharedTriangles.IsCreated) SharedTriangles.Dispose();
        if (SharedNormals.IsCreated) SharedNormals.Dispose();

        if (_emptyBlockArrayL.IsCreated) _emptyBlockArrayL.Dispose();
        if (_emptyBlockArrayR.IsCreated) _emptyBlockArrayR.Dispose();
        if (_emptyBlockArrayB.IsCreated) _emptyBlockArrayB.Dispose();
        if (_emptyBlockArrayF.IsCreated) _emptyBlockArrayF.Dispose();
    }
}