﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Proxy.MultiHost
{
    public class ProxyHandler
    {
        private const int BufferSize = 8196;

        public async Task Run(TcpClient tcpClient, Address address, CancellationToken token)
        {
            var streamFactory = new StreamFactory();

            var client = streamFactory.GetStream(tcpClient);
            var host = streamFactory.GetStream(address, client, token);

            await Task.WhenAll(Proxy(client, host, token));
        }

        private static async Task Proxy(Stream client, Stream host, CancellationToken token)
        {
            var buffer = new byte[BufferSize];
            var headerStream = new HttpHeaderStream();

            int bytes;

            do
            {
                using (var header = await headerStream.GetStream(client, token))
                {
                    var array = header.ToArray();
                    bytes = array.Length;
                    Console.WriteLine(Encoding.ASCII.GetString(array, 0, bytes));
                    host.WriteAsync(array, 0, array.Length, token).Wait(token);
                }

                bytes = await client.ReadAsync(buffer, 0, BufferSize, token);
                Console.WriteLine(Encoding.ASCII.GetString(buffer, 0, bytes));
                await host.WriteAsync(buffer, 0, bytes, token);
            } while (bytes > 0 && !token.IsCancellationRequested);
        }
    }

    public class HttpHeaderStream
    {
        private static readonly string[] Delimiter = {"\r", "\n", "\r", "\n"};

        public async Task<MemoryStream> GetStream(Stream client, CancellationToken token)
        {
            var memoryStream = new MemoryStream();
            var readBuffer = new byte[1];

            int bytesRead;
            var counter = 0;

            do
            {
                bytesRead = await client.ReadAsync(readBuffer, 0, 1, token);
                await memoryStream.WriteAsync(readBuffer, 0, bytesRead, token);

                counter = Encoding.ASCII.GetString(readBuffer) == Delimiter[counter] ? counter + 1 : 0;

                if (counter == Delimiter.Length)
                {
                    return memoryStream;
                }
            } while (bytesRead > 0 && !token.IsCancellationRequested);

            return memoryStream;
        }
    }
}