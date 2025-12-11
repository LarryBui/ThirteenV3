using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Google.Protobuf;
using Nakama;
using TienLen.Gen; // Generated Protobuf namespace
using TienLen.Unity.Domain.Aggregates;
using UnityEngine;
using System.Linq; // Added for LINQ extension methods
using VContainer;
using Serilog;

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
        private readonly IMatchMessageHandler _messageHandler;
        private readonly GameSession _gameSession;
        
        private IMatch _currentMatch;
        private ISession _session;
        private bool _socketHandlersRegistered;

        public IMatch CurrentNakamaMatch => _currentMatch; // Implementation of the property

        public event Action<string> OnError;

        [Inject]
        public NakamaGameNetwork(IClient client, ISocket socket, NakamaAuthService authService, GameModel gameModel, IMatchMessageHandler messageHandler, GameSession gameSession)
        {
            _client = client;
            _socket = socket;
            _authService = authService;
            _gameModel = gameModel; // New
            _messageHandler = messageHandler;
            _gameSession = gameSession;
            _messageHandler.OnError += msg => OnError?.Invoke(msg);
        }

        public async Task ConnectAndJoinMatchAsync()
        {
            try
            {
                // Add timeout to prevent hanging indefinitely
                using (var cts = new System.Threading.CancellationTokenSource(System.TimeSpan.FromSeconds(30)))
                {
                    // Use a fresh custom ID per run to ensure a unique test account.
                    var customId = System.Guid.NewGuid().ToString();
                    var username = $"Tester_{UnityEngine.Random.Range(1000, 9999)}";
                    _session = await _client.AuthenticateCustomAsync(customId, username, create: true);

                    if (!_socket.IsConnected)
                    {
                        _socket.ReceivedMatchState += _messageHandler.Handle;
                        _socket.ReceivedMatchPresence += OnMatchPresence;
                        _socketHandlersRegistered = true;
                        await _socket.ConnectAsync(_session);
                    }

                    // Use RPC to find or create a match authoritatively
                    var rpcResult = await _client.RpcAsync(_session, "quick_match", "{}");
                    var payload = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, string>>(rpcResult.Payload);
                    var matchId = payload["match_id"];
                    
                    _currentMatch = await _socket.JoinMatchAsync(matchId);
                    _gameSession.ConnectedPlayers = _currentMatch.Presences.ToList(); // seed with current presences
                    Debug.Log($"Joined match with ID: {_currentMatch}");
                }
            }
            catch (System.OperationCanceledException)
            {
                OnError?.Invoke("Connection timeout - server not responding");
                throw;
            }
            catch (Exception ex)
            {
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

        public async void Dispose()
        {
            try
            {
                if (_socket != null)
                {
                    if (_socketHandlersRegistered)
                    {
                        _socket.ReceivedMatchState -= _messageHandler.Handle;
                        _socket.ReceivedMatchPresence -= OnMatchPresence;
                        _socketHandlersRegistered = false;
                    }
                    await _socket.CloseAsync();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error closing socket");
            }
        }

        private void OnMatchPresence(IMatchPresenceEvent presenceEvent)
        {
            // Update connected players list based on join/leave events
            var list = _gameSession.ConnectedPlayers ?? new List<IUserPresence>();

            foreach (var join in presenceEvent.Joins)
            {
                if (_currentMatch?.Self != null && join.UserId == _currentMatch.Self.UserId)
                    continue;
                if (!list.Any(p => p.UserId == join.UserId))
                {
                    list.Add(join);
                }
            }

            foreach (var leave in presenceEvent.Leaves)
            {
                list.RemoveAll(p => p.UserId == leave.UserId);
            }

            _gameSession.ConnectedPlayers = list;
            Log.Information("[NakamaGameNetwork] Presence updated. Joins: {Joins}, Leaves: {Leaves}, Total: {Count}",
                string.Join(",", presenceEvent.Joins.Select(j => j.UserId)),
                string.Join(",", presenceEvent.Leaves.Select(l => l.UserId)),
                list.Count);
        }
    }
}
