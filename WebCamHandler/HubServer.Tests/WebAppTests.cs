using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using WebCamHandler.Hubs;

namespace HubServer.Tests
{
    public class WebAppTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public WebAppTests(WebApplicationFactory<Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/")]
        [InlineData("/Home/About")]
        [InlineData("/Home/History")]
        [InlineData("/Home/Contact")]
        public async Task Pages_Render(string url)
        {
            HttpResponseMessage response = await _factory.CreateClient().GetAsync(url);

            response.EnsureSuccessStatusCode();
            Assert.Contains("Cupcake Admin</a>", await response.Content.ReadAsStringAsync());
        }

        [Theory]
        [InlineData("/Content/bootstrap.min.css")]
        [InlineData("/Content/Site.css")]
        [InlineData("/Scripts/jquery-2.2.0.min.js")]
        [InlineData("/Scripts/bootstrap.min.js")]
        [InlineData("/favicon.ico")]
        public async Task StaticAssets_AreServed(string url)
        {
            HttpResponseMessage response = await _factory.CreateClient().GetAsync(url);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Theory]
        [InlineData(HubRoutes.Handler)]
        [InlineData(HubRoutes.Chatter)]
        public async Task HubNegotiation_AllowsCrossOriginWithCredentials(string hubPath)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, hubPath + "/negotiate?negotiateVersion=1");
            request.Headers.Add("Origin", "http://webcam-client.example.test");

            HttpResponseMessage response = await _factory.CreateClient().SendAsync(request);

            response.EnsureSuccessStatusCode();
            Assert.Equal("http://webcam-client.example.test", response.Headers.GetValues("Access-Control-Allow-Origin").Single());
            Assert.Equal("true", response.Headers.GetValues("Access-Control-Allow-Credentials").Single());
            Assert.Contains("connectionToken", await response.Content.ReadAsStringAsync());
        }
    }
}
