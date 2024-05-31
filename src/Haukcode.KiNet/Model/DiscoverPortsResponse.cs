namespace Haukcode.KiNet.Model;

public class DiscoverPortsResponse : BasePacket
{
    public class PortData
    {
        public enum PortTypes
        {
            Unknown = 0,
            DmxOutputBlinkScannable = 1,
            DmxOutput = 2,
            Chromasic = 3,
            XMX = 4,
            RDM = 5,
            sACN = 6,
            ArtNet = 7,
            VirtualPort = 8
        }

        public byte Id { get; set; }

        public PortTypes Type { get; set; }

        public byte[] Extra { get; set; }

        public PortData()
        {
            Extra = new byte[3];
        }

        public PortData(LittleEndianBinaryReader reader)
        {
            Id = reader.ReadByte();
            Type = (PortTypes)reader.ReadInt32();
            Extra = reader.ReadBytes(3);
        }

        internal void WritePacket(LittleEndianBinaryWriter writer)
        {
            writer.WriteByte(Id);
            writer.WriteInt32((int)Type);
            writer.WriteBytes(Extra);
        }
    }

    public const ushort HeaderVersion = 0x0002;
    public const ushort HeaderType = 0x000b;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 8 + (PortCount * 8);

    public int PortCount => Ports.Count;

    public IList<PortData> Ports { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteInt32(PortCount);
        foreach (var port in Ports)
        {
            port.WritePacket(writer);
        }
    }

    public DiscoverPortsResponse()
    {
    }

    public DiscoverPortsResponse(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        int portCount = reader.ReadInt32();

        var ports = new List<PortData>();
        for (int i = 0; i < portCount; i++)
        {
            ports.Add(new PortData(reader));
        }

        Ports = ports;
    }
}
