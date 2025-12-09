using VContainer;
using VContainer.Unity;
using TienLen.Unity.Presentation.Presenters;

namespace TienLen.Unity.Presentation
{
    public class MatchLifetimeScope : LifetimeScope
    {
        protected override void Configure(IContainerBuilder builder)
        {
            // Inject existing Scene Components (MVP View/Presenters)
            builder.RegisterComponentInHierarchy<GamePresenter>();
        }
    }
}
