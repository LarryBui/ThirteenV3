using Cysharp.Threading.Tasks;

namespace TienLen.Unity.Infrastructure.Services
{
    public enum FeatureScene
    {
        GameRoom = 2, // Scene Build Index (Bootstrap=0, Master=1, GameRoom=2)
        Lobby = 3
    }

    public interface ISceneService
    {
        UniTask LoadMasterShellAsync();
        UniTask LoadFeatureAsync(FeatureScene scene);
    }
}
