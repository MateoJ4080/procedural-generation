using UnityEngine;

public class World : MonoBehaviour
{
    public int width = 10, height = 5, depth = 10;
    public GameObject blockPrefab;
    public Block[,,] blocks;

    void Start()
    {
        GenerateWorld();
    }

    void GenerateWorld()
    {
        float noiseScale = 0.1f;
        int maxHeight = height;

        blocks = new Block[width, height, depth];

        // **1. Crear bloques sin generar la malla**
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < depth; z++)
            {
                int terrainHeight = Mathf.FloorToInt(Mathf.PerlinNoise(x * noiseScale, z * noiseScale) * maxHeight);

                for (int y = 0; y < height; y++)
                {
                    GameObject blockObj = Instantiate(blockPrefab, new Vector3(x, y, z), Quaternion.identity, transform);
                    Block block = blockObj.GetComponent<Block>();
                    block.position = new Vector3Int(x, y, z);
                    block.blockType = y <= terrainHeight ? Block.BlockType.Solid : Block.BlockType.Air;
                    blocks[x, y, z] = block;
                }
            }
        }

        // **2. Generar las mallas**
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    Block block = blocks[x, y, z];
                    if (block.blockType == Block.BlockType.Solid && block.TryGetComponent<BlockMeshGenerator>(out var meshGen))
                    {
                        meshGen.Block = block;
                        meshGen.World = this;
                        meshGen.GenerateMesh();
                    }
                }
            }
        }
    }

    public void RefreshAdjacentBlocks()
    {

    }

    public Block GetBlockAt(Vector3Int position)
    {
        if (position.x >= 0 && position.x < width &&
            position.y >= 0 && position.y < height &&
            position.z >= 0 && position.z < depth)
        {
            return blocks[position.x, position.y, position.z];
        }
        return null;
    }
}
