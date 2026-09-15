using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class DockerMiniSweAgentSandboxTests
{
    [Fact]
    public void AdvertisesRequiredIsolationCapabilities()
    {
        var sandbox = new DockerMiniSweAgentSandbox("example/image");

        Assert.True(sandbox.Capabilities.IsolatedWorkspace);
        Assert.True(sandbox.Capabilities.FilesystemConstrained);
        Assert.True(sandbox.Capabilities.EnvironmentAllowListed);
        Assert.True(sandbox.Capabilities.ProcessTreeCancellation);
        Assert.Equal(AiAgentNetworkPolicy.Denied, sandbox.Capabilities.NetworkPolicy);
    }

    [Fact]
    public void BuildDockerArguments_UsesNonInteractiveEntrypointContract()
    {
        var sandbox = new DockerMiniSweAgentSandbox("example/image");
        var workspace = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "load2-docker-args-" + Guid.NewGuid())).FullName;
        try
        {
            var specification = new MiniSweAgentLaunchSpec(
                workspace,
                "deterministic smoke task",
                "sirvan0010-alt/load2",
                "0123456789abcdef0123456789abcdef01234567",
                new[] { "src" },
                TimeSpan.FromSeconds(5),
                1,
                new Dictionary<string, string>(),
                AiAgentNetworkPolicy.Denied,
                ConfigPath: "/workspace/mini-deterministic.yaml",
                Model: "deterministic");

            var args = sandbox.BuildDockerArguments(specification, workspace);

            Assert.Contains("MSWEA_CONFIGURED=true", args);
            Assert.Contains("MSWEA_SILENT_STARTUP=1", args);
            Assert.Contains("MSWEA_GLOBAL_CONFIG_DIR=/tmp/mini-swe-agent-config", args);

            Assert.Contains("--task", args);
            Assert.Equal("deterministic smoke task", args[args.IndexOf("--task") + 1]);
            Assert.Contains("--config", args);
            Assert.Contains("--model", args);

            // Must not invoke the interactive mini CLI control path.
            Assert.DoesNotContain("-y", args);
            Assert.DoesNotContain("--yolo", args);
            Assert.DoesNotContain("--exit-immediately", args);
            Assert.DoesNotContain("--agent-class", args);
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }

    [Fact]
    public async Task RejectsCredentialLikeEnvironmentKeys()
    {
        var sandbox = new DockerMiniSweAgentSandbox(
            "example/image",
            environment: new Dictionary<string, string>
            {
                ["TEST_PASSWORD"] = "not-a-real-secret"
            });

        var specification = new MiniSweAgentLaunchSpec(
            Directory.GetCurrentDirectory(),
            "test",
            "sirvan0010-alt/load2",
            "0123456789abcdef0123456789abcdef01234567",
            new[] { "src" },
            TimeSpan.FromSeconds(1),
            1,
            new Dictionary<string, string>(),
            AiAgentNetworkPolicy.Denied);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sandbox.RunAsync(specification, CancellationToken.None));
    }

    [Fact]
    public async Task RejectsNonDeniedNetworkPolicy()
    {
        var sandbox = new DockerMiniSweAgentSandbox("example/image");
        var workspace = Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "load2-docker-test-" + Guid.NewGuid())).FullName;
        try
        {
            var specification = new MiniSweAgentLaunchSpec(
                workspace,
                "test",
                "sirvan0010-alt/load2",
                "0123456789abcdef0123456789abcdef01234567",
                new[] { "src" },
                TimeSpan.FromSeconds(1),
                1,
                new Dictionary<string, string>(),
                AiAgentNetworkPolicy.Authorized);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                sandbox.RunAsync(specification, CancellationToken.None));
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
