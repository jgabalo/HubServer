using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using WebCamHandler.Models;

namespace WebCamHandler
{
    /// <summary>
    /// Process-wide registry of webcam components. Registered as a DI singleton.
    /// </summary>
    public class Handler
    {
        private readonly IHubContext<HandlerHub> _hubContext;

        private readonly List<AccessControl> _masterList = new List<AccessControl>();

        public Handler(IHubContext<HandlerHub> hubContext)
        {
            _hubContext = hubContext;
        }

        public async Task ProcessAccess(string data)
        {
            Dictionary<string, string> pushData = JsonConvert.DeserializeObject<Dictionary<string, string>>(data)!;
            string connectionId = pushData["ConnectionId"];
            string ipAddress = pushData["IpAddress"];

            if (_masterList.Exists(x => x.ConnectionId == connectionId))
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("processAccess", ipAddress);
            }
        }

        public async Task AddComponent(string data)
        {
            try
            {
                if (!string.IsNullOrEmpty(data))
                {
                    AccessControl? accessdata = JsonConvert.DeserializeObject<AccessControl>(data);

                    if (accessdata != null)
                    {
                        if (_masterList.Exists(x => x.Name == accessdata.Name))
                        {
                            //Some Logic Here Soon.
                        }
                        else
                        {
                            _masterList.Add(accessdata);
                        }
                    }

                    string masterData = JsonConvert.SerializeObject(_masterList);

                    await _hubContext.Clients.All.SendAsync("broadCastData", masterData);
                }
            }
            catch (Exception)
            {

            }
        }

        public async Task RemoveComponent(string data)
        {
            try
            {
                if (!string.IsNullOrEmpty(data))
                {
                    AccessControl? accessdata = JsonConvert.DeserializeObject<AccessControl>(data);

                    if (accessdata != null)
                    {
                        _masterList.Remove(accessdata);
                    }

                    string masterData = JsonConvert.SerializeObject(_masterList);

                    await _hubContext.Clients.All.SendAsync("broadCastData", masterData);
                }
            }
            catch (Exception)
            {

            }
        }
    }
}
