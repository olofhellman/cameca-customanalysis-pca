using Cameca.CustomAnalysis.Interface;
using Cameca.CustomAnalysis.Utilities;
using Prism.Ioc;
using Prism.Modularity;
using System.Diagnostics;

namespace Cameca.CustomAnalysis.Pca;

/// <summary>
/// Public <see cref="IModule"/> implementation is the entry point for AP Suite to discover and configure the custom analysis
/// </summary>
public class PcaModule : IModule
{
    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
          containerRegistry.AddCustomAnalysisUtilities(options => options.UseStandardBaseClasses = true);

        containerRegistry.Register<object, PrincipalComponentAnalysis>(PrincipalComponentAnalysis.UniqueId);
        containerRegistry.RegisterInstance(PrincipalComponentAnalysis.DisplayInfo, PrincipalComponentAnalysis.UniqueId);
        containerRegistry.Register<IAnalysisMenuFactory, PcaNodeMenuFactory>(nameof(PcaNodeMenuFactory));
        containerRegistry.Register<object, PcaViewModel>(PcaViewModel.UniqueId);
    }

    public void OnInitialized(IContainerProvider containerProvider)
    {
        // begin debug code
        var isRegistered = containerProvider.IsRegistered<IExtensionRegistry>();
        if (!isRegistered)
        {
            Debug.WriteLine("nope");
            return;
        }
        // end debug code
        var extensionRegistry = containerProvider.Resolve<IExtensionRegistry>();

        extensionRegistry.RegisterAnalysisView<PcaView, PcaViewModel>(AnalysisViewLocation.Default);
    }
}
