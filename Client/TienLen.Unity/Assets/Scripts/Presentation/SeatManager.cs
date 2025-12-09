using System.Collections.Generic;
using UnityEngine;
using Nakama; // For IUserPresence
using System.Linq; // NEW

namespace TienLen.Unity.Presentation
{
    public class SeatManager : MonoBehaviour
    {
        // Drag these Transforms from the scene to represent player positions
        // This is a fixed setup for 4 players. For dynamic, we'd use a List<Transform>
        [Header("Player Seat Transforms (Ordered)")]
        [Tooltip("Your player's hand container or avatar position")]
        public Transform LocalPlayerHandPosition; // Usually where HandView's CardContainer is

        [Tooltip("Opponent 1's hand container or avatar position")]
        public Transform Opponent1HandPosition; 

        [Tooltip("Opponent 2's hand container or avatar position")]
        public Transform Opponent2HandPosition;

        [Tooltip("Opponent 3's hand container or avatar position")]
        public Transform Opponent3HandPosition;

        // A way to map User IDs to these positions. This will be populated by GamePresenter.
        private Dictionary<string, Transform> _playerToHandMap = new Dictionary<string, Transform>();

        /// <summary>
        /// Populates the mapping of User IDs to their respective hand positions.
        /// This should be called by GamePresenter when players join/leave.
        /// </summary>
        /// <param name="localUserId">The ID of the local player.</param>
        /// <param name="connectedPlayers">List of other connected players.</param>
        public void SetupPlayerSeats(string localUserId, List<IUserPresence> connectedPlayers)
        {
            _playerToHandMap.Clear();

            // Always add local player first
            _playerToHandMap[localUserId] = LocalPlayerHandPosition;

            // Add opponents in a consistent order (e.g., based on sorted UserId or join order)
            // For now, a simple sequential assignment to fixed positions.
            int opponentIndex = 0;
            var sortedOpponents = connectedPlayers.OrderBy(p => p.UserId).ToList(); // Stable sort
            
            foreach (var presence in sortedOpponents)
            {
                if (opponentIndex == 0) _playerToHandMap[presence.UserId] = Opponent1HandPosition;
                else if (opponentIndex == 1) _playerToHandMap[presence.UserId] = Opponent2HandPosition;
                else if (opponentIndex == 2) _playerToHandMap[presence.UserId] = Opponent3HandPosition;
                // Add more if more than 4 players, or use a list dynamically
                opponentIndex++;
            }
        }

        /// <summary>
        /// Gets the world position of a player's hand/avatar for animation purposes.
        /// </summary>
        /// <param name="userId">The ID of the player whose hand position is requested.</param>
        /// <returns>The world position of the player's hand, or Vector3.zero if not found.</returns>
        public Vector3 GetHandPosition(string userId)
        {
            if (_playerToHandMap.TryGetValue(userId, out Transform handTransform))
            {
                return handTransform.position;
            }
            Debug.LogWarning($"Hand position for userId {userId} not found.");
            return Vector3.zero; 
        }
    }
}
