namespace Haukcode.KiNet.Model;

public class DiscoverSupply2Response : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0106;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => throw new NotImplementedException();

    public uint Something1 { get; set; } = 0xf00000c0;
    
    public uint Something2 { get; set; } = 0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        throw new NotImplementedException();
    }

    public DiscoverSupply2Response(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Something1 = reader.ReadUInt32();
        Something2 = reader.ReadUInt32();
    }
}
