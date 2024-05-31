namespace Haukcode.KiNet.Model;

public class SyncPacket : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0109;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 8;

    public byte[] Payload = new byte[8];

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Payload);
    }

    public SyncPacket()
    {
    }
}
