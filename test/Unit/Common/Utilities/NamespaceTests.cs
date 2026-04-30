using ActionCache.Utilities;

namespace Unit.Common.Utilities;

[TestFixture]
public class NamespaceTests
{
    [Test]
    public void ImplicitConversionToString_Always_PrefixesWithAssemblyName()
    {
        Namespace ns = "MyNamespace";

        string result = ns;

        result.Should().Be($"{Namespace.Assembly}:MyNamespace");
    }

    [Test]
    public void ImplicitConversionFromString_Always_SetsValue()
    {
        Namespace ns = "MyNamespace";

        ns.Value.Should().Be("MyNamespace");
    }

    [Test]
    public void Create_Always_ConcatenatesAssemblyNamespaceAndKey()
    {
        Namespace ns = "MyNamespace";

        var result = ns.Create("SomeKey");

        result.Should().Be($"{Namespace.Assembly}:MyNamespace:SomeKey");
    }

    [Test]
    public void ImplicitConversionToString_WhenRouteTemplateParametersSet_UsesThemInsteadOfValue()
    {
        var ns = new Namespace("MyNamespace") { ValueWithRouteTemplateParameters = "MyNamespace/42" };

        string result = ns;

        result.Should().Be($"{Namespace.Assembly}:MyNamespace/42");
    }

    [Test]
    public void Create_WhenRouteTemplateParametersSet_UsesThemInsteadOfValue()
    {
        var ns = new Namespace("MyNamespace") { ValueWithRouteTemplateParameters = "MyNamespace/42" };

        var result = ns.Create("SomeKey");

        result.Should().Be($"{Namespace.Assembly}:MyNamespace/42:SomeKey");
    }
}
