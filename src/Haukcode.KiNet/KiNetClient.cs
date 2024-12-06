using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using Haukcode.HighPerfComm;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet
{
    public class KiNetClient : Client<KiNetClient.SendData, SocketReceiveMessageFromResult>
    {
        public const int DefaultPort = 6038;

        public class SendData : HighPerfComm.SendData
        {
            public IPEndPoint Destination { get; set; } = null!;
        }

        private const int ReceiveBufferSize = 20480;
        private const int SendBufferSize = 1400;
        private static readonly IPEndPoint _blankEndpoint = new(IPAddress.Any, 0);

        private readonly Socket socket;
        private readonly ISubject<ReceiveDataPacket> packetSubject;
        private readonly Dictionary<IPAddress, IPEndPoint> endPointCache = [];
        private IPEndPoint broadcastEndPoint;
        private uint sequenceCounter;
        private readonly IPEndPoint localEndPoint;

        public KiNetClient(IPAddress localAddress, IPAddress localSubnetMask, IPAddress? bindAddress = null)
            : base(() => new SendData(), ReceiveBufferSize)
        {
            this.packetSubject = new Subject<ReceiveDataPacket>();

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.socket.ReceiveBufferSize = ReceiveBufferSize;
            this.socket.SendBufferSize = SendBufferSize;

            // Set the SIO_UDP_CONNRESET ioctl to true for this UDP socket. If this UDP socket
            //    ever sends a UDP packet to a remote destination that exists but there is
            //    no socket to receive the packet, an ICMP port unreachable message is returned
            //    to the sender. By default, when this is received the next operation on the
            //    UDP socket that send the packet will receive a SocketException. The native
            //    (Winsock) error that is received is WSAECONNRESET (10054). Since we don't want
            //    to wrap each UDP socket operation in a try/except, we'll disable this error
            //    for the socket with this ioctl call.
            try
            {
                uint IOC_IN = 0x80000000;
                uint IOC_VENDOR = 0x18000000;
                uint SIO_UDP_CONNRESET = IOC_IN | IOC_VENDOR | 12;

                byte[] optionInValue = { Convert.ToByte(false) };
                byte[] optionOutValue = new byte[4];
                this.socket.IOControl((int)SIO_UDP_CONNRESET, optionInValue, optionOutValue);
            }
            catch
            {
                Debug.WriteLine("Unable to set SIO_UDP_CONNRESET, maybe not supported.");
            }

            this.socket.ExclusiveAddressUse = false;
            this.socket.EnableBroadcast = true;
            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);

            this.localEndPoint = new IPEndPoint(localAddress, DefaultPort);
            this.broadcastEndPoint = new IPEndPoint(GetBroadcastAddress(localAddress, localSubnetMask), this.localEndPoint.Port);

            // Linux wants Any to get multicast/broadcast packets
            this.socket.Bind(new IPEndPoint(bindAddress ?? localAddress, this.localEndPoint.Port));

            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        }

        public IPEndPoint LocalEndPoint => this.localEndPoint;

        /// <summary>
        /// Observable that provides all parsed packets. This is buffered on its own thread so the processing can
        /// take any time necessary (memory consumption will go up though, there is no upper limit to amount of data buffered).
        /// </summary>
        public IObservable<ReceiveDataPacket> OnPacket => this.packetSubject.AsObservable();

        protected override bool SupportsTwoReceivers => false;

        private static IPAddress GetBroadcastAddress(IPAddress address, IPAddress subnetMask)
        {
            byte[] ipAdressBytes = address.GetAddressBytes();
            byte[] subnetMaskBytes = subnetMask.GetAddressBytes();

            if (ipAdressBytes.Length != subnetMaskBytes.Length)
                throw new ArgumentException("Lengths of IP address and subnet mask do not match.");

            byte[] broadcastAddress = new byte[ipAdressBytes.Length];
            for (int i = 0; i < broadcastAddress.Length; i++)
            {
                broadcastAddress[i] = (byte)(ipAdressBytes[i] | (subnetMaskBytes[i] ^ 255));
            }
            return new IPAddress(broadcastAddress);
        }

        /// <summary>
        /// Unicast send data
        /// </summary>
        /// <param name="address">The address to unicast to</param>
        /// <param name="universeId">The Universe ID</param>
        /// <param name="dmxData">Up to 512 bytes of DMX data</param>
        /// <param name="startCode">Start code (default 0)</param>
        public Task SendDmxData(IPAddress? address, ushort universeId, ReadOnlyMemory<byte> dmxData, bool important = false, byte startCode = 0, int protocolVersion = 1)
        {
            BasePacket packet = protocolVersion switch
            {
                1 => new DmxOutPacket(dmxData),
                2 => new PortOutPacket((byte)universeId, dmxData, startCode),
                _ => throw new NotImplementedException(),
            };

            return QueuePacket(packet, address, important: important);
        }

        /// <summary>
        /// Unicast send sync
        /// </summary>
        /// <param name="destination">Destination</param>
        public Task SendSync(IPAddress? destination)
        {
            var packet = new SyncPacket();

            return QueuePacket(packet, destination, important: true);
        }

        /// <summary>
        /// Send packet
        /// </summary>
        /// <param name="destination">Destination</param>
        /// <param name="packet">Packet</param>
        public async Task QueuePacket(BasePacket packet, IPAddress? destination = null, bool important = false)
        {
            packet.Sequence = Interlocked.Increment(ref this.sequenceCounter);

            await base.QueuePacket(packet.Length, important, (newSendData, memory) =>
            {
                if (destination != null)
                {
                    if (!this.endPointCache.TryGetValue(destination, out var ipEndPoint))
                    {
                        ipEndPoint = new IPEndPoint(destination, this.localEndPoint.Port);
                        this.endPointCache.Add(destination, ipEndPoint);
                    }

                    newSendData.Destination = ipEndPoint;
                }

                return packet.WriteToBuffer(memory);
            });
        }

        public void WarmUpSockets(IEnumerable<ushort> universeIds)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                try
                {
                    this.socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }
                this.socket.Close();
                this.socket.Dispose();
            }
        }

        protected override ValueTask<int> SendPacketAsync(SendData sendData, ReadOnlyMemory<byte> payload)
        {
            return this.socket.SendToAsync(payload, SocketFlags.None, sendData.Destination);
        }

        protected override void ParseReceiveData(ReadOnlyMemory<byte> memory, SocketReceiveMessageFromResult result, double timestampMS)
        {
            var packet = BasePacket.Parse(memory);

            if (packet != null)
            {
                var newPacket = new ReceiveDataPacket
                {
                    TimestampMS = timestampMS,
                    Source = (IPEndPoint)result.RemoteEndPoint,
                    Packet = packet
                };

                if (!this.endPointCache.TryGetValue(result.PacketInformation.Address, out var ipEndPoint))
                {
                    ipEndPoint = new IPEndPoint(result.PacketInformation.Address, this.localEndPoint.Port);
                    this.endPointCache.Add(result.PacketInformation.Address, ipEndPoint);
                }

                newPacket.Destination = ipEndPoint ?? this.broadcastEndPoint;

                this.packetSubject.OnNext(newPacket);
            }
        }

        protected override async ValueTask<(int ReceivedBytes, SocketReceiveMessageFromResult Result)> ReceiveData1(Memory<byte> memory, CancellationToken cancelToken)
        {
            var result = await this.socket.ReceiveMessageFromAsync(memory, SocketFlags.None, _blankEndpoint, cancelToken);

            return (result.ReceivedBytes, result);
        }

        protected override ValueTask<(int ReceivedBytes, SocketReceiveMessageFromResult Result)> ReceiveData2(Memory<byte> memory, CancellationToken cancelToken)
        {
            throw new NotImplementedException();
        }
    }
}
