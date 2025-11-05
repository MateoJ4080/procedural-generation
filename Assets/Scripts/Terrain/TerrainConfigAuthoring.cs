using Unity.Entities;
using UnityEngine;

public class TerrainConfigAuthoring : MonoBehaviour
{
    public float frequency = 0.03f;
    public float amplitude = 0.2f;
    public int octaves = 3;
    public float persistence = 0.5f;
    public float lacunarity = 2f;

    class Baker : Baker<TerrainConfigAuthoring>
    {
        public override void Bake(TerrainConfigAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new TerrainConfig
            {
                Frequency = authoring.frequency,
                Amplitude = authoring.amplitude,
                Octaves = authoring.octaves,
                Persistence = authoring.persistence,
                Lacunarity = authoring.lacunarity
            });
        }
    }
}

public struct TerrainConfig : IComponentData
{
    public float Frequency;
    public float Amplitude;
    public int Octaves;
    public float Persistence;
    public float Lacunarity;
}