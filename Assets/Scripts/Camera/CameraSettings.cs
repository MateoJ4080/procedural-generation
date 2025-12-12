using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

// Created because player has to rotate same to the camera
public struct CameraSettings : IComponentData
{
    public float Sensitivity;
}
