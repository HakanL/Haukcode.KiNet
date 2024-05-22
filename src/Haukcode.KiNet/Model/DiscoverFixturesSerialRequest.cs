namespace Haukcode.KiNet.Model;

public class DiscoverFixturesSerialRequest : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0201;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => 8;

    public System.Net.IPAddress Address { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Address.GetAddressBytes());
    }

    public DiscoverFixturesSerialRequest(System.Net.IPAddress address)
    {
        Address = address;
    }
}
