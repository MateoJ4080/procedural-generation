using System.IO.Compression;
using Unity.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    public int width = 16;  // Terrain width
    public int depth = 16;  // Terrain depth
    public int height = 10;  // Maximum height of the terrain
    public float scale = 0.1f;  // Noise scale

    public GameObject cubePrefab;

    void Start()
    {
        GenerateTerrain();
    }

    void GenerateTerrain()
    {
    }
}
