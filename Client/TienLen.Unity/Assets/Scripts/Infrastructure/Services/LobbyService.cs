using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using Nakama;
using UnityEngine;
using TienLen.Unity.Infrastructure.Network;
using TienLen.Unity.Infrastructure.Logging; // Added

namespace TienLen.Unity.Infrastructure.Services
{
    public class LobbyService
    {
        private readonly NakamaSocketService _socketService; // Keep for general Nakama socket
        private readonly IGameNetwork _gameNetwork; // New: For Authoritative Match
        private readonly GameSession _session;

        public LobbyService(NakamaSocketService socketService, IGameNetwork gameNetwork, GameSession session) // Updated constructor
        {
            _socketService = socketService;
            _gameNetwork = gameNetwork;
            _session = session;
        }

        /// <summary>
        /// Connects to the authoritative match.
        /// </summary>
        public async UniTask JoinOrCreateTableAsync()
        {
            try
            {
                // Connect to the authoritative match via IGameNetwork
                await _gameNetwork.ConnectAndJoinMatchAsync();

                // After connecting, the _gameNetwork will have populated CurrentNakamaMatch.
                IMatch currentAuthoritativeMatch = _gameNetwork.CurrentNakamaMatch;
                
                if (currentAuthoritativeMatch == null)
                {
                    throw new Exception("Failed to connect to authoritative match: No match object returned.");
                }

                _session.CurrentRoom = currentAuthoritativeMatch;
                _session.ConnectedPlayers = currentAuthoritativeMatch.Presences.ToList();
                
                // Determine host: If I'm the only one in the match, I'm the host.
                _session.IsHost = !currentAuthoritativeMatch.Presences.Any(); 
                
                FastLog.Info($"[LobbyService] Successfully connected to authoritative match: {currentAuthoritativeMatch.Id}. Am I Host? {_session.IsHost}");
            }
            catch (Exception ex)
            {
                FastLog.Error($"[LobbyService] Failed to connect to authoritative match: {ex.Message}");
                throw;
            }
        }

        public async UniTask LeaveTableAsync()
        {
            // Assuming _gameNetwork handles leaving authoritative matches.
            // A proper leave would involve _gameNetwork.LeaveMatchAsync()
            // For now, we just reset session.
            _session.Leave();
            await UniTask.CompletedTask; // Since IGameNetwork doesn't have LeaveMatchAsync yet.
        }
    }
}
