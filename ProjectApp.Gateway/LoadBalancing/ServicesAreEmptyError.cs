using Ocelot.Errors;

namespace ProjectApp.Gateway.LoadBalancing;

public class ServicesAreEmptyError(string message) : Error(message, OcelotErrorCode.UnknownError, 503);
