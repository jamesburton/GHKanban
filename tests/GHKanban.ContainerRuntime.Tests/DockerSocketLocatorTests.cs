using GHKanban.ContainerRuntime;
using Xunit;

namespace GHKanban.ContainerRuntime.Tests;

public class DockerSocketLocatorTests
{
    [Fact]
    public void DockerHost_env_var_takes_precedence()
    {
        var prev = Environment.GetEnvironmentVariable("DOCKER_HOST");
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", "tcp://localhost:2375");
            Assert.Equal("tcp://localhost:2375", DockerSocketLocator.Resolve().ToString().TrimEnd('/'));
        }
        finally { Environment.SetEnvironmentVariable("DOCKER_HOST", prev); }
    }

    [Fact]
    public void Defaults_to_platform_default_when_env_unset()
    {
        var prev = Environment.GetEnvironmentVariable("DOCKER_HOST");
        try
        {
            Environment.SetEnvironmentVariable("DOCKER_HOST", null);
            var uri = DockerSocketLocator.Resolve();
            Assert.True(uri.Scheme is "npipe" or "unix" or "http", $"unexpected scheme: {uri.Scheme}");
        }
        finally { Environment.SetEnvironmentVariable("DOCKER_HOST", prev); }
    }
}
