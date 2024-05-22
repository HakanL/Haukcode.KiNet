namespace Haukcode.KiNet.Model;

public class DiscoverSupplies3Request : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0001;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => 12;

    public byte Something { get; set; } = 0x01;

    public byte Something2 { get; set; } = 0x00;

    public ushort Something3 { get; set; } = 0x0000;

    public uint Command { get; set; } = 0xfafa010a;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteByte(Something);
        writer.WriteByte(Something2);
        writer.WriteUInt16(Something3);
        writer.WriteUInt32(Command);
    }

    public DiscoverSupplies3Request()
    {
    }

    public DiscoverSupplies3Request(LittleEndianBinaryReader reader)
    {
        Something = reader.ReadByte();
        Something2 = reader.ReadByte();
        Something3 = reader.ReadUInt16();
        Command = reader.ReadUInt32();
    }
}
