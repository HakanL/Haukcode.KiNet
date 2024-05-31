namespace Haukcode.KiNet.Model;

public class DiscoverFixturesChannelRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0303;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 10;

    public System.Net.IPAddress Address { get; set; }

    public uint Serial { get; set; }

    public ushort Something { get; set; } = 0x4100;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Address.GetAddressBytes());
        writer.WriteUInt32(Serial);
        writer.WriteUInt16(Something);
    }

    public DiscoverFixturesChannelRequest(System.Net.IPAddress address)
    {
        Address = address;
    }
}
