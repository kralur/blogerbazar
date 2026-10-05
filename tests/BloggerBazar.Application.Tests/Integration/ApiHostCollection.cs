namespace BloggerBazar.Application.Tests.Integration;

// Every test class that boots the API through WebApplicationFactory joins this collection.
// Program configures Serilog's static bootstrap logger, and two hosts starting in parallel
// race to freeze it; the losing host fails to build. Running them sequentially avoids that.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ApiHostCollection
{
    public const string Name = "API host";
}
