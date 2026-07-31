using CommunityToolkit.Mvvm.ComponentModel;
using Skua.Core.Interfaces;
using Skua.Core.Models;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Skua.Core.GameProxy;

public partial class CaptureProxy : ObservableRecipient, ICaptureProxy
{
    private readonly IDiagnosticsService _diagnostics;
    private long _outboundBytes;
    private long _inboundBytes;
    private long _outboundPackets;
    private long _inboundPackets;
    private long _interceptorInvocations;
    private long _interceptorElapsedTicks;
    private long _forwardingDrops;
    private long _forwardingExceptions;
    private long _activeStreams;
    private CancellationTokenSource? _captureProxyCTS;

    /// <summary>
    /// The default port for the capture proxy to run on.
    /// </summary>
    public const int DefaultPort = 5588;

    public IPEndPoint? Destination { get; set; }
    public List<IInterceptor> Interceptors { get; } = new();

    private Thread? _thread;
    private TcpListener? _listener;
    private TcpClient? _forwarder;
    private TcpClient? _client;
    private int _listenPort = DefaultPort;

    public CaptureProxy(IDiagnosticsService diagnostics)
    {
        _diagnostics = diagnostics;
    }

    [ObservableProperty]
    [NotifyPropertyChangedRecipients]
    private bool _running;

    public void Start()
    {
        ResetDiagnosticsCounters();
        if (Destination == null)
            return;
        Running = true;
        _listenPort = Destination.Port;
        _thread = new(() =>
        {
            _captureProxyCTS = new();
            _listener = new TcpListener(IPAddress.Loopback, _listenPort);
            _Listen(_captureProxyCTS.Token);
            _captureProxyCTS.Dispose();
            _captureProxyCTS = null;
        })
        { Name = "Capture Proxy" };
        _thread.Start();
    }
    public void Stop()
    {
        _captureProxyCTS?.Cancel();
        try { _listener?.Stop(); } catch { }
        if (_forwarder?.Connected ?? false)
        {
            _forwarder.Close();
            _forwarder.Dispose();
        }
        if (_client?.Connected ?? false)
        {
            _client.Close();
            _client.Dispose();
        }
        RecordDiagnosticsSummary();
        Running = false;
    }

    private void _Listen(CancellationToken token)
    {
        try
        {
            _listener?.Start();
        }
        catch
        {
            return;
        }

        while (!token.IsCancellationRequested)
        {
            TcpClient? localClient = null;
            TcpClient? localForwarder = null;
            try
            {
                localClient = _listener?.AcceptTcpClient();
                if (localClient == null)
                    break;
                localClient.NoDelay = true;
                localForwarder = new TcpClient
                {
                    NoDelay = true
                };
                localForwarder.Connect(Destination!);

                _client = localClient;
                _forwarder = localForwarder;
                TcpClient client = localClient;
                TcpClient forwarder = localForwarder;

                Interlocked.Add(ref _activeStreams, 2);
                Task.Factory.StartNew(() => _DataInterceptor(client, forwarder, true, token), token);
                Task.Factory.StartNew(() => _DataInterceptor(forwarder, client, false, token), token);
            }
            catch
            {
                localClient?.Close();
                localClient?.Dispose();
                localForwarder?.Close();
                localForwarder?.Dispose();
            }
        }

        _listener?.Stop();
    }
    private async Task _DataInterceptor(TcpClient target, TcpClient destination, bool outbound, CancellationToken token)
    {
        byte[] messageBuffer = new byte[4096];
        List<byte> cpacket = new();
        NetworkStream targetStream = target.GetStream();
        NetworkStream destStream = destination.GetStream();

        try
        {
            while (!token.IsCancellationRequested && target.Connected && destination.Connected)
            {
                int read = await targetStream.ReadAsync(messageBuffer, token).ConfigureAwait(false);

                if (read == 0)
                    break;

                for (int i = 0; i < read; i++)
                {
                    if (token.IsCancellationRequested)
                        break;

                    byte b = messageBuffer[i];
                    if (b > 0)
                    {
                        cpacket.Add(b);
                        continue;
                    }

                    if (cpacket.Count == 0)
                        continue;

                    byte[] data = cpacket.ToArray();
                    cpacket.Clear();

                    MessageInfo message = new(Encoding.UTF8.GetString(data, 0, data.Length));
                    if (outbound)
                    {
                        Interlocked.Increment(ref _outboundPackets);
                        Interlocked.Add(ref _outboundBytes, data.Length);
                    }
                    else
                    {
                        Interlocked.Increment(ref _inboundPackets);
                        Interlocked.Add(ref _inboundBytes, data.Length);
                    }

                    if (Interceptors.Count > 0)
                    {
                        IInterceptor[] currentInterceptors = Interceptors.OrderBy(i => i.Priority).ToArray();
                        long interceptorStart = Stopwatch.GetTimestamp();
                        foreach (IInterceptor interceptor in currentInterceptors)
                        {
                            interceptor.Intercept(message, outbound);
                            Interlocked.Increment(ref _interceptorInvocations);
                        }
                        Interlocked.Add(ref _interceptorElapsedTicks, Stopwatch.GetTimestamp() - interceptorStart);
                    }

                    if (message.Send)
                    {
                        byte[] contentBytes = _ToBytes(message.Content);
                        byte[] msg = new byte[contentBytes.Length + 1];
                        Buffer.BlockCopy(contentBytes, 0, msg, 0, contentBytes.Length);
                        await destStream.WriteAsync(msg, token).ConfigureAwait(false);
                    }
                    else
                    {
                        Interlocked.Increment(ref _forwardingDrops);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            /* Cancelled */
        }
        catch (Exception)
        {
            Interlocked.Increment(ref _forwardingExceptions);
        }
        finally
        {
            Interlocked.Decrement(ref _activeStreams);
            targetStream?.Dispose();
            destStream?.Dispose();
            try { target.Close(); } catch { }
            try { destination.Close(); } catch { }
        }
    }

    private void ResetDiagnosticsCounters()
    {
        Interlocked.Exchange(ref _outboundBytes, 0);
        Interlocked.Exchange(ref _inboundBytes, 0);
        Interlocked.Exchange(ref _outboundPackets, 0);
        Interlocked.Exchange(ref _inboundPackets, 0);
        Interlocked.Exchange(ref _interceptorInvocations, 0);
        Interlocked.Exchange(ref _interceptorElapsedTicks, 0);
        Interlocked.Exchange(ref _forwardingDrops, 0);
        Interlocked.Exchange(ref _forwardingExceptions, 0);
        Interlocked.Exchange(ref _activeStreams, 0);
    }

    private void RecordDiagnosticsSummary()
    {
        _diagnostics.RecordEvent("Proxy", "OutboundBytes", Interlocked.Read(ref _outboundBytes), "bytes");
        _diagnostics.RecordEvent("Proxy", "InboundBytes", Interlocked.Read(ref _inboundBytes), "bytes");
        _diagnostics.RecordEvent("Proxy", "OutboundPackets", Interlocked.Read(ref _outboundPackets), "packets");
        _diagnostics.RecordEvent("Proxy", "InboundPackets", Interlocked.Read(ref _inboundPackets), "packets");
        _diagnostics.RecordEvent("Proxy", "InterceptorInvocations", Interlocked.Read(ref _interceptorInvocations), "calls");
        long elapsedTicks = Interlocked.Read(ref _interceptorElapsedTicks);
        double elapsedMilliseconds = elapsedTicks * 1000d / Stopwatch.Frequency;
        _diagnostics.RecordEvent("Proxy", "InterceptorTime", elapsedMilliseconds, "ms");
        _diagnostics.RecordEvent("Proxy", "ForwardingDrops", Interlocked.Read(ref _forwardingDrops), "messages");
        _diagnostics.RecordEvent("Proxy", "ForwardingExceptions", Interlocked.Read(ref _forwardingExceptions), "exceptions");
        _diagnostics.RecordEvent("Proxy", "ActiveStreams", Interlocked.Read(ref _activeStreams), "streams");
    }

    private static byte[] _ToBytes(string s)
    {
        byte[] result = new byte[s.Length];
        for (int i = 0; i < s.Length; i++)
            result[i] = (byte)s[i];
        return result;
    }
}