using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TienLen.Unity.Infrastructure
{
    public class BootstrapLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            Debug.Log("[BootstrapLifetimeScope] Configuring...");
            // Register the BootstrapLoader found in the scene hierarchy.
            // This allows VContainer to inject its dependencies (like NakamaAuthService, ISceneService).
            builder.RegisterComponentInHierarchy<BootstrapLoader>();
        }
    }
}
