using Nakama;
using System.Linq;
using System.Collections.Generic; // Added

namespace TienLen.Unity.Infrastructure
{
    public class GameSession
    {
        /// <summary>
        /// The current Nakama Match (Table/Room) the player is connected to.
        /// </summary>
        public IMatch CurrentRoom { get; set; }
        
        /// <summary>
        /// Mutable list of other players currently in the room.
        /// </summary>
        public List<IUserPresence> ConnectedPlayers { get; set; } = new List<IUserPresence>();

        /// <summary>
        /// True if this client is the Room Owner (Host).
        /// </summary>
        public bool IsHost { get; set; }
        
        public void Leave()
        {
            CurrentRoom = null;
            ConnectedPlayers.Clear();
            IsHost = false;
        }
    }
}
