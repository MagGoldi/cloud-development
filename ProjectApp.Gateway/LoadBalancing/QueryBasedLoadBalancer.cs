using Ocelot.Configuration;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.Values;

namespace ProjectApp.Gateway.LoadBalancing;

public class QueryBasedLoadBalancer(DownstreamRoute route) : ILoadBalancer
{
    private readonly List<ServiceHostAndPort> _hosts = route.DownstreamAddresses
            .Select(a => new ServiceHostAndPort(a.Host, a.Port))
            .ToList();

    public QueryBasedLoadBalancer(IEnumerable<Service> services, DownstreamRoute route)
        : this(route)
    {
    }

    /// <summary>
    /// Тип используемого балансировщика нагрузки.
    /// </summary>
    public string Type => nameof(QueryBasedLoadBalancer);

    public Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        if (_hosts.Count == 0)
            return Task.FromResult<Response<ServiceHostAndPort>>(
                new ErrorResponse<ServiceHostAndPort>(
                    new ServicesAreEmptyError("No downstream hosts configured")));

        var idRaw = httpContext.Request.Query["id"].FirstOrDefault()
                    ?? ExtractIdFromPath(httpContext.Request.Path);

        int index = 0;
        if (int.TryParse(idRaw, out var id))
            index = Math.Abs(id) % _hosts.Count;

        return Task.FromResult<Response<ServiceHostAndPort>>(
            new OkResponse<ServiceHostAndPort>(_hosts[index]));
    }

    public void Release(ServiceHostAndPort hostAndPort) { }

    private static string? ExtractIdFromPath(PathString path)
    {
        var segments = path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments?.LastOrDefault();
    }
}
