using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet.ConsoleExample;

// Emulating what Madrix does when searching for devices
internal class DiscoverMadrix
{
    private KiNetClient client;

    public DiscoverMadrix(KiNetClient client)
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
        Console.WriteLine("Sending DiscoverSupplies packet");

        this.client.SendPacket(new DiscoverSuppliesRequest(this.client.LocalEndPoint.Address));
    }

    private void Listener_OnPacket(double timestampMS, double sinceLast, BasePacket e)
    {
        Console.Write($"+{sinceLast:N2}\t");
        Console.Write($"Packet type {e.GetType().Name}\t");

        switch (e)
        {
            case DiscoverSuppliesResponse reply3:
                Console.Write($"From: {reply3.SourceIP}");

                this.client.SendPacket(new DiscoverPortsRequest());
                break;
        }

        Console.WriteLine("");

        var dataPacket = e as DmxOutPacket;
        if (dataPacket == null)
            return;
    }
}
