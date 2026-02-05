# Haukcode.KiNet

A modern .NET library for controlling Philips/Signify Color Kinetics lighting systems using the KiNet protocol (v1 and v2).

## Quick Start

```csharp
using Haukcode.KiNet;
using System.Net;

// Create a KiNet client
var network = Haukcode.Network.Utils.GetFirstBindAddress();
using var client = new KiNetClient(network.IPAddress, network.NetMask);

// Send DMX data (KiNet v1)
byte[] dmxData = new byte[512];
dmxData[0] = 255;  // Red
dmxData[1] = 128;  // Green
dmxData[2] = 0;    // Blue

await client.SendDmxData(
    address: IPAddress.Parse("192.168.1.100"),
    universeId: 1,
    dmxData: dmxData,
    protocolVersion: 1
);

// Broadcast to all devices
await client.SendDmxData(null, 1, dmxData, protocolVersion: 2);

// Synchronize devices
await client.SendSync(null);
```

## Features

- **KiNet v1 & v2** - DMXOUT and PORTOUT protocol support
- **Device Discovery** - Find Color Kinetics power supplies and fixtures
- **Sync Support** - Coordinate timing across multiple devices
- **High Performance** - Efficient UDP communication
- **.NET 8, 9, 10** - Modern multi-target support

## Compatibility

Tested with PDS-150e (v1) and sPDS-60ca (v1/v2). Compatible with all Color Kinetics devices supporting the KiNet protocol.

## Documentation

For comprehensive documentation, examples, and API reference, visit:

**https://github.com/HakanL/Haukcode.KiNet**

## License

MIT License - See [LICENSE](https://github.com/HakanL/Haukcode.KiNet/blob/main/LICENSE)
