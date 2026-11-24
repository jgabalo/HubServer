using Microsoft.AspNetCore.SignalR;
using Newtonsoft.Json;
using WebCamHandler.Models;

namespace WebCamHandler.Hubs
{
    /// <summary>
    /// Process-wide chat relay between users and admins. Registered as a DI singleton.
    /// </summary>
    public class Chatter
    {
        private readonly IHubContext<ChatterHub> _hubContext;

        private readonly List<string> _adminConnectionIds = new List<string>();

        private readonly List<UserData> _userConnectionIds = new List<UserData>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Chatter"/> class.
        /// </summary>
        /// <param name="hubContext">The chatter hub context.</param>
        public Chatter(IHubContext<ChatterHub> hubContext)
        {
            _hubContext = hubContext;
        }

        /// <summary>
        /// Adds the user to list.
        /// </summary>
        /// <param name="userDataJson">The user data json.</param>
        public void AddUserToList(string userDataJson)
        {
            UserData userData = JsonConvert.DeserializeObject<UserData>(userDataJson)!;

            if (!_userConnectionIds.Exists(x => x.ConnectionId == userData.ConnectionId))
            {
                _userConnectionIds.Add(userData);
            }
        }

        /// <summary>
        /// Adds the admin to list.
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        public void AddAdminToList(string connectionId)
        {
            if (!_adminConnectionIds.Exists(x => x == connectionId))
            {
                _adminConnectionIds.Add(connectionId);
            }
        }

        /// <summary>
        /// Sends the admin message.
        /// </summary>
        /// <param name="userConnectionId">The sending user's connection identifier.</param>
        /// <param name="message">The message.</param>
        public async Task SendAdminMessage(string userConnectionId, string message)
        {
            UserData data = _userConnectionIds.SingleOrDefault(x => x.ConnectionId == userConnectionId)!;
            string sendMsg = data.Name + ": " + message;

            foreach (string connectionId in _adminConnectionIds)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("sendAdminMessage", sendMsg);
            }
        }

        /// <summary>
        /// Sends the list of registered users to every admin.
        /// </summary>
        public async Task GetUserData()
        {
            string userData = JsonConvert.SerializeObject(_userConnectionIds);

            foreach (string connectionId in _adminConnectionIds)
            {
                await _hubContext.Clients.Client(connectionId).SendAsync("sendUserData", userData);
            }
        }

        /// <summary>
        /// Sends the user message.
        /// </summary>
        /// <param name="userDataJson">The user data json.</param>
        /// <param name="message">The message.</param>
        public async Task SendUserMessage(string userDataJson, string message)
        {
            UserData userData = JsonConvert.DeserializeObject<UserData>(userDataJson)!;

            if (_userConnectionIds.Exists(x => x.ConnectionId == userData.ConnectionId))
            {
                await _hubContext.Clients.Client(userData.ConnectionId!).SendAsync("sendUserMessage", message);
            }
        }
    }
}
