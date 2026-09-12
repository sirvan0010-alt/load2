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
