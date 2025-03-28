using UnityEngine;

public class BlockMeshGenerator : MonoBehaviour
{
    [SerializeField] private Block block;
    public Block Block { get; set; }
    [SerializeField] private World world;
    public World World { get; set; }

    private MeshFilter meshFilter;

    void Start()
    {
        if (world == null)
        {
            World = FindFirstObjectByType<World>();
        }
    }

    public void GenerateMesh()
    {
        meshFilter = GetComponent<MeshFilter>();
        Vector3[] vertices = new Vector3[24]; // 6 faces * 4 vertex each face
        int[] triangles = new int[36]; // 6 faces * 2 triangles per face * 3 vertex

        int vertexIndex = 0;
        int triangleIndex = 0;

        Vector3[] cubeVertices = new Vector3[]
        {
            new Vector3(0, 0, 0), // 0
            new Vector3(1, 0, 0), // 1
            new Vector3(1, 1, 0), // 2
            new Vector3(0, 1, 0), // 3
            new Vector3(0, 0, 1), // 4
            new Vector3(1, 0, 1), // 5
            new Vector3(1, 1, 1), // 6
            new Vector3(0, 1, 1)  // 7
        };

        // Directions for each face
        Vector3Int[] directions = new Vector3Int[]
        {
            Vector3Int.up,
            Vector3Int.down,
            Vector3Int.left,
            Vector3Int.right,
            Vector3Int.forward,
            Vector3Int.back
        };

        // Creation of each face
        for (int i = 0; i < directions.Length; i++)
        {
            Vector3Int dir = directions[i];
            if (Block.IsAdjacentBlockSolid(dir, World)) continue;

            Vector3 offset = Block.transform.position;

            int[] faceVertices = GetFaceVertices(i);
            for (int j = 0; j < 4; j++)
            {
                vertices[vertexIndex++] = cubeVertices[faceVertices[j]];
            }

            int[] faceTriangles = GetFaceTriangles();
            for (int j = 0; j < 6; j++)
            {
                triangles[triangleIndex++] = faceTriangles[j] + vertexIndex - 4;
            }
        }

        Mesh mesh = new Mesh()
        {
            vertices = vertices,
            triangles = triangles
        };

        mesh.RecalculateNormals();
        meshFilter.mesh = mesh;
    }

    // Take vertices from the cube by positions in cubeVertices
    int[] GetFaceVertices(int faceIndex)
    {
        switch (faceIndex)
        {
            // *Numbers must reference the cubeVertices in an anti-clockwise order
            case 0: return new int[] { 3, 2, 6, 7 }; // Up
            case 1: return new int[] { 1, 0, 4, 5 }; // Down
            case 2: return new int[] { 4, 0, 3, 7 }; // Left
            case 3: return new int[] { 2, 1, 5, 6 }; // Right
            case 4: return new int[] { 7, 6, 5, 4 }; // Front
            case 5: return new int[] { 0, 1, 2, 3 }; // Back
            default: return new int[] { 0, 0, 0, 0 };
        }
    }

    int[] GetFaceTriangles()
    {
        return new int[]
        {
            0, 2, 1,
            0, 3, 2
        };
    }
}