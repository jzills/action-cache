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

    [Test]
    public async Task ValueWithRouteTemplateParameters_ConcurrentRequests_AreIsolatedPerAsyncContext()
    {
        Namespace sharedNamespace = "Account:{id}";
        var results = new System.Collections.Concurrent.ConcurrentDictionary<string, string?>();

        await Task.WhenAll(
            Task.Run(() =>
            {
                sharedNamespace.ValueWithRouteTemplateParameters = "Account/1";
                results["A"] = sharedNamespace.ValueWithRouteTemplateParameters;
            }),
            Task.Run(() =>
            {
                sharedNamespace.ValueWithRouteTemplateParameters = "Account/2";
                results["B"] = sharedNamespace.ValueWithRouteTemplateParameters;
            })
        );

        results["A"].Should().Be("Account/1");
        results["B"].Should().Be("Account/2");
    }

    [Test]
    public void ValueWithRouteTemplateParameters_WhenSet_ChangesImplicitStringConversion()
    {
        Namespace ns = "Resource:{id}";

        string keyBeforeSet = ns;
        ns.ValueWithRouteTemplateParameters = "Resource/99";
        string keyAfterSet = ns;

        keyBeforeSet.Should().Be($"{Namespace.Assembly}:Resource:{{id}}");
        keyAfterSet.Should().Be($"{Namespace.Assembly}:Resource/99");
    }
}
