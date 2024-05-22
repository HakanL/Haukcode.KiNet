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
using Haukcode.KiNet.Model;

namespace Haukcode.KiNet
{
    public class KiNetClient : IDisposable
    {
        public class SendSocketData
        {
            public Socket Socket;

            public IPEndPoint Destination;
        }

        public class SendData
        {
            public IPEndPoint Destination;

            public IMemoryOwner<byte> Data;

            public int DataLength;

            public Stopwatch Enqueued;

            public double AgeMS => Enqueued.Elapsed.TotalMilliseconds;

            public SendData()
            {
                Enqueued = Stopwatch.StartNew();
            }
        }

        private const int ReceiveBufferSize = 20480;
        private const int SendBufferSize = 1400;
        private static readonly IPEndPoint _blankEndpoint = new(IPAddress.Any, 0);

        private readonly Socket socket;
        private readonly ISubject<Exception> errorSubject;
        private readonly ISubject<ReceiveDataPacket> packetSubject;
        private readonly Memory<byte> receiveBufferMem;
        private readonly Stopwatch clock = new();
        private readonly Task receiveTask;
        private readonly Task sendTask;
        private readonly CancellationTokenSource shutdownCTS = new();
        private readonly Dictionary<IPAddress, IPEndPoint> endPointCache = new();
        private readonly BlockingCollection<SendData> sendQueue = new();
        private readonly MemoryPool<byte> memoryPool = MemoryPool<byte>.Shared;
        private int droppedPackets;
        private int slowSends;
        private readonly HashSet<IPAddress> usedDestinations = new();
        private IPEndPoint broadcastEndPoint;
        private uint sequenceCounter;
        private readonly IPEndPoint localEndPoint;

        public KiNetClient(IPAddress localAddress, IPAddress localSubnetMask, IPAddress bindAddress = null)
        {
            var receiveBuffer = GC.AllocateArray<byte>(length: ReceiveBufferSize, pinned: true);
            this.receiveBufferMem = receiveBuffer.AsMemory();

            this.errorSubject = new Subject<Exception>();
            this.packetSubject = new Subject<ReceiveDataPacket>();

            this.socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
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

            this.localEndPoint = new IPEndPoint(localAddress, Port);
            // Linux wants Any to get multicast/broadcast packets
            this.socket.Bind(new IPEndPoint(bindAddress ?? localAddress, Port));

            this.socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);

            this.broadcastEndPoint = new IPEndPoint(GetBroadcastAddress(localAddress, localSubnetMask), Port);

            this.receiveTask = Task.Run(Receiver);
            this.sendTask = Task.Run(Sender);
        }

        public bool IsOperational => !this.shutdownCTS.IsCancellationRequested;

        public int Port => 6038;

        public IObservable<Exception> OnError => this.errorSubject.AsObservable();

        public SendStatistics SendStatistics
        {
            get
            {
                var sendStatistics = new SendStatistics
                {
                    DroppedPackets = this.droppedPackets,
                    QueueLength = this.sendQueue.Count,
                    SlowSends = this.slowSends,
                    DestinationCount = this.usedDestinations.Count
                };

                // Reset
                this.droppedPackets = 0;
                this.slowSends = 0;
                this.usedDestinations.Clear();

                return sendStatistics;
            }
        }

        /// <summary>
        /// Observable that provides all parsed packets. This is buffered on its own thread so the processing can
        /// take any time necessary (memory consumption will go up though, there is no upper limit to amount of data buffered).
        /// </summary>
        public IObservable<ReceiveDataPacket> OnPacket => this.packetSubject.AsObservable();

        public void StartReceive()
        {
            this.clock.Restart();
        }

        public double ReceiveClock => this.clock.Elapsed.TotalMilliseconds;

        private async Task Receiver()
        {
            while (!this.shutdownCTS.IsCancellationRequested)
            {
                try
                {
                    var result = await this.socket.ReceiveMessageFromAsync(this.receiveBufferMem, SocketFlags.None, _blankEndpoint, this.shutdownCTS.Token);

                    // Capture the timestamp first so it's as accurate as possible
                    double timestampMS = this.clock.Elapsed.TotalMilliseconds;

                    if (result.RemoteEndPoint.Equals(this.localEndPoint))
                        // Filter out our own
                        continue;

                    if (result.ReceivedBytes > 0)
                    {
                        var readBuffer = this.receiveBufferMem[..result.ReceivedBytes];

                        var packet = BasePacket.Parse(readBuffer);

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
                                ipEndPoint = new IPEndPoint(result.PacketInformation.Address, Port);
                                this.endPointCache.Add(result.PacketInformation.Address, ipEndPoint);
                            }

                            newPacket.Destination = ipEndPoint;

                            this.packetSubject.OnNext(newPacket);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ex is not OperationCanceledException)
                    {
                        this.errorSubject.OnNext(ex);
                    }

                    if (ex is System.Net.Sockets.SocketException)
                    {
                        // Network unreachable
                        this.shutdownCTS.Cancel();
                        break;
                    }
                }
            }
        }

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

        private async Task Sender()
        {
            while (!this.shutdownCTS.IsCancellationRequested)
            {
                var sendData = this.sendQueue.Take(this.shutdownCTS.Token);

                try
                {
                    if (sendData.AgeMS > 100)
                    {
                        // Old, discard
                        this.droppedPackets++;
                        //Console.WriteLine($"Age {sendData.Enqueued.Elapsed.TotalMilliseconds:N2}   queue length = {this.sendQueue.Count}   Dropped = {this.droppedPackets}");
                        continue;
                    }

                    var destination = sendData.Destination ?? this.broadcastEndPoint;

                    var watch = Stopwatch.StartNew();
                    if (destination != null)
                        await this.socket.SendToAsync(sendData.Data.Memory[..sendData.DataLength], SocketFlags.None, destination);
                    watch.Stop();

                    if (watch.ElapsedMilliseconds > 20)
                        this.slowSends++;
                }
                catch (Exception ex)
                {
                    if (ex is OperationCanceledException)
                        continue;

                    //Console.WriteLine($"Exception in Sender handler: {ex.Message}");
                    this.errorSubject.OnNext(ex);

                    if (ex is System.Net.Sockets.SocketException)
                    {
                        // Network unreachable
                        this.shutdownCTS.Cancel();
                        break;
                    }
                }
                finally
                {
                    // Return to pool
                    sendData.Data.Dispose();
                }
            }
        }

        /// <summary>
        /// Broadcast send data
        /// </summary>
        /// <param name="universeId">The universe Id to broadcast to</param>
        /// <param name="data">Up to 512 bytes of DMX data</param>
        /// <param name="startCode">Start code (default 0)</param>
        public void SendDmxBroadcast(ushort universeId, ReadOnlyMemory<byte> data, byte startCode = 0, int protocolVersion = 1)
        {
            BasePacket packet = protocolVersion switch
            {
                1 => new DmxOutPacket(data),
                2 => new PortOutPacket((byte)universeId, data, startCode),
                _ => throw new NotImplementedException(),
            };

            SendPacket(packet);
        }

        /// <summary>
        /// Unicast send data
        /// </summary>
        /// <param name="address">The address to unicast to</param>
        /// <param name="universeId">The Universe ID</param>
        /// <param name="data">Up to 512 bytes of DMX data</param>
        /// <param name="startCode">Start code (default 0)</param>
        public void SendDmxUnicast(IPAddress address, ushort universeId, ReadOnlyMemory<byte> data, byte startCode = 0, int protocolVersion = 1)
        {
            BasePacket packet = protocolVersion switch
            {
                1 => new DmxOutPacket(data),
                2 => new PortOutPacket((byte)universeId, data, startCode),
                _ => throw new NotImplementedException(),
            };

            SendPacket(address, packet);
        }

        /// <summary>
        /// Broadcast send sync
        /// </summary>
        public void SendSyncBroadcast()
        {
            var packet = new SyncPacket();

            SendPacket(packet);
        }

        /// <summary>
        /// Unicast send sync
        /// </summary>
        /// <param name="destination">Destination</param>
        public void SendSyncUnicast(IPAddress destination)
        {
            var packet = new SyncPacket();

            SendPacket(destination, packet);
        }

        /// <summary>
        /// Send packet
        /// </summary>
        /// <param name="destination">Destination</param>
        /// <param name="packet">Packet</param>
        public void SendPacket(IPAddress destination, BasePacket packet)
        {
            if (!this.endPointCache.TryGetValue(destination, out var ipEndPoint))
            {
                ipEndPoint = new IPEndPoint(destination, Port);
                this.endPointCache.Add(destination, ipEndPoint);
            }

            var memory = this.memoryPool.Rent(packet.Length);

            int packetLength = packet.WriteToBuffer(memory.Memory);

            var newSendData = new SendData
            {
                Data = memory,
                DataLength = packetLength,
                Destination = ipEndPoint
            };

            this.usedDestinations.Add(destination);

            if (IsOperational)
            {
                this.sendQueue.Add(newSendData);
            }
            else
            {
                // Clear queue
                while (this.sendQueue.TryTake(out _)) ;
            }
        }

        /// <summary>
        /// Send packet
        /// </summary>
        /// <param name="universeId">Universe Id</param>
        /// <param name="packet">Packet</param>
        public void SendPacket(BasePacket packet)
        {
            packet.Sequence = Interlocked.Increment(ref this.sequenceCounter);

            var memory = this.memoryPool.Rent(packet.Length);

            int packetLength = packet.WriteToBuffer(memory.Memory);

            var newSendData = new SendData
            {
                Data = memory,
                DataLength = packetLength
            };

            this.usedDestinations.Add(null);
            if (IsOperational)
            {
                this.sendQueue.Add(newSendData);
            }
            else
            {
                // Clear queue
                while (this.sendQueue.TryTake(out _)) ;
            }
        }

        public void WarmUpSockets(IEnumerable<ushort> universeIds)
        {
        }

        public void Dispose()
        {
            this.shutdownCTS.Cancel();

            try
            {
                this.socket.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            if (this.receiveTask?.IsCanceled == false)
                this.receiveTask?.Wait();
            this.receiveTask?.Dispose();

            if (this.sendTask?.IsCanceled == false)
                this.sendTask?.Wait();
            this.sendTask?.Dispose();

            this.socket.Close();
            this.socket.Dispose();
        }
    }
}
