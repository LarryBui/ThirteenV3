using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using TienLen.Gen; // Generated Protobuf namespace
using TienLen.Unity.Infrastructure.Logging;
using TienLen.Unity.Domain.Aggregates;
using UnityEngine;
using System.Linq; // Added for LINQ extension methods
using VContainer;

namespace TienLen.Unity.Infrastructure.Network
{
    public interface IGameNetwork
    {
        event Action<string> OnError;

        IMatch CurrentNakamaMatch { get; } // Added

        Task ConnectAndJoinMatchAsync();
        Task SendPlayCardAsync(List<int> cardIndices);
        Task SendPassAsync();
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
            FastLog.Info("[[[[[[ ConnectAndJoinMatchAsync CALLED ]]]]]]");
            try
            {
                // Add timeout to prevent hanging indefinitely
                using (var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(30)))
                {
                    // Use a fresh custom ID per run to ensure a unique test account.
                    var customId = System.Guid.NewGuid().ToString();
                    var username = $"Tester_{UnityEngine.Random.Range(1000, 9999)}";
                    _session = await _client.AuthenticateCustomAsync(customId, username, create: true);
                    
                    FastLog.Info($"Authenticated new test user as {_session.Username} ({_session.UserId})");

                    if (!_socket.IsConnected)
                    {
                        _socket.ReceivedMatchState += OnReceivedMatchState;
                        await _socket.ConnectAsync(_session);
                        FastLog.Info("Socket Connected");
                    }

                    FastLog.Info("[[[[[[ About to Quick Match RPC... ]]]]]]");
                    // Use RPC to find or create a match authoritatively
                    var rpcResult = await _client.RpcAsync(_session, "quick_match", "{}");
                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(rpcResult.Payload);
                    var matchId = payload["match_id"];
                    
                    _currentMatch = await _socket.JoinMatchAsync(matchId);
                    FastLog.Info($"[[[[[[ Joined Match via Quick Match RPC, Match ID: {_currentMatch.Id} ]]]]]]");
                    
                    FastLog.Info($"Joined Match: {_currentMatch.Id}");
                }
            }
            catch (System.OperationCanceledException)
            {
                FastLog.Error("Network timeout: Connection took too long");
                OnError?.Invoke("Connection timeout - server not responding");
                throw;
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

        public async Task SendPassAsync()
        {
            if (_currentMatch == null) return;
            await _socket.SendMatchStateAsync(_currentMatch.Id, (long)OpCode.OpPass, Array.Empty<byte>());
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
                if (state?.State == null || state.State.Length == 0)
                {
                    FastLog.Warn("Received empty match state");
                    return;
                }

                switch ((OpCode)state.OpCode)
                {
                    case OpCode.OpMatchStart:
                        try
                        {
                            var startPacket = MatchStartPacket.Parser.ParseFrom(state.State);
                            // Convert Protobuf Hand to Domain Hand
                            var domainHand = new TienLen.Unity.Domain.Aggregates.Hand();
                            foreach (var protoCard in startPacket.Hand)
                            {
                                domainHand.AddCard(new TienLen.Unity.Domain.ValueObjects.Card((TienLen.Unity.Domain.Enums.Rank)protoCard.Rank, (TienLen.Unity.Domain.Enums.Suit)protoCard.Suit));
                            }
                            MainThreadDispatcher.Enqueue(() => {
                                _gameModel.SetIsPlaying(true); // Match has started
                                _gameModel.SetPlayerHand(domainHand);
                                _gameModel.SetMatchOwner(startPacket.OwnerId);
                            });
                        }
                        catch (Exception ex)
                        {
                            FastLog.Error($"Failed to parse MatchStartPacket: {ex.Message}");
                            MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Protocol error: {ex.Message}"));
                        }
                        break;

                    case OpCode.OpTurnUpdate:
                        try
                        {
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
                        }
                        catch (Exception ex)
                        {
                            FastLog.Error($"Failed to parse TurnUpdatePacket: {ex.Message}");
                            MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Protocol error: {ex.Message}"));
                        }
                        break;
                    
                    case OpCode.OpError:
                        var errorMsg = System.Text.Encoding.UTF8.GetString(state.State);
                        MainThreadDispatcher.Enqueue(() => 
                        {
                            OnError?.Invoke(errorMsg);
                        });
                        break;

                    case OpCode.OpOwnerUpdate:
                        var ownerId = System.Text.Encoding.UTF8.GetString(state.State); // Assuming OwnerId is sent as raw string
                        MainThreadDispatcher.Enqueue(() => {
                            _gameModel.SetMatchOwner(ownerId);
                        });
                        break;

                    case OpCode.OpGameOver:
                        try
                        {
                            var gameOverPacket = GameOverPacket.Parser.ParseFrom(state.State);
                            MainThreadDispatcher.Enqueue(() => {
                                _gameModel.SetGameOver(gameOverPacket.WinnerId); // Need to add SetGameOver to GameModel
                            });
                        }
                        catch (Exception ex)
                        {
                            FastLog.Error($"Failed to parse GameOverPacket: {ex.Message}");
                            MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Protocol error: {ex.Message}"));
                        }
                        break;

                    case OpCode.OpMatchState: // For late joiners / spectators
                        try
                        {
                            var matchStatePacket = MatchStatePacket.Parser.ParseFrom(state.State);
                            MainThreadDispatcher.Enqueue(() => {
                                _gameModel.SetIsPlaying(matchStatePacket.IsPlaying); // Need to add SetIsPlaying to GameModel
                                _gameModel.SetMatchOwner(matchStatePacket.OwnerId);
                                _gameModel.UpdateBoard(matchStatePacket.Board.Select(c => new TienLen.Unity.Domain.ValueObjects.Card((TienLen.Unity.Domain.Enums.Rank)c.Rank, (TienLen.Unity.Domain.Enums.Suit)c.Suit)).ToList()); // Convert proto cards
                                _gameModel.SetActivePlayer(matchStatePacket.ActivePlayerId);
                                _gameModel.SetPlayerIds(matchStatePacket.PlayerIds.ToList()); // Need to add SetPlayerIds to GameModel
                            });
                        }
                        catch (Exception ex)
                        {
                            FastLog.Error($"Failed to parse MatchStatePacket: {ex.Message}");
                            MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Protocol error: {ex.Message}"));
                        }
                        break;

                    case OpCode.OpHandUpdate:
                        try
                        {
                            var handPacket = HandUpdatePacket.Parser.ParseFrom(state.State);
                            var domainHand = new TienLen.Unity.Domain.Aggregates.Hand();
                            foreach (var protoCard in handPacket.Hand)
                            {
                                domainHand.AddCard(new TienLen.Unity.Domain.ValueObjects.Card((TienLen.Unity.Domain.Enums.Rank)protoCard.Rank, (TienLen.Unity.Domain.Enums.Suit)protoCard.Suit));
                            }
                            MainThreadDispatcher.Enqueue(() => _gameModel.SetPlayerHand(domainHand));
                        }
                        catch (Exception ex)
                        {
                            FastLog.Error($"Failed to parse HandUpdatePacket: {ex.Message}");
                            MainThreadDispatcher.Enqueue(() => OnError?.Invoke($"Protocol error: {ex.Message}"));
                        }
                        break;

                    default:
                        FastLog.Warn($"Unknown OpCode: {state.OpCode}");
                        break;
                }
            }
            catch (Exception ex)
            {
                FastLog.Error($"Fatal error in OnReceivedMatchState: {ex.Message}");
                MainThreadDispatcher.Enqueue(() => 
                {
                    OnError?.Invoke($"Fatal protocol error: {ex.Message}");
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
