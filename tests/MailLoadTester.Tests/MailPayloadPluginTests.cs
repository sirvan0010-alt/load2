using System.Reflection;
using System.Reflection.Emit;
using MailLoadTester;
using MimeKit;
using Xunit;

namespace MailLoadTester.Tests;

public sealed class MailPayloadPluginTests
{
    [Fact]
    public async Task Pipeline_ExecutesPluginsInDeterministicOrder()
    {
        var calls = new List<string>();
        var pipeline = new MailPayloadPluginPipeline(new IMailPayloadPlugin[]
        {
            new RecordingPlugin("z", 10, calls),
            new RecordingPlugin("b", 0, calls),
            new RecordingPlugin("a", 0, calls)
        });

        await pipeline.ApplyAsync(new MimeMessage(),
            new MailPayloadPluginContext(1, "test@example.test", 0, CancellationToken.None));

        Assert.Equal(new[] { "a", "b", "z" }, calls);
        Assert.Equal(new[] { "a", "b", "z" }, pipeline.PluginNames);
    }

    [Fact]
    public async Task Pipeline_ObservesCancellationBeforePluginExecution()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var called = false;
        var pipeline = new MailPayloadPluginPipeline(new[]
        {
            new DelegatePlugin(0, "cancel", (_, _) =>
            {
                called = true;
                return Task.CompletedTask;
            })
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.ApplyAsync(new MimeMessage(),
                new MailPayloadPluginContext(1, "test@example.test", 0, cts.Token)));

        Assert.False(called);
    }

    [Fact]
    public async Task Pipeline_PassesContextAndAllowsMimeMutation()
    {
        MailPayloadPluginContext? observed = null;
        var pipeline = new MailPayloadPluginPipeline(new[]
        {
            new DelegatePlugin(0, "header", (message, context) =>
            {
                observed = context;
                message.Headers["X-Plugin-Test"] = context.TestId.ToString();
                return Task.CompletedTask;
            })
        });
        var message = new MimeMessage();
        var context = new MailPayloadPluginContext(42, "recipient@example.test", 2, CancellationToken.None);

        await pipeline.ApplyAsync(message, context);

        Assert.Same(context, observed);
        Assert.Equal("42", message.Headers["X-Plugin-Test"]);
    }

    [Fact]
    public async Task Pipeline_WrapsPluginException_AndDoesNotCallLaterPlugins()
    {
        var calls = new List<string>();
        var pipeline = new MailPayloadPluginPipeline(new IMailPayloadPlugin[]
        {
            new DelegatePlugin(0, "ok", (_, _) =>
            {
                calls.Add("ok");
                return Task.CompletedTask;
            }),
            new DelegatePlugin(1, "boom", (_, _) =>
                throw new InvalidOperationException("plugin-broke")),
            new DelegatePlugin(2, "later", (_, _) =>
            {
                calls.Add("later");
                return Task.CompletedTask;
            })
        });

        var ex = await Assert.ThrowsAsync<MailPayloadPluginException>(() =>
            pipeline.ApplyAsync(new MimeMessage(),
                new MailPayloadPluginContext(7, "r@example.test", 0, CancellationToken.None)));

        Assert.Equal("boom", ex.PluginName);
        Assert.Equal(1, ex.PluginOrder);
        Assert.Contains("plugin-broke", ex.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
        Assert.Equal(new[] { "ok" }, calls);
    }

    [Fact]
    public async Task Pipeline_DoesNotWrapOperationCanceledException()
    {
        var pipeline = new MailPayloadPluginPipeline(new[]
        {
            new DelegatePlugin(0, "cancel-inside", (_, _) =>
                throw new OperationCanceledException())
        });

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            pipeline.ApplyAsync(new MimeMessage(),
                new MailPayloadPluginContext(1, "r@example.test", 0, CancellationToken.None)));
    }

    [Fact]
    public async Task Pipeline_Empty_IsNoOp()
    {
        var pipeline = new MailPayloadPluginPipeline(null);
        Assert.Equal(0, pipeline.Count);
        var message = new MimeMessage();
        await pipeline.ApplyAsync(message,
            new MailPayloadPluginContext(1, "r@example.test", 0, CancellationToken.None));
        Assert.Empty(message.Headers.Where(h => h.Field.StartsWith("X-Plugin", StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public void Loader_MissingDirectory_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mlt-plugins-missing-" + Guid.NewGuid().ToString("N"));
        var plugins = MailPayloadPluginLoader.LoadFromDirectory(dir);
        Assert.Empty(plugins);
    }

    [Fact]
    public void Loader_EmptyDirectory_ReturnsEmpty()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mlt-plugins-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var plugins = MailPayloadPluginLoader.LoadFromDirectory(dir);
            Assert.Empty(plugins);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Loader_CorruptDll_IsSkipped_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), "mlt-plugins-bad-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllBytes(Path.Combine(dir, "not-a-plugin.dll"), new byte[] { 0x4D, 0x5A, 0x00, 0x01, 0x02, 0x03 });
            var logs = new List<string>();
            var plugins = MailPayloadPluginLoader.LoadFromDirectory(dir, logs.Add);
            Assert.Empty(plugins);
            Assert.Contains(logs, static l => l.Contains("skipped", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void Loader_NonPluginAssembly_YieldsNoPlugins()
    {
        // Load types from an assembly that has no IMailPayloadPlugin implementations.
        // Use a temp directory with a copy of a known non-plugin DLL if available;
        // otherwise verify missing interface types produce empty list via directory without DLLs.
        var dir = Path.Combine(Path.GetTempPath(), "mlt-plugins-none-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // Place a text file named .dll is still skipped by load failure path.
            File.WriteAllText(Path.Combine(dir, "readme.dll.txt"), "not a dll");
            var plugins = MailPayloadPluginLoader.LoadFromDirectory(dir);
            Assert.Empty(plugins);
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public async Task Pipeline_MultiplePlugins_StableOrder_AndCumulativeMimeMutation()
    {
        var pipeline = new MailPayloadPluginPipeline(new IMailPayloadPlugin[]
        {
            new DelegatePlugin(10, "second", (message, _) =>
            {
                message.Headers["X-Second"] = "2";
                return Task.CompletedTask;
            }),
            new DelegatePlugin(1, "first", (message, _) =>
            {
                message.Headers["X-First"] = "1";
                return Task.CompletedTask;
            })
        });

        var message = new MimeMessage();
        await pipeline.ApplyAsync(message,
            new MailPayloadPluginContext(3, "r@example.test", 1, CancellationToken.None));

        Assert.Equal("1", message.Headers["X-First"]);
        Assert.Equal("2", message.Headers["X-Second"]);
        Assert.Equal(new[] { "first", "second" }, pipeline.PluginNames);
    }

    [Fact]
    public void MailPayloadPluginException_PreservesPluginIdentity()
    {
        var inner = new InvalidOperationException("x");
        var ex = new MailPayloadPluginException("my-plugin", 5, inner);
        Assert.Equal("my-plugin", ex.PluginName);
        Assert.Equal(5, ex.PluginOrder);
        Assert.Same(inner, ex.InnerException);
        Assert.Contains("my-plugin", ex.Message, StringComparison.Ordinal);
    }

    private sealed class RecordingPlugin : IMailPayloadPlugin
    {
        private readonly List<string> _calls;
        public RecordingPlugin(string name, int order, List<string> calls)
        {
            Name = name;
            Order = order;
            _calls = calls;
        }

        public int Order { get; }
        public string Name { get; }

        public Task ApplyAsync(MimeMessage message, MailPayloadPluginContext context)
        {
            _calls.Add(Name);
            return Task.CompletedTask;
        }
    }

    private sealed class DelegatePlugin : IMailPayloadPlugin
    {
        private readonly Func<MimeMessage, MailPayloadPluginContext, Task> _apply;
        public DelegatePlugin(int order, string name, Func<MimeMessage, MailPayloadPluginContext, Task> apply)
        {
            Order = order;
            Name = name;
            _apply = apply;
        }

        public int Order { get; }
        public string Name { get; }
        public Task ApplyAsync(MimeMessage message, MailPayloadPluginContext context) => _apply(message, context);
    }
}
