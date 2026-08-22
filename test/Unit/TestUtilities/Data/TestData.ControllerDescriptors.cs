using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System.Linq.Expressions;
using Unit.TestUtiltiies.Extensions;

namespace Unit.TestUtiltiies.Data;

public static partial class TestData
{
    private static readonly RouteValueDictionary RouteValues = new RouteValueDictionary
    {
        { "area", "someArea" },
        { "controller", "someController" }, 
        { "action", "someAction" }
    };

    public static IEnumerable<TestCaseData> GetControllerDescriptors() =>
    [
        CreateTestCase<SampleController, IActionResult>(controller => controller.OneParameter(1), RouteValues),
        CreateTestCase<SampleController, IActionResult>(controller => controller.TwoParametersWithEqualOrder(1, "test"), RouteValues),
        CreateTestCase<SampleController, IActionResult>(controller => controller.TwoParametersAscendingOrder(1, "test"), RouteValues),
        CreateTestCase<SampleController, IActionResult>(controller => controller.TwoParametersDescendingOrder(1, "test"), RouteValues),
        CreateTestCase<SampleController, IActionResult>(controller => controller.TwoParametersOneComplex(1, new SampleModel()), RouteValues),
        CreateTestCase<SampleController, IActionResult>(controller => controller.TwoParametersOneComplexNested(1, new SampleNestedModel()), RouteValues)
    ];

    private static TestCaseData CreateTestCase<TController, TResult>(
        Expression<Func<TController, TResult>> actionExpression, 
        RouteValueDictionary routeValues
    ) where TController : Controller
    {
        var controllerActions = ControllerExtensions.GetControllerActionDescriptorsWithValues(actionExpression);
        var actionArguments = controllerActions
            .ToDictionary(
                controllerAction => controllerAction.ParameterDescriptor.Name, 
                controllerAction => controllerAction.ParameterValue
            );

        return new TestCaseData(
            controllerActions.Select(controllerAction => controllerAction.ParameterDescriptor),
            routeValues,
            actionArguments
        );
    }

    private class SampleController : Controller
    {
        public IActionResult OneParameter(int _) => Ok();
        public IActionResult TwoParametersWithEqualOrder(int _, string __) => Ok();
        public IActionResult TwoParametersAscendingOrder(int _, string __) => Ok();
        public IActionResult TwoParametersDescendingOrder(int _, string __) => Ok();
        public IActionResult TwoParametersOneComplex(int _, SampleModel __) => Ok();
        public IActionResult TwoParametersOneComplexNested(int _, SampleNestedModel __) => Ok();
    }

    private class SampleModel
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
    }

    private class SampleNestedModel
    {
        public Guid Id { get; set; }
        public SampleModel Sample { get; set; } = new();
    }
}