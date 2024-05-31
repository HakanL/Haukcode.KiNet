namespace Haukcode.KiNet.Model;

public class DiscoverSupply4Response : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x000b;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => throw new NotImplementedException();

    public uint Something1 { get; set; } = 0xf00000c0;
    
    public uint Something2 { get; set; } = 0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        throw new NotImplementedException();
    }

    public DiscoverSupply4Response(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Something1 = reader.ReadUInt32();
        Something2 = reader.ReadUInt32();
    }
}
