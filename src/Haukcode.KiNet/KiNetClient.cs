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
    public class KiNetClient : Client<KiNetClient.SendData, ReceiveDataPacket>
    {
        public const int DefaultPort = 6038;

        public class SendData : HighPerfComm.SendData
        {
            public IPEndPoint Destination { get; set; }

            public SendData(IPEndPoint destination)
            {
                Destination = destination;
            }
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
            : base(1000)
        {
            this.packetSubject = new Subject<ReceiveDataPacket>();

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            this.socket.ReceiveBufferSize = ReceiveBufferSize;
            this.socket.SendBufferSize = SendBufferSize;

            Haukcode.Network.Utils.SetSocketOptions(this.socket);

            this.socket.EnableBroadcast = true;

            this.localEndPoint = new IPEndPoint(localAddress, DefaultPort);
            this.broadcastEndPoint = new IPEndPoint(Haukcode.Network.Utils.GetBroadcastAddress(localAddress, localSubnetMask), this.localEndPoint.Port);

            // Linux wants Any to get multicast/broadcast packets (including unicast)
            this.socket.Bind(new IPEndPoint(bindAddress ?? localAddress, this.localEndPoint.Port));

            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
        }

        public IPEndPoint LocalEndPoint => this.localEndPoint;

        /// <summary>
        /// Observable that provides all parsed packets. This is buffered on its own thread so the processing can
        /// take any time necessary (memory consumption will go up though, there is no upper limit to amount of data buffered).
        /// </summary>
        public IObservable<ReceiveDataPacket> OnPacket => this.packetSubject.AsObservable();

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

            await base.QueuePacket(packet.Length, important, () =>
            {
                IPEndPoint? sendDataDestination = null;

                if (destination != null)
                {
                    if (!this.endPointCache.TryGetValue(destination, out var ipEndPoint))
                    {
                        ipEndPoint = new IPEndPoint(destination, this.localEndPoint.Port);
                        this.endPointCache.Add(destination, ipEndPoint);
                    }

                    // Only works for when subnet mask is /24 or less
                    if (ipEndPoint.Address.GetAddressBytes().Last() == 255)
                        sendDataDestination = null;
                    else
                        sendDataDestination = ipEndPoint;
                }

                return new SendData(sendDataDestination ?? this.broadcastEndPoint);
            }, packet.WriteToBuffer);
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

        protected override async ValueTask<(int ReceivedBytes, SocketReceiveMessageFromResult Result)> ReceiveData(Memory<byte> memory, CancellationToken cancelToken)
        {
            var result = await this.socket.ReceiveMessageFromAsync(memory, SocketFlags.None, _blankEndpoint, cancelToken);

            return (result.ReceivedBytes, result);
        }

        protected override ReceiveDataPacket? TryParseObject(ReadOnlyMemory<byte> buffer, double timestampMS, IPEndPoint sourceIP, IPAddress destinationIP)
        {
            var packet = BasePacket.Parse(buffer);

            // Note that we're still using the memory from the pipeline here, the packet is not allocating its own DMX data byte array
            if (packet != null)
            {
                var parsedObject = new ReceiveDataPacket
                {
                    TimestampMS = timestampMS,
                    Source = sourceIP,
                    Packet = packet
                };

                if (!this.endPointCache.TryGetValue(destinationIP, out var ipEndPoint))
                {
                    ipEndPoint = new IPEndPoint(destinationIP, this.localEndPoint.Port);
                    this.endPointCache.Add(destinationIP, ipEndPoint);
                }

                parsedObject.Destination = ipEndPoint ?? this.broadcastEndPoint;

                return parsedObject;
            }

            return null;
        }

        protected override void InitializeReceiveSocket()
        {
        }

        protected override void DisposeReceiveSocket()
        {
        }
    }
}
