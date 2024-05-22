namespace Haukcode.KiNet.Model;

public class SupplyReadPropertiesRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0105;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => 12;

    public uint Something1 { get; set; } = 0x000000c0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteUInt32(Something1);
    }

    public SupplyReadPropertiesRequest()
    {
    }

    public SupplyReadPropertiesRequest(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Something1 = reader.ReadUInt32();
    }
}
