namespace Haukcode.KiNet.Model;

public class SupplyWritePropertiesRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0103;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => throw new NotImplementedException();

    public uint Something1 { get; set; } = 0x000000c0;

    public uint Something2 { get; set; } = 0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        throw new NotImplementedException();
    }

    public SupplyWritePropertiesRequest(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Something1 = reader.ReadUInt32();
        Something2 = reader.ReadUInt32();
    }
}
