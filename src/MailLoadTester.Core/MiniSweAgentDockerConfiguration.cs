namespace MailLoadTester.Core;

public sealed record MiniSweAgentDockerConfiguration(
    string Image,
    string AgentExecutable = "mini-swe-agent",
    string DockerExecutable = "docker",
    long MemoryBytes = 2L * 1024 * 1024 * 1024,
    int CpuLimit = 2,
    int PidsLimit = 128)
{
    public static MiniSweAgentDockerConfiguration FromEnvironment(string imageVariable = "LOAD2_MINI_SWE_IMAGE")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(imageVariable);
        var image = Environment.GetEnvironmentVariable(imageVariable);
        if (string.IsNullOrWhiteSpace(image)) throw new InvalidOperationException($"Docker agent image is not configured. Set {imageVariable} explicitly before enabling external agent execution.");
        return new MiniSweAgentDockerConfiguration(image, Environment.GetEnvironmentVariable("LOAD2_MINI_SWE_EXECUTABLE") ?? "mini-swe-agent", Environment.GetEnvironmentVariable("LOAD2_DOCKER_EXECUTABLE") ?? "docker");
    }
}
