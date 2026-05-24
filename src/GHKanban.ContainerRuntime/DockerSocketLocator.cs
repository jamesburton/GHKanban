using System.Runtime.InteropServices;

namespace GHKanban.ContainerRuntime;

public static class DockerSocketLocator
{
    public static Uri Resolve()
    {
        var dockerHost = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(dockerHost))
            return new Uri(dockerHost);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return new Uri("npipe://./pipe/docker_engine");

        return new Uri("unix:///var/run/docker.sock");
    }
}
