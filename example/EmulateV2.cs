using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet.ConsoleExample;

// Emulating a v2 device
internal class EmulateV2
{
    private KiNetClient client;

    public EmulateV2(KiNetClient client)
    {
        this.client = client;

        double last = 0;
        client.OnPacket.Subscribe(d =>
        {
            Listener_OnPacket(d.TimestampMS, d.TimestampMS - last, d.Packet);
            last = d.TimestampMS;
        });
    }

    public void Execute()
    {
    }

    private void Listener_OnPacket(double timestampMS, double sinceLast, BasePacket e)
    {
        Console.Write($"+{sinceLast:N2}\t");
        Console.Write($"Packet type {e.GetType().Name}\t");

        switch (e)
        {
            case DiscoverSuppliesRequest request1:
                var response1 = new DiscoverSuppliesResponse(this.client.LocalEndPoint.Address)
                {
                    Details = "M:Hauktest, Inc\nD:Test System",
                    MacAddress = [0x00, 0x01, 0x02, 0x03, 0x04, 0x05],
                    Model = "Test Model",
                    ProtocolVersion = 2,
                    Serial = [0x12, 0x34, 0x56, 0x78, 0, 0, 0, 0]
                };
                this.client.SendPacket(response1);
                break;

            case DiscoverPortsRequest request2:
                var response2 = new DiscoverPortsResponse
                {
                    Ports =
                    [
                        new DiscoverPortsResponse.PortData
                        {
                            Id = 1,
                            Type = DiscoverPortsResponse.PortData.PortTypes.DmxOutput
                        }
                    ]
                };
                this.client.SendPacket(response2);
                break;
        }

        Console.WriteLine("");
    }
}
