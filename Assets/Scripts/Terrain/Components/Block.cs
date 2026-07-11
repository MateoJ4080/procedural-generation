using Unity.Entities;

public struct Block : IBufferElementData
{
    public byte Type;
    public bool Visible;
}