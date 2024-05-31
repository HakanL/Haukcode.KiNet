using System.Diagnostics;

namespace Haukcode.KiNet.Model;

public class PortOutPacket : BasePacket
{
    public const ushort HeaderVersion = 0x0002;
    public const ushort HeaderType = 0x0108;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 16 + DMXData.Length;

    private const ushort MinDMXDataLength = 24;

    public uint Universe { get; set; } = 0xffffffff;

    public byte Port { get; set; } = 0;

    public byte Padding1 { get; set; } = 0;

    public ushort PortOutFlags { get; set; } = 0x0000;

    public ushort DataLength { get; set; } = 0x0000;

    public ushort StartCode { get; set; }

    public ReadOnlyMemory<byte> DMXData { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteUInt32(Universe);
        writer.WriteByte(Port);
        writer.WriteByte(Padding1);
        writer.WriteUInt16(PortOutFlags);

        ushort dataLength = Math.Max(MinDMXDataLength, DataLength);
        writer.WriteUInt16(dataLength);
        writer.WriteUInt16(StartCode);

        writer.WriteBytes(DMXData);

        // Pad if less than 24 bytes
        for (int i = DMXData.Length; i < MinDMXDataLength; i++)
            writer.WriteByte(0);
    }

    public PortOutPacket(byte port, ReadOnlyMemory<byte> data, byte startCode = 0)
    {
        Port = port;
        DMXData = data;
        DataLength = (ushort)data.Length;
        StartCode = startCode;
    }

    public PortOutPacket(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Universe = reader.ReadUInt32();
        Port = reader.ReadByte();
        Padding1 = reader.ReadByte();
        PortOutFlags = reader.ReadUInt16();
        DataLength = reader.ReadUInt16();
        StartCode = reader.ReadUInt16();

        Debug.Assert(DataLength == reader.BytesLeft);

        DMXData = reader.ReadBytes();
    }
}
