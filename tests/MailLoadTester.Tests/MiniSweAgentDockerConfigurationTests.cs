using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class MiniSweAgentDockerConfigurationTests
{
    [Fact]
    public void FromEnvironmentRequiresExplicitImage()
    {
        const string variable = "LOAD2_TEST_MINI_SWE_IMAGE_MISSING";
        Environment.SetEnvironmentVariable(variable, null);
        try
        {
            var exception = Assert.Throws<InvalidOperationException>(() =>
                MiniSweAgentDockerConfiguration.FromEnvironment(variable));

            Assert.Contains(variable, exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }

    [Fact]
    public void FromEnvironmentReadsOnlyNonSecretRuntimeSettings()
    {
        const string variable = "LOAD2_TEST_MINI_SWE_IMAGE";
        Environment.SetEnvironmentVariable(variable, "ghcr.io/example/load2-agent:test");
        try
        {
            var configuration = MiniSweAgentDockerConfiguration.FromEnvironment(variable);

            Assert.Equal("ghcr.io/example/load2-agent:test", configuration.Image);
            Assert.Equal("mini-swe-agent", configuration.AgentExecutable);
            Assert.Equal("docker", configuration.DockerExecutable);
        }
        finally
        {
            Environment.SetEnvironmentVariable(variable, null);
        }
    }
}
