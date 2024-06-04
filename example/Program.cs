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

            //var test = new DiscoverMadrix(client);
            var test = new EmulateV2(client);

            client.StartReceive();

            test.Execute();
            /*            while (true)
                        {
                            client.SendDmxBroadcast(1, new byte[] { 1, 2, 3, 4, 5 });

                            Thread.Sleep(500);
                        }*/

            Console.ReadLine();

            client.Dispose();
        }
    }
}
