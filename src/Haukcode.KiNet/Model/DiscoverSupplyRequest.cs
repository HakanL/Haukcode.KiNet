namespace Haukcode.KiNet.Model;

public class DiscoverSupplyRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x000a;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 5;

    public byte Something1 { get; set; } = 0x11;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteByte(Something1);
    }
}
