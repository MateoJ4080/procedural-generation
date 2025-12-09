using Unity.Entities;

public struct CameraConfig : IComponentData
{
    public float Sensitivity;
    public float PitchMin;
    public float PitchMax;
}
