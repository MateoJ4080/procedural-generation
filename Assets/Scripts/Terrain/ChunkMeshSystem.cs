using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Burst;
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

    protected override void OnUpdate()
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

        // For each entity with the component "Chunk", take its block buffer and schedule job to calculate faces
        System.Collections.Generic.List<MeshTask> tasks = new();

        Entity player = SystemAPI.GetSingletonEntity<PlayerTag>();
        float3 playerPos = SystemAPI.GetComponent<LocalTransform>(player).Position;
        int2 playerChunk = new int2((int)(playerPos.x / 16), (int)(playerPos.z / 16));

        if (_leftArr.IsCreated && _leftArr != _emptyBlockArrayL) _leftArr.Dispose();
        if (_rightArr.IsCreated && _rightArr != _emptyBlockArrayR) _rightArr.Dispose();
        if (_backArr.IsCreated && _backArr != _emptyBlockArrayB) _backArr.Dispose();
        if (_frontArr.IsCreated && _frontArr != _emptyBlockArrayF) _frontArr.Dispose();

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

            SharedNormals.Clear();
            SharedUVs.Clear();
            SharedTriangles.Clear();
            SharedVertices.Clear();

            var width = chunk.Width;
            var height = chunk.Height;
            var depth = chunk.Depth;

            // Assign default value so in next iterations doesn't give an incorrect value
            _leftChunkBuffer = default;
            _rightChunkBuffer = default;
            _backChunkBuffer = default;
            _frontChunkBuffer = default;

            NativeHashMap<int2, Entity> chunksMap = SystemAPI.GetComponent<ChunksGlobalData>(_globalChunkDataEntity).Chunks;
            int2 chunkPos = SystemAPI.GetComponent<ChunkData>(entity).ChunkCoord;

            chunksMap.TryAdd(chunkPos, entity);

            // Take adjacent chunk entities by position from the HashMap
            if (chunksMap.TryGetValue(chunkPos + new int2(-1, 0), out var leftChunk))
                _leftChunkBuffer = SystemAPI.GetBuffer<Block>(leftChunk);
            if (chunksMap.TryGetValue(chunkPos + new int2(1, 0), out var rightChunk))
                _rightChunkBuffer = SystemAPI.GetBuffer<Block>(rightChunk);
            if (chunksMap.TryGetValue(chunkPos + new int2(0, -1), out var backChunk))
                _backChunkBuffer = SystemAPI.GetBuffer<Block>(backChunk);
            if (chunksMap.TryGetValue(chunkPos + new int2(0, 1), out var frontChunk))
                _frontChunkBuffer = SystemAPI.GetBuffer<Block>(frontChunk);

            if (_leftChunkBuffer.IsCreated)
            {
                _leftArr = new NativeArray<Block>(_leftChunkBuffer.Length, Allocator.TempJob);
                _leftArr.CopyFrom(_leftChunkBuffer.AsNativeArray());
            }
            else _leftArr = _emptyBlockArrayL;

            if (_rightChunkBuffer.IsCreated)
            {
                _rightArr = new NativeArray<Block>(_rightChunkBuffer.Length, Allocator.TempJob);
                _rightArr.CopyFrom(_rightChunkBuffer.AsNativeArray());
            }
            else _rightArr = _emptyBlockArrayR;

            if (_backChunkBuffer.IsCreated)
            {
                _backArr = new NativeArray<Block>(_backChunkBuffer.Length, Allocator.TempJob);
                _backArr.CopyFrom(_backChunkBuffer.AsNativeArray());
            }
            else _backArr = _emptyBlockArrayB;

            if (_frontChunkBuffer.IsCreated)
            {
                _frontArr = new NativeArray<Block>(_frontChunkBuffer.Length, Allocator.TempJob);
                _frontArr.CopyFrom(_frontChunkBuffer.AsNativeArray());
            }
            else _frontArr = _emptyBlockArrayF;

            var addFacesJob = new AddFacesJob
            {
                Buffer = buffer.AsNativeArray(),

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

            // Let ChunkMeshData know of the jobHandle so other scripts can wait for the current job before using data
            var data = SystemAPI.GetComponent<ChunksGlobalData>(_globalChunkDataEntity);
            data.currentMeshHandle = _pendingJobHandle;
            SystemAPI.SetComponent(_globalChunkDataEntity, data);
        }
    }

    [BurstCompile]
    public struct AddFacesJob : IJob
    {
        [ReadOnly] public NativeArray<Block> Buffer; // DynamicBuffer can't be used in jobs; NativeArray provides native blittable memory (needed by the Job System)
        public int Width;
        public int Height;
        public int Depth;

        // Adjacent chunks
        public NativeArray<Block> LeftArray;
        public NativeArray<Block> RightArray;
        public NativeArray<Block> BackArray;
        public NativeArray<Block> FrontArray;

        public NativeList<float3> Vertices;
        public NativeList<float2> UVs;
        public NativeList<int> Triangles;
        public NativeList<float3> Normals;

        public void Execute()
        {
            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    for (int z = 0; z < Depth; z++)
                    {
                        int bufferIndex = x + y * Width + z * Width * Height;
                        var block = Buffer[bufferIndex];

                        if (block.Type == 0) continue;

                        bool right = IsAir(x + 1, y, z);
                        bool left = IsAir(x - 1, y, z);
                        bool top = IsAir(x, y + 1, z);
                        bool bottom = IsAir(x, y - 1, z);
                        bool front = IsAir(x, y, z + 1);
                        bool back = IsAir(x, y, z - 1);

                        AddVisibleFaces(new int3(x, y, z), right, left, top, bottom, front, back);
                    }
                }
            }
        }

        bool IsAir(int x, int y, int z)
        {
            int index;

            if (x < 0)
            {
                if (LeftArray.Length == 0) return true;
                index = x + Width + y * Width + z * Width * Height;
                return LeftArray[index].Type == 0;
            }

            if (x >= Width)
            {
                if (RightArray.Length == 0) return true;
                index = x - Width + y * Width + z * Width * Height;
                return RightArray[index].Type == 0;
            }

            if (z < 0)
            {
                if (BackArray.Length == 0) return true;
                index = x + y * Width + (z + Depth) * Width * Height;
                return BackArray[index].Type == 0;
            }

            if (z >= Depth)
            {
                if (FrontArray.Length == 0) return true;
                index = x + y * Width + (z - Depth) * Width * Height;
                return FrontArray[index].Type == 0;
            }

            if (y < 0 || y >= Height) return true;

            index = x + y * Width + z * Width * Height;
            return Buffer[index].Type == 0;
        }

        void AddVisibleFaces(int3 pos, bool right, bool left, bool top, bool bottom, bool front, bool back)
        {
            // "pos" is the position of the vertex at the bottom back left of the block, not its center
            // Vertices of the faces might not be set in sync with the UVs. Verify later.

            int start = Vertices.Length;

            if (right)
            {
                Vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
                Vertices.Add(pos + new float3(1, 1, 0)); // Top left
                Vertices.Add(pos + new float3(1, 1, 1)); // Top right
                Vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.875f, 0.5f));
                UVs.Add(new float2(0.875f, 0.75f));
                UVs.Add(new float2(1, 0.75f));
                UVs.Add(new float2(1, 0.5f));

                AddQuad(start);
                AddNormals(new float3(1, 0, 0));
                start += 4;
            }

            if (left)
            {
                Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
                Vertices.Add(pos + new float3(0, 1, 1)); // Top left
                Vertices.Add(pos + new float3(0, 1, 0)); // Top right
                Vertices.Add(pos + new float3(0, 0, 0)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.875f, 0.5f));
                UVs.Add(new float2(0.875f, 0.75f));
                UVs.Add(new float2(1, 0.75f));
                UVs.Add(new float2(1, 0.5f));

                AddQuad(start);
                AddNormals(new float3(-1, 0, 0));
                start += 4;
            }

            if (top)
            {
                Vertices.Add(pos + new float3(0, 1, 0)); // Bottom left of this face
                Vertices.Add(pos + new float3(0, 1, 1)); // Top left
                Vertices.Add(pos + new float3(1, 1, 1)); // Top right
                Vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.5f, 0.75f));
                UVs.Add(new float2(0.5f, 1));
                UVs.Add(new float2(0.625f, 1));
                UVs.Add(new float2(0.625f, 0.75f));

                AddQuad(start);
                AddNormals(new float3(0, 1, 0));
                start += 4;
            }

            if (bottom)
            {
                Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
                Vertices.Add(pos + new float3(0, 0, 0)); // Top left
                Vertices.Add(pos + new float3(1, 0, 0)); // Top right
                Vertices.Add(pos + new float3(1, 0, 1)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.875f, 0.5f));
                UVs.Add(new float2(0.875f, 0.75f));
                UVs.Add(new float2(1, 0.75f));
                UVs.Add(new float2(1, 0.5f));

                AddQuad(start);
                AddNormals(new float3(0, -1, 0));
                start += 4;
            }

            if (front)
            {
                Vertices.Add(pos + new float3(0, 0, 1)); // Bottom left of this face
                Vertices.Add(pos + new float3(1, 0, 1)); // Top left
                Vertices.Add(pos + new float3(1, 1, 1)); // Top right
                Vertices.Add(pos + new float3(0, 1, 1)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.875f, 0.5f));
                UVs.Add(new float2(0.875f, 0.75f));
                UVs.Add(new float2(1, 0.75f));
                UVs.Add(new float2(1, 0.5f));

                AddQuad(start);
                AddNormals(new float3(0, 0, 1));
                start += 4;
            }

            if (back)
            {
                Vertices.Add(pos + new float3(1, 0, 0)); // Bottom left of this face
                Vertices.Add(pos + new float3(0, 0, 0)); // Top left
                Vertices.Add(pos + new float3(0, 1, 0)); // Top right
                Vertices.Add(pos + new float3(1, 1, 0)); // Bottom right

                // *Has to be in same order as vertices to have the right orientation*
                UVs.Add(new float2(0.875f, 0.5f));
                UVs.Add(new float2(0.875f, 0.75f));
                UVs.Add(new float2(1, 0.75f));
                UVs.Add(new float2(1, 0.5f));

                AddQuad(start);
                AddNormals(new float3(0, 0, -1));
            }
        }


        private void AddQuad(int start)
        {
            Triangles.Add(start + 0);
            Triangles.Add(start + 1);
            Triangles.Add(start + 2);
            Triangles.Add(start + 0);
            Triangles.Add(start + 2);
            Triangles.Add(start + 3);
        }

        private void AddNormals(float3 normal)
        {
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
            Normals.Add(normal);
        }
    }
}