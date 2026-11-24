using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace HubServer.Tests
{
    internal static class HubTestHelpers
    {
        public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Builds and starts a SignalR client connected to the in-memory test server.
        /// </summary>
        public static async Task<HubConnection> ConnectAsync(WebApplicationFactory<Program> factory, string hubPath)
        {
            HubConnection connection = new HubConnectionBuilder()
                .WithUrl(new Uri(factory.Server.BaseAddress, hubPath), options =>
                {
                    options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    options.Transports = HttpTransportType.LongPolling;
                })
                .Build();

            await connection.StartAsync();
            return connection;
        }

        /// <summary>
        /// Registers a one-shot listener for a client callback and returns a task for its first value.
        /// </summary>
        public static Task<T> Expect<T>(HubConnection connection, string method)
        {
            var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            connection.On<T>(method, value => tcs.TrySetResult(value));
            return tcs.Task.WaitAsync(Timeout);
        }

        /// <summary>
        /// Asserts that a callback is NOT received within a short window.
        /// </summary>
        public static async Task AssertNotReceived<T>(Task<T> pending)
        {
            Task winner = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromMilliseconds(500)));
            Assert.NotSame(pending, winner);
        }
    }
}
