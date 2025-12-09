using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using TienLen.Gen; // Generated Protobuf namespace
using TienLen.Unity.Infrastructure.Logging;
using UnityEngine;
using VContainer;

namespace TienLen.Unity.Infrastructure.Network
{
    public interface IGameNetwork
    {
        event Action<MatchStartPacket> OnMatchStart;
        event Action<TurnUpdatePacket> OnTurnUpdate;
        event Action<string> OnError;

        IMatch CurrentNakamaMatch { get; } // Added

        Task ConnectAndJoinMatchAsync();
        Task SendPlayCardAsync(List<int> cardIndices);
        Task SendStartMatchAsync();
    }

    public class NakamaGameNetwork : IGameNetwork, IDisposable
    {
        private readonly IClient _client;
        private readonly ISocket _socket;
        private readonly NakamaAuthService _authService;
        
        private IMatch _currentMatch;
        private ISession _session;

        public IMatch CurrentNakamaMatch => _currentMatch; // Implementation of the property

        public event Action<MatchStartPacket> OnMatchStart;
        public event Action<TurnUpdatePacket> OnTurnUpdate;
        public Action<string> OnError;

        [Inject]
        public NakamaGameNetwork(IClient client, ISocket socket, NakamaAuthService authService)
        {
            _client = client;
            _socket = socket;
            _authService = authService;
        }

        public async Task ConnectAndJoinMatchAsync()
        {
            try
            {
                var deviceId = SystemInfo.deviceUniqueIdentifier;
                _session = await _client.AuthenticateDeviceAsync(deviceId, create: true, username: "Player_" + UnityEngine.Random.Range(1000, 9999));
                
                FastLog.Info($"Authenticated as {_session.Username} ({_session.UserId})");

                if (!_socket.IsConnected)
                {
                    _socket.ReceivedMatchState += OnReceivedMatchState;
                    await _socket.ConnectAsync(_session);
                    FastLog.Info("Socket Connected");
                }

                _currentMatch = await _socket.CreateMatchAsync("tienlen_match");
                
                FastLog.Info($"Joined Match: {_currentMatch.Id}");
            }
            catch (Exception ex)
            {
                FastLog.Error($"Network Error: {ex.Message}");
                OnError?.Invoke(ex.Message);
                throw;
            }
        }

        public async Task SendPlayCardAsync(List<int> cardIndices)
        {
            if (_currentMatch == null) return;

            var request = new PlayCardRequest();
            request.CardIndices.AddRange(cardIndices);

            await _socket.SendMatchStateAsync(_currentMatch.Id, (long)OpCode.OpPlayCard, request.ToByteArray());
        }
        
        public async Task SendStartMatchAsync()
        {
            if (_currentMatch == null) return;
            await _socket.SendMatchStateAsync(_currentMatch.Id, (long)OpCode.OpMatchStartRequest, null);
        }

        private void OnReceivedMatchState(IMatchState state)
        {
            try
            {
                switch ((OpCode)state.OpCode)
                {
                    case OpCode.OpMatchStart:
                        var startPacket = MatchStartPacket.Parser.ParseFrom(state.State);
                        MainThreadDispatcher.Enqueue(() => OnMatchStart?.Invoke(startPacket));
                        break;

                    case OpCode.OpTurnUpdate:
                        var turnPacket = TurnUpdatePacket.Parser.ParseFrom(state.State);
                        MainThreadDispatcher.Enqueue(() => OnTurnUpdate?.Invoke(turnPacket));
                        break;
                    
                    case OpCode.OpError:
                        var errorMsg = System.Text.Encoding.UTF8.GetString(state.State);
                        MainThreadDispatcher.Enqueue(() => OnError?.Invoke(errorMsg));
                        break;
                }
            }
            catch (Exception ex)
            {
                FastLog.Error($"Error parsing match state: {ex.Message}");
            }
        }

        public void Dispose()
        {
            _socket?.CloseAsync();
        }
    }
}
