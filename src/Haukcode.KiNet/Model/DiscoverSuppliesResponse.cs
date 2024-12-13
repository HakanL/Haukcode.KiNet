namespace Haukcode.KiNet.Model;

public class DiscoverSuppliesResponse : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0002;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 24 + Details.Length + 1 + Model.Length + 1 + 2 + 8;

    public string Details { get; set; }

    public string Model { get; set; }

    // IPv4
    public System.Net.IPAddress SourceIP { get; private set; }

    // 6 bytes
    public byte[] MacAddress { get; set; }

    // 8 bytes
    public byte[] Serial { get; set; }

    public ushort ProtocolVersion { get; set; }

    public ushort Something { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteBytes(SourceIP.GetAddressBytes());
        writer.WriteBytes(MacAddress);
        writer.WriteUInt16(ProtocolVersion);
        writer.WriteBytes(Serial);

        writer.WriteBytes(System.Text.Encoding.UTF8.GetBytes(Details));
        writer.WriteByte(0);

        writer.WriteBytes(System.Text.Encoding.UTF8.GetBytes(Model));
        writer.WriteByte(0);

        writer.WriteUInt16(Something);

        writer.WriteZeros(8);
    }

    public DiscoverSuppliesResponse(System.Net.IPAddress sourceIP)
    {
        SourceIP = sourceIP;
    }

    public DiscoverSuppliesResponse(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        SourceIP = new System.Net.IPAddress(reader.ReadBytes(4));
        MacAddress = reader.ReadBytes(6);
        ProtocolVersion = reader.ReadUInt16();
        Serial = reader.ReadBytes(8);

        // Max 60?
        Details = reader.ReadString();
        // Max 31?
        Model = reader.ReadString();
        Something = reader.ReadUInt16();

        // sPDS-60ca has 8 bytes, PDS-150e has 22 more bytes
        var extra = reader.ReadBytes();
    }
}
