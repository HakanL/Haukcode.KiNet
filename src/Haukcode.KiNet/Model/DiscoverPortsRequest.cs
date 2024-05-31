namespace Haukcode.KiNet.Model;

public class DiscoverPortsRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0002;
    public const ushort HeaderType = 0x000a;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 8;

    public uint Something1 { get; set; } = 0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteUInt32(Something1);
    }

    public DiscoverPortsRequest()
    {
    }

    public DiscoverPortsRequest(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Something1 = reader.ReadUInt32();
    }
}
