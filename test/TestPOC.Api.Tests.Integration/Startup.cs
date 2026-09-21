using Microsoft.Extensions.DependencyInjection;
using TestPOC.Api.Tests.Integration.TestInfrastructure;
using TestPOC.TestInfrastructure.Containers;

namespace TestPOC.Api.Tests.Integration;

/// <summary>
/// Xunit.DependencyInjection Startup — MUST live in the assembly root namespace
/// as <c>{AssemblyName}.Startup</c>, otherwise Xunit.DependencyInjection throws
/// "Default startup is required." See https://github.com/pengweiqhca/Xunit.DependencyInjection#4-default-startup.
///
/// Services registered here are available for ctor-injection into any test class.
/// </summary>
public sealed class Startup
{
	public void ConfigureServices(IServiceCollection services)
	{
		services.AddSingleton(SharedPostgresContainerReference.Instance);
		services.AddSingleton(SourceContainerReference.Instance);
		services.AddSingleton<ApiTestFactory>();
	}
}
