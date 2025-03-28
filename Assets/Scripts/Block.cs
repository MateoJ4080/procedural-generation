using UnityEditor.Experimental.GraphView;
using UnityEngine;

public class Block : MonoBehaviour
{
    public enum BlockType { Air, Solid }

    public BlockType blockType = BlockType.Solid;

    // World coordinates    
    public Vector3Int position;

    public bool IsAdjacentBlockSolid(Vector3Int direction, World world)
    {
        Block adjacentBlock = world.GetBlockAt(position + direction);
        return adjacentBlock != null && adjacentBlock.blockType == BlockType.Solid;
    }
}