using MailLoadTester.Core;

namespace MailLoadTester.Tests;

public sealed class MiniSweAgentDockerIntegrationTests
{
    [Fact]
    public async Task ExecutesRealMiniSweAgentInsideBoundedDockerWhenOptedIn()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("LOAD2_RUN_MINI_SWE_INTEGRATION"),
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var image = Environment.GetEnvironmentVariable("LOAD2_MINI_SWE_IMAGE");
        Assert.False(string.IsNullOrWhiteSpace(image));

        var workspace = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), "load2-mini-swe-it-" + Guid.NewGuid())).FullName;
        var configPath = Path.Combine(workspace, "mini-deterministic.yaml");

        // Deterministic DefaultAgent config — no interactive mode keys.
        // Completion is signaled by DefaultAgent exit_status=Submitted and the
        // entrypoint marker LOAD2_MINI_SWE_SMOKE_OK (not shell-echo of the action).
        await File.WriteAllTextAsync(configPath, """
            agent:
              system_template: |
                You are a deterministic integration-test agent.
              instance_template: |
                Execute the supplied integration task.
              step_limit: 2
              cost_limit: 1
            environment:
              environment_class: local
              cwd: /workspace
              timeout: 5
            model:
              model_class: deterministic
              model_name: deterministic
              outputs:
                - role: assistant
                  content: |
                    COMPLETE_TASK_AND_SUBMIT_FINAL_OUTPUT
                    integration smoke test
                  extra:
                    actions:
                      - command: echo LOAD2_MINI_SWE_ACTION_RAN
            """);

        try
        {
            var sandbox = new DockerMiniSweAgentSandbox(image!);
            var runtime = new MiniSweAgentRuntime(
                sandbox,
                "/workspace/mini-deterministic.yaml",
                "deterministic");

            var task = new AiAgentTask(
                "TEST-MINI-SWE-DOCKER-001",
                "TEST_AGENT",
                "sirvan0010-alt/load2",
                "0123456789abcdef0123456789abcdef01234567",
                new[] { "tests/MailLoadTester.Tests" },
                new[] { "mini-SWE-agent executes inside the bounded container" },
                RealTargetRequired: false,
                Authorized: false,
                TimeSpan.FromSeconds(30),
                MaxIterations: 2,
                workspace);

            var result = await runtime.RunAsync(task, workspace);

            var combined = (result.StandardOutput ?? string.Empty) + "\n" + (result.StandardError ?? string.Empty);

            // Non-interactive contract: never hit prompt_toolkit TTY abort.
            Assert.DoesNotContain("Input is not a terminal", combined, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("is not a terminal", combined, StringComparison.OrdinalIgnoreCase);

            Assert.True(
                result.ExitCode == 0,
                $"mini-SWE-agent Docker execution failed with exit code {result.ExitCode}. stderr: {result.StandardError}\nstdout: {result.StandardOutput}");

            // Real runner completed via DefaultAgent API (CI run #15 already showed exit_status Submitted).
            Assert.Contains("LOAD2_MINI_SWE_SMOKE_OK", result.StandardOutput, StringComparison.Ordinal);
            Assert.True(
                result.StandardOutput.Contains("Submitted", StringComparison.Ordinal)
                || result.StandardOutput.Contains("exit_status", StringComparison.Ordinal),
                $"Expected DefaultAgent completion markers in stdout. stdout: {result.StandardOutput}");
        }
        finally
        {
            Directory.Delete(workspace, recursive: true);
        }
    }
}
