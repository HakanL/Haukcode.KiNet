namespace Haukcode.KiNet.Model;

public abstract class BasePacket
{
    public const uint Magic = 0x4adc0104;

    public abstract ushort Version { get; }

    public abstract ushort Type { get; }

    public abstract int Length { get; }

    public uint Sequence { get; set; }

    public int WriteToBuffer(Memory<byte> buffer)
    {
        var writer = new LittleEndianBinaryWriter(buffer);

        writer.WriteUInt32(Magic);
        writer.WriteUInt16(Version);
        writer.WriteUInt16(Type);

        WritePacket(writer);

        return writer.BytesWritten;
    }

    public static BasePacket Parse(ReadOnlyMemory<byte> inputBuffer)
    {
        var reader = new LittleEndianBinaryReader(inputBuffer);

        if (reader.ReadUInt32() != Magic)
            throw new ArgumentException("Invalid magic");

        ushort version = reader.ReadUInt16();
        ushort type = reader.ReadUInt16();

        switch (version << 16 | type)
        {
            case DiscoverSupplies3Response.HeaderVersion << 16 | DiscoverSupplies3Response.HeaderType:
                return new DiscoverSupplies3Response(reader);

            case DiscoverSupply2Response.HeaderVersion << 16 | DiscoverSupply2Response.HeaderType:
                return new DiscoverSupply2Response(reader);

            case DiscoverFixturesChannelResponse.HeaderVersion << 16 | DiscoverFixturesChannelResponse.HeaderType:
                return new DiscoverFixturesChannelResponse(reader);

            case DiscoverFixturesSerialResponse.HeaderVersion << 16 | DiscoverFixturesSerialResponse.HeaderType:
                return new DiscoverFixturesSerialResponse(reader);

            case DiscoverSupplies2Request.HeaderVersion << 16 | DiscoverSupplies2Request.HeaderType:
                return new DiscoverSupplies2Request(reader);

            case DiscoverSupplies3Request.HeaderVersion << 16 | DiscoverSupplies3Request.HeaderType:
                return new DiscoverSupplies3Request(reader);

            case SupplyReadPropertiesRequest.HeaderVersion << 16 | SupplyReadPropertiesRequest.HeaderType:
                return new SupplyReadPropertiesRequest(reader);

            case SupplyWritePropertiesRequest.HeaderVersion << 16 | SupplyWritePropertiesRequest.HeaderType:
                return new SupplyWritePropertiesRequest(reader);

            case DmxOutPacket.HeaderVersion << 16 | DmxOutPacket.HeaderType:
                return new DmxOutPacket(reader);

            case PortOutPacket.HeaderVersion << 16 | PortOutPacket.HeaderType:
                return new PortOutPacket(reader);
        }

        // Not implemented
        return null;
    }

    internal abstract void WritePacket(LittleEndianBinaryWriter writer);
}
