using Microsoft.AspNetCore.SignalR;

namespace WebCamHandler.Hubs
{
    public class ChatterHub : Hub
    {
        private readonly Chatter _chatter;

        /// <summary>
        /// Initializes a new instance of the <see cref="ChatterHub"/> class.
        /// </summary>
        /// <param name="chatter">The chatter.</param>
        public ChatterHub(Chatter chatter)
        {
            _chatter = chatter;
        }

        /// <summary>
        /// Access for Users
        /// </summary>
        /// <param name="userDataJson">The user data json.</param>
        public void UserAccess(string userDataJson)
        {
            _chatter.AddUserToList(userDataJson);
        }

        /// <summary>
        /// Access for Admins
        /// </summary>
        /// <param name="connectionId">The connection identifier.</param>
        public void AdminAccess(string connectionId)
        {
            _chatter.AddAdminToList(connectionId);
        }

        public Task GetUserData()
        {
            return _chatter.GetUserData();
        }

        /// <summary>
        /// Sends the message to admin.
        /// </summary>
        /// <param name="connectionId">The sending user's connection identifier.</param>
        /// <param name="message">The message.</param>
        public Task SendMessageToAdmin(string connectionId, string message)
        {
            return _chatter.SendAdminMessage(connectionId, message);
        }

        public Task SendMessageToUser(string userDataJson, string message)
        {
            return _chatter.SendUserMessage(userDataJson, message);
        }
    }
}
