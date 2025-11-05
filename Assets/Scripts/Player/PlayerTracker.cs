using UnityEngine;
using Unity.Entities;
using System;
using Unity.Mathematics;

public struct PlayerTracker : IComponentData
{
    public bool exists;
    public float3 playerPosition;
}
