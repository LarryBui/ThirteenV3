using System;
using Nakama;
using UnityEngine;
using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Infrastructure.Network
{
    public class NakamaAuthService
    {
        private readonly IClient _client;
        public ISession Session { get; private set; }

        public NakamaAuthService(IClient client)
        {
            _client = client;
        }

        public async UniTask<ISession> AuthenticateDeviceAsync()
        {
            var deviceId = SystemInfo.deviceUniqueIdentifier;
            
            // Retry loop could go here
            Session = await _client.AuthenticateDeviceAsync(deviceId);
            Debug.Log($"[NakamaAuth] Authenticated: {Session.UserId}");
            return Session;
        }
    }
}
