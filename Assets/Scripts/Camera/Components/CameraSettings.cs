using Unity.Entities;

// Created because player has to rotate same to the camera
public struct CameraSettings : IComponentData
{
    public float Sensitivity;
}
