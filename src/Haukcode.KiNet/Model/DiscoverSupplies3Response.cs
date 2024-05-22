namespace Haukcode.KiNet.Model;

public class DiscoverSupplies3Response : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0002;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    public override int Length => throw new NotImplementedException();

    public byte[] Data { get; set; }

    public string Details { get; set; }

    public string Model { get; set; }

    public System.Net.IPAddress SourceIP { get; set; }

    public byte[] MacAddress { get; set; }

    public uint Serial { get; set; }

    public uint Zero1 { get; set; }

    public ushort Zero2 { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        throw new NotImplementedException();
    }

    public DiscoverSupplies3Response(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        SourceIP = new System.Net.IPAddress(reader.ReadBytes(4));
        MacAddress = reader.ReadBytes(6);
        Data = reader.ReadBytes(2);     // String?
        Serial = reader.ReadUInt32();
        Zero1 = reader.ReadUInt32();

        // Max 60?
        Details = reader.ReadString();
        // Max 31?
        Model = reader.ReadString();
        Zero2 = reader.ReadUInt16();
    }
}
