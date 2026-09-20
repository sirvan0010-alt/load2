using MailLoadTester.Core;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class DockerMiniSweAgentSandboxTests
{
    [Fact]
    public void BuildsHardenedDockerArgumentsWithNetworkDisabled()
    {
        var sandbox = new DockerMiniSweAgentSandbox(
            "load2-agent:test",
            dockerExecutable: "docker",
            memoryBytes: 128 * 1024 * 1024,
            cpuLimit: 1,
            pidsLimit: 32);

        var spec = Specification();
        var args = sandbox.BuildDockerArguments(spec, Path.GetTempPath());

        Assert.Equal("run", args[0]);
        AssertContainsPair(args, "--network", "none");
        Assert.Contains(args, "--read-only");
        AssertContainsPair(args, "--cap-drop", "ALL");
        AssertContainsPair(args, "--security-opt", "no-new-privileges");
        AssertContainsPair(args, "--pids-limit", "32");
        AssertContainsPair(args, "--memory", "134217728b");
        AssertContainsPair(args, "--cpus", "1");
        Assert.Contains(args, "/tmp:rw,noexec,nosuid,size=256m");
    }

    [Theory]
    [InlineData("PASSWORD")]
    [InlineData("API_TOKEN")]
    [InlineData("CLIENT_SECRET")]
    [InlineData("API_KEY")]
    [InlineData("AUTHORIZATION")]
    public void RejectsCredentialLikeEnvironmentKeys(string key)
    {
        var sandbox = new DockerMiniSweAgentSandbox(
            "load2-agent:test",
            new Dictionary<string, string> { [key] = "do-not-pass" });

        var exception = Assert.Throws<ArgumentException>(() =>
            sandbox.BuildDockerArguments(Specification(), Path.GetTempPath()));

        Assert.Contains("Credential-like", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsNetworkPolicyOtherThanDenied()
    {
        var sandbox = new DockerMiniSweAgentSandbox("load2-agent:test");
        var specification = Specification() with { NetworkPolicy = AiAgentNetworkPolicy.Authorized };

        var exception = Assert.Throws<InvalidOperationException>(() =>
            sandbox.RunAsync(specification, CancellationToken.None));

        Assert.Contains("network access", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsFilesystemRootWorkspace()
    {
        var sandbox = new DockerMiniSweAgentSandbox("load2-agent:test");

        var exception = Assert.Throws<ArgumentException>(() =>
            sandbox.RunAsync(Specification(), CancellationToken.None));

        Assert.Contains("filesystem root", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static MiniSweAgentLaunchSpec Specification() => new(
        Path.GetTempPath(),
        "safe test task",
        "sirvan0010-alt/load2",
        "0123456789abcdef0123456789abcdef01234567",
        new[] { "src/MailLoadTester.Core" },
        TimeSpan.FromSeconds(5),
        1,
        new Dictionary<string, string>(),
        AiAgentNetworkPolicy.Denied);

    private static void AssertContainsPair(IReadOnlyList<string> args, string key, string value)\n    {\n        var index = args.IndexOf(key);\n        Assert.True(index >= 0 && index + 1 < args.Count);\n        Assert.Equal(value, args[index + 1]);\n    }
}
