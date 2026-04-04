using Ocelot.Configuration;
using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.ServiceDiscovery.Providers;
using Ocelot.Values;

namespace ProjectApp.Gateway.LoadBalancing;

public class QueryBasedLoadBalancerFactory : ILoadBalancerFactory
{
    public const string LoadBalancerType = "QueryBased";

    public Response<ILoadBalancer> Get(
        DownstreamRoute route,
        ServiceProviderConfiguration serviceProviderConfiguration)
    {
        return new OkResponse<ILoadBalancer>(new QueryBasedLoadBalancer(route));
    }
}
