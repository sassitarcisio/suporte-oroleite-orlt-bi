namespace OroBI.Application.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        var references = typeof(ApplicationAssemblyMarker).Assembly.GetReferencedAssemblies();

        Assert.DoesNotContain(references, reference => reference.Name == "OroBI.Infrastructure");
    }
}
