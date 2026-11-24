namespace WebCamHandler.Hubs
{
    /// <summary>
    /// URL paths the hubs are mapped to. ASP.NET Core SignalR maps each hub to its own
    /// endpoint, replacing the single SignalR 2 "/signalr" endpoint that selected hubs by name.
    /// These are part of the public contract with the webcam clients.
    /// </summary>
    public static class HubRoutes
    {
        public const string Handler = "/signalr/handlerHub";
        public const string Chatter = "/signalr/chatterHub";
    }
}
