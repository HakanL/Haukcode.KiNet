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
        //TODO
        //client.OnPacket.Subscribe(d =>
        //{
        //    Listener_OnPacket(d.TimestampMS, d.TimestampMS - last, d.Packet);
        //    last = d.TimestampMS;
        //});
    }

    public async Task Execute()
    {
        Console.WriteLine("Sending DiscoverSupplies packet");

        await this.client.QueuePacket(new DiscoverSuppliesRequest(this.client.LocalEndPoint.Address));
    }

    private async void Listener_OnPacket(double timestampMS, double sinceLast, BasePacket e)
    {
        Console.Write($"+{sinceLast:N2}\t");
        Console.Write($"Packet type {e.GetType().Name}\t");

        switch (e)
        {
            case DiscoverSuppliesResponse reply3:
                Console.Write($"From: {reply3.SourceIP}");

                await this.client.QueuePacket(new DiscoverPortsRequest());
                break;
        }

        Console.WriteLine("");

        var dataPacket = e as DmxOutPacket;
        if (dataPacket == null)
            return;
    }
}
