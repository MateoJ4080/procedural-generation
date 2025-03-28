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
        blocks = new Block[width, height, depth];

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                for (int z = 0; z < depth; z++)
                {
                    Debug.Log($"<color=yellow>Block instantiated at (<color=red>{x}, <color=green>{y}, <color=cyan>{z}<color=yellow>)");
                    GameObject blockObj = Instantiate(blockPrefab, new Vector3(x, y, z), Quaternion.identity, transform);
                    Block block = blockObj.GetComponent<Block>();
                    block.position = new Vector3Int(x, y, z);
                    blocks[x, y, z] = block;

                    if (blockObj.TryGetComponent<BlockMeshGenerator>(out var meshGen))
                    {
                        meshGen.Block = block;
                        meshGen.World = this;
                        meshGen.GenerateMesh();
                    }
                }
            }
        }
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
