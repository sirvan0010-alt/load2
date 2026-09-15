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
    public void BuildDockerArguments_ForcesNonInteractiveMiniSweAgentContract()
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

            // Root cause fix: skip configure_if_first_time() / prompt_toolkit setup.
            Assert.Contains("MSWEA_CONFIGURED=true", args);
            var envIdx = args.IndexOf("MSWEA_CONFIGURED=true");
            Assert.True(envIdx > 0);
            Assert.Equal("--env", args[envIdx - 1]);

            // DefaultAgent path — not InteractiveAgent confirm/yolo.
            Assert.Contains("--agent-class", args);
            Assert.Equal("default", args[args.IndexOf("--agent-class") + 1]);

            // Task must be explicit so CLI never opens multiline prompt.
            Assert.Contains("--task", args);
            Assert.Equal("deterministic smoke task", args[args.IndexOf("--task") + 1]);

            // Must not depend on interactive yolo/confirm flags for non-TTY.
            Assert.DoesNotContain("-y", args);
            Assert.DoesNotContain("--yolo", args);
            Assert.DoesNotContain("--exit-immediately", args);
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
