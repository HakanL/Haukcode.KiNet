using System.IO;

namespace Haukcode.KiNet.Model;

public class DmxOutPacket : BasePacket
{
    public const ushort HeaderVersion = 0x0001;
    public const ushort HeaderType = 0x0101;

    public override ushort Version => HeaderVersion;

    public override ushort Type => HeaderType;

    protected override int PacketPayloadLength => 13 + Math.Max(MinDMXDataLength, DMXData.Length);

    private const ushort MinDMXDataLength = 24;

    public byte Port { get; set; } = 0;

    public byte Flags { get; set; } = 0;

    public ushort Timer { get; set; } = 0;

    public uint UniverseId { get; set; } = 0xffffffff;

    public byte StartCode { get; set; } = 0;

    public ReadOnlyMemory<byte> DMXData { get; set; }

    internal override void WritePacket(LittleEndianBinaryWriter writer)
    {
        writer.WriteUInt32(Sequence);
        writer.WriteByte(Port);
        writer.WriteByte(Flags);
        writer.WriteUInt16(Timer);
        writer.WriteUInt32(UniverseId);
        writer.WriteByte(StartCode);

        writer.WriteBytes(DMXData);

        // Pad if less than 24 bytes
        for (int i = DMXData.Length; i < MinDMXDataLength; i++)
            writer.WriteByte(0);
    }

    public DmxOutPacket(ReadOnlyMemory<byte> data)
    {
        DMXData = data;
    }

    public DmxOutPacket(LittleEndianBinaryReader reader)
    {
        Sequence = reader.ReadUInt32();
        Port = reader.ReadByte();
        Flags = reader.ReadByte();
        Timer = reader.ReadUInt16();
        UniverseId = reader.ReadUInt32();
        StartCode = reader.ReadByte();

        DMXData = reader.ReadBytes();
    }
}
