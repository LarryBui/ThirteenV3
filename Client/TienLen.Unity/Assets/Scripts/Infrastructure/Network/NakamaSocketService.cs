using System;
using Nakama;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace TienLen.Unity.Infrastructure.Network
{
    public class NakamaSocketService : IDisposable
    {
        public event Action OnConnected; // Added event

        public IClient Client { get; private set; }
        public ISession Session { get; private set; }
        public ISocket Socket { get; private set; }

        public NakamaSocketService(IClient client)
        {
            Client = client;
        }

        public async UniTask ConnectAsync(ISession session)
        {
            Session = session;
            Socket = Client.NewSocket();
            await Socket.ConnectAsync(session);
            Debug.Log("[NakamaSocket] Connected.");
            OnConnected?.Invoke(); // Invoke event
        }

        public void Dispose()
        {
            Socket?.CloseAsync();
        }
    }
}
