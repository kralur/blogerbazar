using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BloggerBazar.Api.Routing;

public sealed class ApiV1RouteConvention : IControllerModelConvention
{
    private const string LegacyApiPrefix = "api/";
    private const string VersionedApiPrefix = "api/v1/";
    private const string WebhookPrefix = "api/webhooks/";

    public void Apply(ControllerModel controller)
    {
        var versionedSelectors = controller.Selectors
            .Where(selector => selector.AttributeRouteModel?.Template is { } template
                && template.StartsWith(LegacyApiPrefix, StringComparison.OrdinalIgnoreCase)
                && !template.StartsWith(VersionedApiPrefix, StringComparison.OrdinalIgnoreCase)
                && !template.StartsWith(WebhookPrefix, StringComparison.OrdinalIgnoreCase))
            .Select(CreateVersionedSelector)
            .ToArray();

        foreach (var selector in versionedSelectors)
        {
            controller.Selectors.Add(selector);
        }
    }

    private static SelectorModel CreateVersionedSelector(SelectorModel selector)
    {
        var route = selector.AttributeRouteModel!;
        return new SelectorModel(selector)
        {
            AttributeRouteModel = new AttributeRouteModel(route)
            {
                Name = route.Name is null ? null : $"v1-{route.Name}",
                Template = $"{VersionedApiPrefix}{route.Template![LegacyApiPrefix.Length..]}"
            }
        };
    }
}
