namespace Haukcode.KiNet.Model;

public class DiscoverFixturesSerialResponse : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0201;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 8;

    public System.Net.IPAddress Address { get; set; }

    public uint Serial { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Address.GetAddressBytes());
        writer.WriteUInt32(Serial);
    }

    public DiscoverFixturesSerialResponse(LittleEndianBinaryReader reader)
    {
        Address = new System.Net.IPAddress(reader.ReadBytes(4));
        Serial = reader.ReadUInt32();
    }
}
