# Haukcode.KiNet [![NuGet Version](http://img.shields.io/nuget/v/Haukcode.KiNet.svg?style=flat)](https://www.nuget.org/packages/Haukcode.KiNet/)

A modern, high-performance .NET library for controlling Philips/Signify Color Kinetics lighting systems using the KiNet protocol. Built for .NET 8, 9, and 10.

## What is KiNet?

KiNet is a proprietary protocol developed by Color Kinetics (now part of Signify/Philips) for controlling professional LED lighting fixtures and power supplies over Ethernet. This library provides a complete implementation of both KiNet v1 and v2 protocols, enabling you to:

- Send DMX512 lighting control data over Ethernet
- Discover and configure Color Kinetics power supplies and fixtures
- Synchronize multiple devices for coordinated lighting effects
- Read and write device properties
- Build custom lighting control applications

## Features

- ✅ **KiNet v1 Protocol** - DMXOUT packets for legacy devices
- ✅ **KiNet v2 Protocol** - PORTOUT packets with enhanced features
- ✅ **Device Discovery** - Automatic discovery of power supplies and fixtures on the network
- ✅ **Synchronization** - Sync packets for coordinated lighting effects across multiple devices
- ✅ **Power Supply Management** - Read and write device properties
- ✅ **High Performance** - Built on `Haukcode.HighPerfComm` for efficient UDP communication
- ✅ **Reactive Extensions** - Observable pattern for handling received packets
- ✅ **Multi-Target** - Supports .NET 8, .NET 9, and .NET 10

## Hardware Compatibility

Tested with:
- **PDS-150e** (KiNet v1)
- **sPDS-60ca** (KiNet v1 and v2)

Should work with all Color Kinetics power supplies and fixtures that support the KiNet protocol.

## Installation

Install via NuGet Package Manager:

```bash
dotnet add package Haukcode.KiNet
```

Or via Package Manager Console:

```powershell
Install-Package Haukcode.KiNet
```

## Quick Start

### Basic Setup

```csharp
using Haukcode.KiNet;
using System.Net;

// Get the first available network interface
var networkInterface = Haukcode.Network.Utils.GetFirstBindAddress();

// Create a KiNet client
using var client = new KiNetClient(
    localAddress: networkInterface.IPAddress,
    localSubnetMask: networkInterface.NetMask,
    port: KiNetClient.DefaultPort  // Default is 6038
);

// Handle errors
client.OnError.Subscribe(error => 
{
    Console.WriteLine($"Error: {error.Message}");
});
```

### Sending DMX Data

#### KiNet v1 (DMXOUT)

```csharp
// Prepare DMX data (up to 512 channels)
byte[] dmxData = new byte[512];
dmxData[0] = 255;  // Channel 1 - Red
dmxData[1] = 128;  // Channel 2 - Green
dmxData[2] = 0;    // Channel 3 - Blue

// Send to specific IP address (unicast)
var targetDevice = IPAddress.Parse("192.168.1.100");
await client.SendDmxData(
    address: targetDevice,
    universeId: 1,
    dmxData: dmxData,
    protocolVersion: 1
);

// Send to all devices (broadcast)
await client.SendDmxData(
    address: null,  // null = broadcast
    universeId: 1,
    dmxData: dmxData,
    protocolVersion: 1
);
```

#### KiNet v2 (PORTOUT)

```csharp
byte[] dmxData = new byte[512];
// ... populate DMX data

await client.SendDmxData(
    address: targetDevice,
    universeId: 1,
    dmxData: dmxData,
    startCode: 0,           // DMX start code (default: 0)
    protocolVersion: 2      // Use KiNet v2
);
```

### Synchronizing Devices

Send a sync packet to coordinate timing across multiple devices:

```csharp
// Send sync to specific device
await client.SendSync(IPAddress.Parse("192.168.1.100"));

// Send sync broadcast to all devices
await client.SendSync(null);
```

### Device Discovery

Discover Color Kinetics power supplies and devices on the network:

```csharp
using Haukcode.KiNet.Model;

// Send discovery request
await client.QueuePacket(
    new DiscoverSuppliesRequest(client.LocalEndPoint.Address)
);

// Listen for responses (requires StartReceive)
client.StartReceive();
client.OnPacket.Subscribe(packet => 
{
    if (packet.Packet is DiscoverSuppliesResponse response)
    {
        Console.WriteLine($"Found device at {packet.Source}");
        Console.WriteLine($"  Model: {response.Model}");
        Console.WriteLine($"  Protocol: v{response.ProtocolVersion}");
        Console.WriteLine($"  Details: {response.Details}");
    }
});
```

### Receiving Packets

To receive and process incoming KiNet packets:

```csharp
// Start receiving packets
client.StartReceive();

// Subscribe to received packets
client.OnPacket.Subscribe(receivedPacket => 
{
    Console.WriteLine($"Received at {receivedPacket.TimestampMS}ms");
    Console.WriteLine($"From: {receivedPacket.Source}");
    
    switch (receivedPacket.Packet)
    {
        case DmxOutPacket dmxOut:
            Console.WriteLine($"DMX v1: Universe {dmxOut.UniverseId}, {dmxOut.DMXData.Length} channels");
            break;
            
        case PortOutPacket portOut:
            Console.WriteLine($"DMX v2: Port {portOut.Port}, {portOut.DMXData.Length} channels");
            break;
            
        case DiscoverSuppliesResponse discovery:
            Console.WriteLine($"Device: {discovery.Model}");
            break;
    }
});

// Later, stop receiving
client.StopReceive();
```

## API Reference

### KiNetClient

The main class for communicating with KiNet devices.

#### Constructor

```csharp
KiNetClient(IPAddress localAddress, IPAddress localSubnetMask, int port = 6038)
```

**Parameters:**
- `localAddress` - Local IP address to bind to
- `localSubnetMask` - Subnet mask for broadcast calculations
- `port` - UDP port (default: 6038)

#### Methods

##### SendDmxData

```csharp
Task SendDmxData(
    IPAddress? address, 
    byte universeId, 
    ReadOnlyMemory<byte> dmxData, 
    bool important = false, 
    byte startCode = 0, 
    int protocolVersion = 1
)
```

Sends DMX512 data to devices.

**Parameters:**
- `address` - Target IP address, or `null` for broadcast
- `universeId` - Universe/port identifier (0-255)
- `dmxData` - DMX channel data (up to 512 bytes)
- `important` - Priority flag for the packet
- `startCode` - DMX start code (default: 0)
- `protocolVersion` - 1 for DMXOUT (v1), 2 for PORTOUT (v2)

##### SendSync

```csharp
Task SendSync(IPAddress? destination)
```

Sends a synchronization packet to coordinate device timing.

**Parameters:**
- `destination` - Target IP address, or `null` for broadcast

##### QueuePacket

```csharp
Task QueuePacket(BasePacket packet, IPAddress? destination = null, bool important = false)
```

Queues a custom packet for transmission.

**Parameters:**
- `packet` - The packet to send
- `destination` - Target IP address, or `null` for broadcast
- `important` - Priority flag for the packet

##### StartReceive / StopReceive

```csharp
void StartReceive()
void StopReceive()
```

Start or stop receiving packets from the network.

#### Properties

- `OnPacket` - `IObservable<ReceiveDataPacket>` - Observable stream of received packets
- `OnError` - `IObservable<Exception>` - Observable stream of errors
- `LocalEndPoint` - Local endpoint the client is bound to
- `BroadcastAddress` - Calculated broadcast address for the subnet

### Packet Types

The library supports the following packet types:

**DMX Data:**
- `DmxOutPacket` - KiNet v1 DMX output
- `PortOutPacket` - KiNet v2 DMX output
- `SyncPacket` - Synchronization packet

**Device Discovery:**
- `DiscoverSuppliesRequest` / `DiscoverSuppliesResponse` - Discover power supplies
- `DiscoverSupplies2Request` - Extended discovery request
- `DiscoverSupply2Response` / `DiscoverSupply4Response` - Extended discovery responses
- `DiscoverFixturesChannelRequest` / `DiscoverFixturesChannelResponse` - Discover fixtures by channel
- `DiscoverFixturesSerialRequest` / `DiscoverFixturesSerialResponse` - Discover fixtures by serial
- `DiscoverPortsRequest` / `DiscoverPortsResponse` - Discover available ports

**Configuration:**
- `SupplyReadPropertiesRequest` - Read device properties
- `SupplyWritePropertiesRequest` - Write device properties
- `DiscoverSupplyRequest` - Request supply information

## Protocol Details

### KiNet v1 (DMXOUT)

- **Packet Type:** 0x0101
- **Version:** 0x0001
- **Max Payload:** 512 bytes DMX data
- **Features:** Basic DMX output, universe ID, timing control

### KiNet v2 (PORTOUT)

- **Packet Type:** 0x0108
- **Version:** 0x0002
- **Max Payload:** 512 bytes DMX data
- **Features:** Enhanced port control, start codes, flags

### Network Communication

- **Protocol:** UDP
- **Default Port:** 6038
- **Broadcast Support:** Yes
- **Unicast Support:** Yes
- **Max Packet Size:** 638 bytes

## Examples

See the [example](example/) directory for complete examples:

- **EmulateV2.cs** - Emulate a KiNet v2 device
- **DiscoverMadrix.cs** - Discover devices (similar to MADRIX)
- **Program.cs** - Basic client setup and usage

## Performance Considerations

- The library uses high-performance UDP communication via `Haukcode.HighPerfComm`
- Packets are queued and sent efficiently to minimize network overhead
- Important packets can be prioritized
- Receive buffer size: 20,480 bytes (configurable via `ActualReceiveBufferSize` property)

## Contributing

Contributions are welcome! Please feel free to submit issues or pull requests on [GitHub](https://github.com/HakanL/Haukcode.KiNet).

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Authors

- **Hakan Lindestaf** - Primary developer
- **Jesse Higginson** - Contributor

## Related Projects

- [Haukcode.HighPerfComm](https://github.com/HakanL/Haukcode.HighPerfComm) - High-performance communication library
- [Haukcode.Network](https://github.com/HakanL/Haukcode.Network) - Network utilities

## Support

For issues, questions, or feature requests, please use the [GitHub Issues](https://github.com/HakanL/Haukcode.KiNet/issues) page.
