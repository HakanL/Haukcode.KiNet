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

        /// <summary>
        /// The destination pre-serialized once. Socket.SendTo(..., EndPoint) re-serializes the
        /// EndPoint into a fresh SocketAddress on every call; handing it an already-serialized one
        /// removes that work and the last per-packet allocations.
        /// </summary>
        public SocketAddress DestinationAddress { get; set; }

        public SendData(IPEndPoint destination)
        {
            Destination = destination;
            DestinationAddress = destination.Serialize();
        }
    }

    public const int DefaultPort = 6038;
    public const int ReceiveBufferSize = 20480;
    private const int SendBufferSize = 1400;
    private static readonly IPEndPoint _blankEndpoint = new(IPAddress.Any, 0);

    private Socket? listenSocket;

    // One send socket per sender shard. Several threads sharing one UDP socket serialize on the
    // kernel's socket lock, which gives back most of the gain from sharding.
    private readonly Socket[] sendSockets;

    private readonly IPEndPoint localEndPoint;
    private readonly IPEndPoint broadcastEndPoint;
    private readonly Dictionary<IPAddress, IPEndPoint> endPointCache = [];

    // Serialized destinations, so the hot path never re-serializes an IPEndPoint. Only touched from
    // the single queue-writer thread (the send-data factory).
    private readonly Dictionary<IPEndPoint, SocketAddress> socketAddressCache = [];

    // KiNet's header "sequence" is a nominal field: the reverse-engineered protocol documents it as
    // "always set to 0, seq #, most of the time it's 0, not implemented in most supplies", real
    // Color Kinetics supplies ignore it, and captured packets carry zeros. It is filled here (as
    // OLA also does) but nothing reads it — so it imposes no ordering requirement on the wire and
    // is not a reason to keep the send path single-threaded.
    private uint sequenceCounter;

    // Reused across SendDmxData calls instead of allocating a packet per send (one per protocol
    // version). Reconfigured in place and serialized synchronously inside QueuePacket before the
    // next call, so a single instance is safe on the single-threaded send path.
    private readonly DmxOutPacket scratchDmxOutPacket = new(ReadOnlyMemory<byte>.Empty);
    private readonly PortOutPacket scratchPortOutPacket = new(0, ReadOnlyMemory<byte>.Empty);
    private readonly Func<Memory<byte>, int> scratchDmxOutWriter;
    private readonly Func<Memory<byte>, int> scratchPortOutWriter;

    // Argument for the cached send-data factory. QueuePacket invokes the factory synchronously
    // before its first await on the single queue-writer thread (the same assumption the
    // non-locked caches here already rest on), so passing the destination through a field lets
    // one cached delegate replace a fresh closure per queued packet.
    private IPAddress? pendingDestination;
    private readonly Func<SendData> pendingSendDataFactory;

    /// <param name="senderCount">
    /// Number of sender threads/sockets, sharded by universe id. KiNet pays the same per-packet
    /// kernel send cost as every other protocol, and one sender thread saturates a core at roughly
    /// 24,000 packets/sec. Default 1 = the original behavior.
    /// </param>
    public KiNetClient(IPAddress localAddress, IPAddress localSubnetMask, int port = DefaultPort, int senderCount = 1)
        : base(BasePacket.MAX_PACKET_SIZE, null, null, senderCount)
    {
        this.scratchDmxOutWriter = this.scratchDmxOutPacket.WriteToBuffer;
        this.scratchPortOutWriter = this.scratchPortOutPacket.WriteToBuffer;
        this.pendingSendDataFactory = BuildPendingSendData;

        this.localEndPoint = new IPEndPoint(localAddress, port);
        this.broadcastEndPoint = new IPEndPoint(Haukcode.Network.Utils.GetBroadcastAddress(localAddress, localSubnetMask), port);

        this.sendSockets = new Socket[SenderCount];
        for (int i = 0; i < SenderCount; i++)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.SendBufferSize = SendBufferSize;

            Haukcode.Network.Utils.SetSocketOptions(socket);

            socket.DontFragment = true;
            socket.EnableBroadcast = true;

            // Bind to the local interface (ephemeral port)
            socket.Bind(new IPEndPoint(localAddress, 0));

            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

            this.sendSockets[i] = socket;
        }
    }

    /// <summary>
    /// Serialized form of a destination, cached. Called from the send-data factory on the single
    /// queue-writer thread.
    /// </summary>
    private SocketAddress GetSocketAddress(IPEndPoint endPoint)
    {
        if (!this.socketAddressCache.TryGetValue(endPoint, out var socketAddress))
        {
            socketAddress = endPoint.Serialize();
            this.socketAddressCache.Add(endPoint, socketAddress);
        }

        return socketAddress;
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
        // Reconfigure the reused scratch packets in place instead of allocating per send. The
        // DMX memory is referenced, not copied — QueuePacket serializes it synchronously.
        BasePacket packet;
        switch (protocolVersion)
        {
            case 1:
                this.scratchDmxOutPacket.DMXData = dmxData;
                packet = this.scratchDmxOutPacket;
                break;

            case 2:
                this.scratchPortOutPacket.Port = universeId;
                this.scratchPortOutPacket.DMXData = dmxData;
                this.scratchPortOutPacket.DataLength = (ushort)dmxData.Length;
                this.scratchPortOutPacket.StartCode = startCode;
                packet = this.scratchPortOutPacket;
                break;

#if DEBUG
            default:
                throw new NotImplementedException();
#else
            default:
                return Task.CompletedTask;
#endif
        }

        return QueuePacket(packet, address, important: important, shardKey: universeId);
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
    public async Task QueuePacket(BasePacket packet, IPAddress? destination = null, bool important = false, int shardKey = 0)
    {
        packet.Sequence = Interlocked.Increment(ref this.sequenceCounter);

        this.pendingDestination = destination;

        // The scratch packets get cached writer delegates; anything else (rare) pays the
        // method-group allocation.
        Func<Memory<byte>, int> packetWriter =
            ReferenceEquals(packet, this.scratchDmxOutPacket) ? this.scratchDmxOutWriter :
            ReferenceEquals(packet, this.scratchPortOutPacket) ? this.scratchPortOutWriter :
            packet.WriteToBuffer;

        await base.QueuePacket(packet.Length, important, this.pendingSendDataFactory, packetWriter,
            // Shard by universe: every packet for a universe goes out on the same thread and socket, so
            // that universe's frames stay in order. The header sequence is ignored by receivers, so it
            // imposes no cross-universe ordering constraint.
            shardKey);
    }

    private SendData BuildPendingSendData()
    {
        var destination = this.pendingDestination;

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

        var destinationEndPoint = sendDataDestination ?? this.broadcastEndPoint;

        // Reuse a spent send-data object returned by the sender instead of allocating a new
        // one for every queued packet. Every field is rewritten before use.
        var pooledSendData = RentSendData();
        if (pooledSendData != null)
        {
            pooledSendData.Destination = destinationEndPoint;
            pooledSendData.DestinationAddress = GetSocketAddress(destinationEndPoint);

            return pooledSendData;
        }

        // Pool empty (startup only) — the constructor serializes the destination itself.
        return new SendData(destinationEndPoint);
    }

    /// <summary>
    /// Send packet immediately, bypassing the send queue
    /// </summary>
    /// <param name="destination">Destination</param>
    /// <param name="packet">Packet</param>
    /// <param name="important">Important</param>
    public Task SendPacketImmediately(IPAddress? destination, BasePacket packet, bool important = false)
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

        return SendPacketImmediately(sendDataDestination ?? this.broadcastEndPoint, packet, important);
    }

    public async Task SendPacketImmediately(IPEndPoint destination, BasePacket packet, bool important = false)
    {
        await SendImmediateAsync(
            allocatePacketLength: packet.Length,
            important: important,
            sendDataFactory: () => new SendData(destination),
            packetWriter: packet.WriteToBuffer);

    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (var socket in this.sendSockets)
            {
                try
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
                catch
                {
                }

                socket.Close();
                socket.Dispose();
            }
        }
    }

    protected override int SendPacket(SendData sendData, ReadOnlyMemory<byte> payload, int senderIndex)
    {
        // SendTo(..., SocketAddress) with a pre-serialized destination: the EndPoint overload
        // re-serializes into a fresh SocketAddress on every call.
        return this.sendSockets[senderIndex].SendTo(payload.Span, SocketFlags.None, sendData.DestinationAddress);
    }

    protected override int ReceiveData(Memory<byte> memory, out IPEndPoint? remoteEndPoint, out IPAddress? destinationAddress)
    {
        if (!MemoryMarshal.TryGetArray<byte>(memory, out var segment))
            throw new InvalidOperationException("Expected an array-backed receive buffer");

        var socketFlags = SocketFlags.None;
        EndPoint endPoint = _blankEndpoint;
        int receivedBytes = this.listenSocket!.ReceiveMessageFrom(segment.Array!, segment.Offset, segment.Count, ref socketFlags, ref endPoint, out IPPacketInformation packetInformation);

        remoteEndPoint = endPoint as IPEndPoint;
        destinationAddress = packetInformation.Address;

        return receivedBytes;
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
