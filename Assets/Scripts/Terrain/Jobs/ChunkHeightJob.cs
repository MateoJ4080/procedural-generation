
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public struct ChunkHeightJob : IJob
{
    public int2 chunkCoord;
    public TerrainConfig config;
    public NativeArray<Block> blocks;

    public void Execute()
    {
        for (int x = 0; x < 16; x++)
        {
            for (int z = 0; z < 16; z++)
            {
                float nx = (x + chunkCoord.x * 16) * config.Frequency;
                float nz = (z + chunkCoord.y * 16) * config.Frequency;

                float height = 0f;
                float amp = config.Amplitude;
                float freq = 1f;

                for (int o = 0; o < config.Octaves; o++)
                {
                    height += amp * noise.snoise(new float2(nx * freq, nz * freq));
                    amp *= config.Persistence;
                    freq *= config.Lacunarity;
                }

                height = height * 0.5f + 0.5f;
                int maxY = (int)math.floor(height * 16);

                for (int y = 0; y < 16; y++)
                {
                    int index = x + y * 16 + z * 16 * 16;
                    blocks[index] = new Block { Type = (byte)(y <= maxY ? 1 : 0) };
                }
            }
        }
    }
}