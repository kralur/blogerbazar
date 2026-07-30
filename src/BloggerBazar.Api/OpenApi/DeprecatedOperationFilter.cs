using System.Reflection;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace BloggerBazar.Api.OpenApi;

public sealed class DeprecatedOperationFilter : IOperationFilter
{
    private const string LegacyNotice = "Deprecated: retained only for backward compatibility. New clients must use the current marketplace API.";

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        if (context.MethodInfo.GetCustomAttribute<ObsoleteAttribute>() is null)
        {
            return;
        }

        operation.Deprecated = true;
        operation.Description = string.IsNullOrWhiteSpace(operation.Description)
            ? LegacyNotice
            : $"{LegacyNotice}\n\n{operation.Description}";
    }
}
