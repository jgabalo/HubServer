using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using WebCamHandler.Hubs;
using WebCamHandler.Models;
using static HubServer.Tests.HubTestHelpers;

namespace HubServer.Tests
{
    /// <summary>
    /// Contract tests for chatterHub, exercising the user and admin flows with two real clients.
    /// </summary>
    public class ChatterHubTests : IAsyncLifetime
    {
        private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>();
        private HubConnection _user = null!;
        private HubConnection _admin = null!;

        public async Task InitializeAsync()
        {
            _user = await ConnectAsync(_factory, HubRoutes.Chatter);
            _admin = await ConnectAsync(_factory, HubRoutes.Chatter);
        }

        public async Task DisposeAsync()
        {
            await _user.DisposeAsync();
            await _admin.DisposeAsync();
            await _factory.DisposeAsync();
        }

        [Fact]
        public async Task SendMessageToAdmin_PrefixesSenderName()
        {
            await RegisterAsync();
            Task<string> received = Expect<string>(_admin, "sendAdminMessage");

            await _user.InvokeAsync("SendMessageToAdmin", _user.ConnectionId, "Is my order ready?");

            Assert.Equal("Alex: Is my order ready?", await received);
        }

        [Fact]
        public async Task GetUserData_SendsRegisteredUsersToAdmins()
        {
            await RegisterAsync();
            Task<string> received = Expect<string>(_admin, "sendUserData");

            await _admin.InvokeAsync("GetUserData");

            UserData user = Assert.Single(JsonConvert.DeserializeObject<List<UserData>>(await received)!);
            Assert.Equal("Alex", user.Name);
            Assert.Equal(_user.ConnectionId, user.ConnectionId);
        }

        [Fact]
        public async Task SendMessageToUser_DeliversToThatUser()
        {
            await RegisterAsync();
            Task<string> received = Expect<string>(_user, "sendUserMessage");

            await _admin.InvokeAsync("SendMessageToUser", UserJson(), "Yes, it is ready.");

            Assert.Equal("Yes, it is ready.", await received);
        }

        [Fact]
        public async Task SendMessageToUser_IgnoresUnregisteredUser()
        {
            Task<string> received = Expect<string>(_user, "sendUserMessage");

            await _admin.InvokeAsync("SendMessageToUser", UserJson(), "hello");

            await AssertNotReceived(received);
        }

        [Fact]
        public async Task UserAccess_IsIdempotentPerConnection()
        {
            await RegisterAsync();
            await _user.InvokeAsync("UserAccess", UserJson());
            Task<string> received = Expect<string>(_admin, "sendUserData");

            await _admin.InvokeAsync("GetUserData");

            Assert.Single(JsonConvert.DeserializeObject<List<UserData>>(await received)!);
        }

        private async Task RegisterAsync()
        {
            await _user.InvokeAsync("UserAccess", UserJson());
            await _admin.InvokeAsync("AdminAccess", _admin.ConnectionId);
        }

        private string UserJson() => JsonConvert.SerializeObject(new UserData
        {
            Name = "Alex",
            Email = "alex@example.test",
            Phone = "021 000 0000",
            ConnectionId = _user.ConnectionId,
        });
    }
}
