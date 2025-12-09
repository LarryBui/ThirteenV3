using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using TienLen.Gen; // Generated Protobuf namespace
using TienLen.Unity.Infrastructure.Logging;
using TienLen.Unity.Domain.Aggregates;
using UnityEngine;
using VContainer;

namespace TienLen.Unity.Infrastructure.Network
{
    public interface IGameNetwork
    {
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
        private readonly GameModel _gameModel; // New
        
        private IMatch _currentMatch;
        private ISession _session;

        public IMatch CurrentNakamaMatch => _currentMatch; // Implementation of the property

        public event Action<string> OnError;

        [Inject]
        public NakamaGameNetwork(IClient client, ISocket socket, NakamaAuthService authService, GameModel gameModel)
        {
            _client = client;
            _socket = socket;
            _authService = authService;
            _gameModel = gameModel; // New
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
            await _socket.SendMatchStateAsync(_currentMatch.Id, (long)OpCode.OpMatchStartRequest, System.Array.Empty<byte>());
        }

        private void OnReceivedMatchState(IMatchState state)
        {
            try
            {
                switch ((OpCode)state.OpCode)
                {
                    case OpCode.OpMatchStart:
                        var startPacket = MatchStartPacket.Parser.ParseFrom(state.State);
                        // Convert Protobuf Hand to Domain Hand
                        var domainHand = new TienLen.Unity.Domain.Aggregates.Hand();
                        foreach (var protoCard in startPacket.Hand)
                        {
                            domainHand.AddCard(new TienLen.Unity.Domain.ValueObjects.Card((TienLen.Unity.Domain.Enums.Rank)protoCard.Rank, (TienLen.Unity.Domain.Enums.Suit)protoCard.Suit));
                        }
                        MainThreadDispatcher.Enqueue(() => _gameModel.SetPlayerHand(domainHand));
                        break;

                    case OpCode.OpTurnUpdate:
                        var turnPacket = TurnUpdatePacket.Parser.ParseFrom(state.State);
                        // Convert Protobuf Cards to Domain Cards
                        var domainCards = new List<TienLen.Unity.Domain.ValueObjects.Card>();
                        foreach (var protoCard in turnPacket.LastPlayedCards)
                        {
                            domainCards.Add(new TienLen.Unity.Domain.ValueObjects.Card((TienLen.Unity.Domain.Enums.Rank)protoCard.Rank, (TienLen.Unity.Domain.Enums.Suit)protoCard.Suit));
                        }
                        MainThreadDispatcher.Enqueue(() => {
                            _gameModel.UpdateBoard(domainCards);
                            _gameModel.SetActivePlayer(turnPacket.ActivePlayerId);
                            _gameModel.SetSecondsRemaining(turnPacket.SecondsRemaining);
                        });
                        break;
                    
                    case OpCode.OpError:
                        var errorMsg = System.Text.Encoding.UTF8.GetString(state.State);
                        MainThreadDispatcher.Enqueue(() => 
                        {
                            if (OnError != null)
                                OnError?.Invoke(errorMsg);
                        });
                        break;
                }
            }
            catch (Exception ex)
            {
                FastLog.Error($"Error parsing match state: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => 
                {
                    if (OnError != null)
                        OnError?.Invoke($"Parse error: {ex.Message}");
                });
            }
        }

        public async void Dispose()
        {
            try
            {
                if (_socket != null)
                {
                    await _socket.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                FastLog.Error($"Error closing socket: {ex.Message}");
            }
        }
    }
}
