using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Burst;
using Unity.Jobs;

// UpdateAfter to wait for the chunk data
[UpdateAfter(typeof(ChunkGenerationSystem))]
public partial class ChunkMeshSystem : SystemBase
{
    [SerializeField] private Material _sharedMaterial;

    public NativeList<float3> SharedVertices;
    public NativeList<float2> SharedUVs;
    public NativeList<int> SharedTriangles;
    public NativeList<float3> SharedNormals;

    bool debugObjectInstantiated = false;
    private struct PendingMesh
    {
        public Entity Entity;
        public int Width;
        public int Height;
        public int Depth;
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

    protected override void OnCreate()
    {
        SharedVertices = new NativeList<float3>(Allocator.Persistent);
        SharedUVs = new NativeList<float2>(Allocator.Persistent);
        SharedTriangles = new NativeList<int>(Allocator.Persistent);
        SharedNormals = new NativeList<float3>(Allocator.Persistent);

        _sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        var atlas = Resources.Load<Texture2D>("WSUUw");
        _sharedMaterial.mainTexture = atlas;

        if (atlas == null) Debug.Log("Atlas not found");
        if (_sharedMaterial == null) Debug.LogError("Shader not found");

        _hasPendingJob = false;
    }

    protected override void OnDestroy()
    {
        if (_hasPendingJob)
        {
            _pendingJobHandle.Complete();
        }

        if (_sharedMaterial != null)
        {
            Object.Destroy(_sharedMaterial);
        }

        if (SharedVertices.IsCreated) SharedVertices.Dispose();
        if (SharedUVs.IsCreated) SharedUVs.Dispose();
        if (SharedTriangles.IsCreated) SharedTriangles.Dispose();
        if (SharedNormals.IsCreated) SharedNormals.Dispose();
    }
    protected override void OnUpdate()
    {
        if (_hasPendingJob)
        {
            // Complete job to have faces
            _pendingJobHandle.Complete();
            Debug.Log($"Job complete: Vertices: {SharedVertices.Length}, Triangles: {SharedTriangles.Length}, Normals: {SharedNormals.Length}, UVs: {SharedUVs.Length}");

            if (SharedVertices.Length > 0)
            {
                // Generate mesh
                var mesh = new Mesh();
                mesh.name = $"ChunkMesh_{_pendingMeshData.Entity.Index}";
                mesh.SetVertices(SharedVertices.AsArray());
                mesh.SetTriangles(SharedTriangles.AsArray().ToArray(), 0);
                mesh.SetNormals(SharedNormals.AsArray());
                mesh.SetUVs(0, SharedUVs.AsArray());

                var desc = new RenderMeshDescription(
                    shadowCastingMode: ShadowCastingMode.Off,
                    receiveShadows: false);

                var renderMeshArray = new RenderMeshArray(new[] { _sharedMaterial }, new[] { mesh });
                RenderMeshUtility.AddComponents(_pendingMeshData.Entity, EntityManager, desc, renderMeshArray, MaterialMeshInfo.FromRenderMeshArrayIndices(0, 0));

                // If LocalTransform already exists, refresh
                if (EntityManager.HasComponent<Unity.Transforms.LocalTransform>(_pendingMeshData.Entity))
                {
                    var transform = EntityManager.GetComponentData<Unity.Transforms.LocalTransform>(_pendingMeshData.Entity);
                    EntityManager.SetComponentData(_pendingMeshData.Entity, transform);
                }

                // EntityCommandBuffer
                var ecb = new EntityCommandBuffer(Allocator.Temp);
                ecb.SetName(_pendingMeshData.Entity, "ChunkMeshS_" + _pendingMeshData.Entity.Index);
                ecb.Playback(EntityManager);
                ecb.Dispose();

                if (!debugObjectInstantiated)
                {
                    Debug.Log("Generating debug GameObject");
                    var go = new GameObject("DebugMesh", typeof(MeshFilter), typeof(MeshRenderer));
                    go.GetComponent<MeshFilter>().sharedMesh = mesh;
                    go.GetComponent<MeshRenderer>().sharedMaterial = _sharedMaterial;
                    go.transform.position = new Vector3(0, 0, 25f);
                    debugObjectInstantiated = true;
                }

                mesh.RecalculateBounds();
            }
            _hasPendingJob = false;
        }

        // For each entity with the component "Chunk" and buffer of blocks, calculate faces with a job and generate mesh
        foreach (var (chunk, buffer, entity) in SystemAPI.Query<RefRO<Chunk>, DynamicBuffer<Block>>()
            .WithEntityAccess()
            .WithNone<MaterialMeshInfo>())
        {
            SharedNormals.Clear();
            SharedUVs.Clear();
            SharedTriangles.Clear();
            SharedVertices.Clear();

            var width = chunk.ValueRO.Width;
            var height = chunk.ValueRO.Height;
            var depth = chunk.ValueRO.Depth;

            var addFacesJob = new AddFacesJob
            {
                Buffer = buffer.AsNativeArray(),

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

            break;
        }
    }

    [BurstCompile]
    public struct AddFacesJob : IJob
    {
        [ReadOnly] public NativeArray<Block> Buffer; // DynamicBuffer can't be used in jobs; NativeArray provides native blittable memory (needed by the Job System)
        public int Width;
        public int Height;
        public int Depth;

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
                        int index = x + y * Width + z * Width * Height;
                        var block = Buffer[index];

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
            if (x < 0 || y < 0 || z < 0 || x >= Width || y >= Height || z >= Depth)
                return true;

            int index = x + y * Width + z * Width * Height;
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