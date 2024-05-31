namespace Haukcode.KiNet.Model;

public class DiscoverSuppliesRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0001;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 8;

    public System.Net.IPAddress SourceIP { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteBytes(SourceIP.GetAddressBytes());
    }

    public DiscoverSuppliesRequest(System.Net.IPAddress sourceIP)
    {
        SourceIP = sourceIP;
    }

    public DiscoverSuppliesRequest(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        SourceIP = new System.Net.IPAddress(reader.ReadBytes(4));
    }
}
