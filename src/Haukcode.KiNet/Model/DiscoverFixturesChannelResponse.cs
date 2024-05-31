namespace Haukcode.KiNet.Model;

public class DiscoverFixturesChannelResponse : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0203;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;


    protected override int PacketPayloadLength => 12;

    public System.Net.IPAddress Address { get; set; }

    public uint Serial { get; set; }

    public ushort Something { get; set; } = 0x4100;

    public byte Channel { get; set; } = 0x00;

    public byte Ok { get; set; } = 0x00;

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteBytes(Address.GetAddressBytes());
        writer.WriteUInt32(Serial);
        writer.WriteUInt16(Something);
        writer.WriteByte(Channel);
        writer.WriteByte(Ok);
    }

    public DiscoverFixturesChannelResponse(LittleEndianBinaryReader reader)
    {
        //TODO
        throw new NotImplementedException();
    }
}
