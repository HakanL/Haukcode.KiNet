namespace Haukcode.KiNet.Model;

public class DiscoverSuppliesRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0001;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => 12;

    public uint Command { get; set; } = 0x8901a8c0;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteUInt32(Command);
    }
}
