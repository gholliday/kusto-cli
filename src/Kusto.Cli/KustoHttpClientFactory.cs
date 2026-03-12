using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;

namespace Kusto.Cli;

internal static class KustoHttpClientFactory
{
    internal const int KeepAliveIdleSeconds = 60;
    internal const int KeepAliveIntervalSeconds = 30;
    internal const int KeepAliveRetryCount = 5;

    public static HttpClient Create()
    {
        return new HttpClient(CreateHandler())
        {
            Timeout = TimeSpan.FromMinutes(CliRunner.DefaultRequestTimeoutMinutes)
        };
    }

    internal static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = ConnectAsync
        };
    }

    private static async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var dnsEndPoint = context.DnsEndPoint;
        var addresses = await Dns.GetHostAddressesAsync(dnsEndPoint.Host, dnsEndPoint.AddressFamily, cancellationToken);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };

        try
        {
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveTime, KeepAliveIdleSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveInterval, KeepAliveIntervalSeconds);
            socket.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.TcpKeepAliveRetryCount, KeepAliveRetryCount);

            await socket.ConnectAsync(addresses, dnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
