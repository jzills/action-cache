using System.Linq.Expressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace Unit.TestUtiltiies.Extensions;

public static class ControllerExtensions
{
    public struct ControllerParameterWithValue
    {
        public ControllerParameterDescriptor ParameterDescriptor { get; set; }
        public object ParameterValue { get; set; }
    }

    public static ControllerParameterWithValue[] GetControllerActionDescriptorsWithValues<TController, TResult>(
        Expression<Func<TController, TResult>> actionExpression
    ) where TController : Controller
    {
        if (!(actionExpression.Body is MethodCallExpression methodCall))
        {
            throw new InvalidOperationException("The provided expression is not a valid method call.");
        }

        var methodInfo = methodCall.Method;
        if (methodInfo is null)
        {
            throw new InvalidOperationException($"Method not found on controller.");
        }

        var parameters = methodInfo.GetParameters();
        var argumentValues = methodCall.Arguments.Select(GetExpressionValue).ToArray();

        return parameters.Select((parameter, index) => new ControllerParameterWithValue
        {
            ParameterDescriptor = new ControllerParameterDescriptor
            {
                Name = parameter.Name!,
                ParameterInfo = parameter,
                ParameterType = parameter.ParameterType
            },
            ParameterValue = argumentValues[index]
        }).ToArray();
    }

    private static object GetExpressionValue(Expression expression) =>
        Expression.Lambda(expression)
            .Compile()
            .DynamicInvoke()!;
}