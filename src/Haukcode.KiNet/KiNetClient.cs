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
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Haukcode.HighPerfComm;
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet;

public class KiNetClient : Client<KiNetClient.SendData, ReceiveDataPacket>
{
    public class SendData : HighPerfComm.SendData
    {
        public IPEndPoint Destination { get; set; }

        public SendData(IPEndPoint destination)
        {
            Destination = destination;
        }
    }

    public const int DefaultPort = 6038;
    public const int ReceiveBufferSize = 20480;
    private const int SendBufferSize = 1400;
    private static readonly IPEndPoint _blankEndpoint = new(IPAddress.Any, 0);

    private Socket? listenSocket;
    private readonly Socket sendSocket;
    private readonly IPEndPoint localEndPoint;
    private readonly IPEndPoint broadcastEndPoint;
    private readonly Dictionary<IPAddress, IPEndPoint> endPointCache = [];
    private uint sequenceCounter;

    public KiNetClient(IPAddress localAddress, IPAddress localSubnetMask, int port = DefaultPort)
        : base(BasePacket.MAX_PACKET_SIZE, null, null)
    {
        this.localEndPoint = new IPEndPoint(localAddress, port);
        this.broadcastEndPoint = new IPEndPoint(Haukcode.Network.Utils.GetBroadcastAddress(localAddress, localSubnetMask), port);

        this.sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        this.sendSocket.SendBufferSize = SendBufferSize;

        Haukcode.Network.Utils.SetSocketOptions(this.sendSocket);

        this.sendSocket.DontFragment = true;
        this.sendSocket.EnableBroadcast = true;

        // Bind to the local interface
        this.sendSocket.Bind(new IPEndPoint(localAddress, 0));

        this.sendSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
    }

    public IPEndPoint LocalEndPoint => this.localEndPoint;

    public IPAddress BroadcastAddress => this.broadcastEndPoint.Address;

    /// <summary>
    /// Send data
    /// </summary>
    /// <param name="address">The address to unicast to</param>
    /// <param name="universeId">The Universe ID</param>
    /// <param name="dmxData">Up to 512 bytes of DMX data</param>
    /// <param name="startCode">Start code (default 0)</param>
    public Task SendDmxData(IPAddress? address, byte universeId, ReadOnlyMemory<byte> dmxData, bool important = false, byte startCode = 0, int protocolVersion = 1)
    {
        BasePacket packet;
        switch (protocolVersion)
        {
            case 1:
                packet = new DmxOutPacket(dmxData);
                break;

            case 2:
                packet = new PortOutPacket(universeId, dmxData, startCode);
                break;

#if DEBUG
            default:
                throw new NotImplementedException();
#else
            default:
                return Task.CompletedTask;
#endif
        }

        return QueuePacket(packet, address, important: important);
    }

    /// <summary>
    /// Send sync
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

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            try
            {
                this.sendSocket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }
            this.sendSocket.Close();
            this.sendSocket.Dispose();
        }
    }

    protected override ValueTask<int> SendPacketAsync(SendData sendData, ReadOnlyMemory<byte> payload)
    {
        return this.sendSocket.SendToAsync(payload, SocketFlags.None, sendData.Destination);
    }

    protected override async ValueTask<(int ReceivedBytes, SocketReceiveMessageFromResult Result)> ReceiveData(Memory<byte> memory, CancellationToken cancelToken)
    {
        var result = await this.listenSocket!.ReceiveMessageFromAsync(memory, SocketFlags.None, _blankEndpoint, cancelToken);

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

    public int? ActualReceiveBufferSize
    {
        get
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                // Linux reports the internal buffer size, which is double the requested size
                return this.listenSocket?.ReceiveBufferSize / 2;
            else
                return this.listenSocket?.ReceiveBufferSize;
        }
    }

    protected override void InitializeReceiveSocket()
    {
        this.listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        this.listenSocket.ReceiveBufferSize = ReceiveBufferSize;

        Haukcode.Network.Utils.SetSocketOptions(this.listenSocket);

        // Linux wants IPAddress.Any to get all types of packets (unicast/multicast/broadcast)
        this.listenSocket.Bind(new IPEndPoint(IPAddress.Any, this.localEndPoint.Port));
    }

    protected override void DisposeReceiveSocket()
    {
        try
        {
            this.listenSocket?.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        this.listenSocket?.Close();
        this.listenSocket?.Dispose();
        this.listenSocket = null;
    }
}
