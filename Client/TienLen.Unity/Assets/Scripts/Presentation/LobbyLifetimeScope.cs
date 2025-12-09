using TienLen.Unity.Presentation.Presenters;
using VContainer;
using VContainer.Unity;

    public class LobbyLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            UnityEngine.Debug.Log("[LobbyLifetimeScope] Configuring...");
            builder.RegisterComponentInHierarchy<LobbyPresenter>();
        }
    }
