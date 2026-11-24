using Microsoft.AspNetCore.SignalR;

namespace WebCamHandler
{
    public class HandlerHub : Hub
    {
        private readonly Handler _handler;

        public HandlerHub(Handler handler)
        {
            _handler = handler;
        }

        public Task Send(string name, string message)
        {
            // Call the addNewMessageToPage method to update clients.
            return Clients.All.SendAsync("addNewMessageToPage", name, message);
        }

        public Task AllowAccess(string data)
        {
            return _handler.ProcessAccess(data);
        }

        public Task AddComponent(string data)
        {
            return _handler.AddComponent(data);
        }

        public Task RemoveComponent(string data)
        {
            return _handler.RemoveComponent(data);
        }
    }
}
