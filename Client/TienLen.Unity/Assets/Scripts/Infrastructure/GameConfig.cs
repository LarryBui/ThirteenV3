using UnityEngine;

namespace TienLen.Unity.Infrastructure
{
    [CreateAssetMenu(fileName = "GameConfig", menuName = "TienLen/Game Config")]
    public class GameConfig : ScriptableObject
    {
        [Header("Scene Configuration")]
        [Tooltip("The name of the persistent shell scene.")]
        public string MasterSceneName = "Master";

        [Tooltip("The name of the gameplay room scene.")]
        public string GameRoomSceneName = "GameRoom";
        
        [Tooltip("The name of the lobby scene.")]
        public string LobbySceneName = "Lobby";

        [Header("Network Configuration")]
        public string NakamaHost = "localhost";
        public int NakamaPort = 7350;
        public string NakamaKey = "defaultkey";
    }
}
