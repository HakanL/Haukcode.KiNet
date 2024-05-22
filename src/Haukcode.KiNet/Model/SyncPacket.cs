namespace Haukcode.KiNet.Model;

public class SyncPacket : BasePacket
{
    public const ushort HeaderVersion = 0x0002;
    public const ushort HeaderType = 0x0109;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => 21;

    // Probably not correct, but this is what Luminair sends
    public byte[] Payload = new byte[50];

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Payload);
    }

    public SyncPacket()
    {
    }
}
