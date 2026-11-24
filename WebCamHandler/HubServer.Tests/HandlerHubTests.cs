using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using WebCamHandler.Hubs;
using WebCamHandler.Models;
using static HubServer.Tests.HubTestHelpers;

namespace HubServer.Tests
{
    /// <summary>
    /// Contract tests for handlerHub. Method and callback names are consumed by external
    /// webcam clients, so a failure here usually means the public contract changed.
    /// </summary>
    public class HandlerHubTests : IAsyncLifetime
    {
        private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>();
        private HubConnection _client = null!;

        public async Task InitializeAsync()
        {
            _client = await ConnectAsync(_factory, HubRoutes.Handler);
        }

        public async Task DisposeAsync()
        {
            await _client.DisposeAsync();
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task Send_BroadcastsAddNewMessageToPage()
        {
            var tcs = new TaskCompletionSource<(string, string)>(TaskCreationOptions.RunContinuationsAsynchronously);
            _client.On<string, string>("addNewMessageToPage", (name, message) => tcs.TrySetResult((name, message)));

            await _client.InvokeAsync("Send", "cam-1", "hello");

            Assert.Equal(("cam-1", "hello"), await tcs.Task.WaitAsync(HubTestHelpers.Timeout));
        }

        [Fact]
        public async Task AddComponent_BroadcastsMasterList()
        {
            Task<string> broadcast = Expect<string>(_client, "broadCastData");

            await _client.InvokeAsync("AddComponent", Json(new AccessControl { Name = "cam-1", IsHost = true }));

            List<AccessControl> list = JsonConvert.DeserializeObject<List<AccessControl>>(await broadcast)!;
            AccessControl item = Assert.Single(list);
            Assert.Equal("cam-1", item.Name);
            Assert.True(item.IsHost);
        }

        [Fact]
        public async Task AddComponent_IgnoresDuplicateNames()
        {
            await _client.InvokeAsync("AddComponent", Json(new AccessControl { Name = "cam-1" }));
            Task<string> broadcast = Expect<string>(_client, "broadCastData");

            await _client.InvokeAsync("AddComponent", Json(new AccessControl { Name = "cam-1", IpAddress = "10.0.0.9" }));

            AccessControl item = Assert.Single(JsonConvert.DeserializeObject<List<AccessControl>>(await broadcast)!);
            Assert.Null(item.IpAddress);
        }

        [Fact]
        public async Task AddComponent_BroadcastsPascalCaseJson()
        {
            Task<string> broadcast = Expect<string>(_client, "broadCastData");

            await _client.InvokeAsync("AddComponent", "{\"name\":\"cam-1\"}");

            Assert.Contains("\"Name\":\"cam-1\"", await broadcast);
        }

        [Fact]
        public async Task AllowAccess_SendsProcessAccessToRegisteredConnection()
        {
            await _client.InvokeAsync("AddComponent", Json(new AccessControl { Name = "cam-1", ConnectionId = _client.ConnectionId }));
            Task<string> processAccess = Expect<string>(_client, "processAccess");

            await _client.InvokeAsync("AllowAccess", Json(new Dictionary<string, string>
            {
                ["ConnectionId"] = _client.ConnectionId!,
                ["IpAddress"] = "192.168.1.20",
            }));

            Assert.Equal("192.168.1.20", await processAccess);
        }

        [Fact]
        public async Task AllowAccess_IgnoresUnregisteredConnection()
        {
            Task<string> processAccess = Expect<string>(_client, "processAccess");

            await _client.InvokeAsync("AllowAccess", Json(new Dictionary<string, string>
            {
                ["ConnectionId"] = _client.ConnectionId!,
                ["IpAddress"] = "192.168.1.20",
            }));

            await AssertNotReceived(processAccess);
        }

        [Fact]
        public async Task RemoveComponent_BroadcastsMasterList_KnownBugDoesNotRemove()
        {
            await _client.InvokeAsync("AddComponent", Json(new AccessControl { Name = "cam-1" }));
            Task<string> broadcast = Expect<string>(_client, "broadCastData");

            await _client.InvokeAsync("RemoveComponent", Json(new AccessControl { Name = "cam-1" }));

            // Characterises pre-existing behaviour carried over from the .NET Framework version:
            // List.Remove uses reference equality, so the component is never removed.
            // Update this assertion when the bug is fixed.
            Assert.Single(JsonConvert.DeserializeObject<List<AccessControl>>(await broadcast)!);
        }

        private static string Json(object value) => JsonConvert.SerializeObject(value);
    }
}
