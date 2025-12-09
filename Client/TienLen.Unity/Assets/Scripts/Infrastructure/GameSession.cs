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
        
        public void Leave()
        {
            CurrentRoom = null;
            ConnectedPlayers.Clear();
        }
    }
}
