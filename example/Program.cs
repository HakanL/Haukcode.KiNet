using Haukcode.KiNet;
using Haukcode.KiNet.Model;
using System;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Threading;

namespace Haukcode.KiNet.ConsoleExample
{
    public class Program
    {
        private static KiNetClient client;

        public static void Main(string[] args)
        {
            Listen();
        }

        static void Listen()
        {
            var firstAddr = Haukcode.Network.Helper.GetFirstBindAddress();

            client = new KiNetClient(
                localAddress: firstAddr.IPAddress,
                localSubnetMask: firstAddr.NetMask,
                bindAddress: IPAddress.Any);

            client.OnError.Subscribe(e =>
            {
                Console.WriteLine($"Error! {e.Message}");
            });

            double last = 0;
            client.OnPacket.Subscribe(d =>
            {
                Listener_OnPacket(d.TimestampMS, d.TimestampMS - last, d.Packet);
                last = d.TimestampMS;
            });

            client.StartReceive();

            Console.WriteLine("Sending DiscoverSupplies packet");

            // Send 10 of the discover packets
            for (int i = 0; i < 10; i++)
                client.SendPacket(new DiscoverSupplies2Request());

            client.SendPacket(new DiscoverSupplies3Request());
            /*            while (true)
                        {
                            client.SendDmxBroadcast(1, new byte[] { 1, 2, 3, 4, 5 });

                            Thread.Sleep(500);
                        }*/

            Console.ReadLine();

            client.Dispose();
        }

        private static void Listener_OnPacket(double timestampMS, double sinceLast, BasePacket e)
        {
            Console.Write($"+{sinceLast:N2}\t");
            Console.Write($"Packet type {e.GetType().Name}\t");

            switch (e)
            {
                case DiscoverSupplies3Response reply3:
                    Console.Write($"From: {reply3.SourceIP}");

                    client.SendPacket(new DiscoverSupplyRequest());
                    client.SendPacket(new SupplyReadPropertiesRequest());
                    break;
            }

            Console.WriteLine("");

            var dataPacket = e as DmxOutPacket;
            if (dataPacket == null)
                return;
        }
    }
}
