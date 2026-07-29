using FluentAssertions;
using SoftwareSchmiede.AutoUpdate;

namespace SoftwareSchmiede.AutoUpdate.Tests;

public sealed class AutoUpdateEventsTests
{
    [Fact]
    public void Raise_BeforeCheckSource_HonorsCancel()
    {
        var events = new AutoUpdateEvents();
        events.BeforeCheckSource += (_, args) => args.Cancel = true;

        var canceled = events.RaiseBeforeCheckSource(this);

        canceled.Should().BeTrue();
    }

    [Fact]
    public void Raise_WhenHandlerThrows_ReportsErrorAndContinues()
    {
        var events = new AutoUpdateEvents();
        AutoUpdateErrorEventArgs? captured = null;
        var secondSubscriberInvoked = false;
        events.BeforeCheckSource += (_, _) => throw new InvalidOperationException("boom");
        events.BeforeCheckSource += (_, _) => secondSubscriberInvoked = true;
        events.ErrorOccured += (_, args) => captured = args;

        var canceled = events.RaiseBeforeCheckSource(this);

        canceled.Should().BeFalse();
        secondSubscriberInvoked.Should().BeTrue("a subsequent subscriber must still run after an earlier one throws");
        captured.Should().NotBeNull();
        captured!.Error.Should().BeOfType<InvalidOperationException>();
        captured.Phase.Should().Be("BeforeCheckSource");
    }

    [Fact]
    public void Raise_WhenEarlierSubscriberCancels_LaterSubscriberCannotUndoCancellation()
    {
        var events = new AutoUpdateEvents();
        events.BeforeCheckSource += (_, args) => args.Cancel = true;
        events.BeforeCheckSource += (_, args) => args.Cancel = false;

        var canceled = events.RaiseBeforeCheckSource(this);

        canceled.Should().BeTrue("each subscriber receives its own args instance, so a later subscriber cannot revert an earlier cancellation");
    }

    [Fact]
    public void Raise_BeforeDownload_PassesSourceUriAndHonorsCancel()
    {
        var events = new AutoUpdateEvents();
        Uri? receivedUri = null;
        var sourceUri = new Uri("https://example.test/app.zip");
        events.BeforeDownload += (_, args) =>
        {
            receivedUri = args.SourceUri;
            args.Cancel = true;
        };

        var canceled = events.RaiseBeforeDownload(this, sourceUri);

        canceled.Should().BeTrue();
        receivedUri.Should().Be(sourceUri);
    }

    [Fact]
    public void Raise_BeforeInstall_PassesPackageFileAndHonorsCancel()
    {
        var events = new AutoUpdateEvents();
        FileInfo? receivedFile = null;
        var packageFile = new FileInfo(Path.Combine(Path.GetTempPath(), "app.zip"));
        events.BeforeInstall += (_, args) =>
        {
            receivedFile = args.PackageFile;
            args.Cancel = true;
        };

        var canceled = events.RaiseBeforeInstall(this, packageFile);

        canceled.Should().BeTrue();
        receivedFile.Should().Be(packageFile);
    }

    [Fact]
    public void Raise_BeforeStartUpdateScript_PassesScriptFileAndHonorsCancel()
    {
        var events = new AutoUpdateEvents();
        FileInfo? receivedFile = null;
        var scriptFile = new FileInfo(Path.Combine(Path.GetTempPath(), "update.ps1"));
        events.BeforeStartUpdateScript += (_, args) =>
        {
            receivedFile = args.ScriptFile;
            args.Cancel = true;
        };

        var canceled = events.RaiseBeforeStartUpdateScript(this, scriptFile);

        canceled.Should().BeTrue();
        receivedFile.Should().Be(scriptFile);
    }

    [Fact]
    public void Raise_WhenErrorSubscriberThrows_DoesNotPropagate()
    {
        var events = new AutoUpdateEvents();
        events.ErrorOccured += (_, _) => throw new InvalidOperationException("subscriber boom");

        var act = () => events.RaiseErrorOccured(this, new InvalidOperationException("original"), "SomePhase");

        act.Should().NotThrow();
    }

    [Fact]
    public void Raise_AfterStartUpdateScript_HasNoCancelSemantics()
    {
        var events = new AutoUpdateEvents();
        var raised = false;
        events.AfterStartUpdateScript += (_, _) => raised = true;

        events.RaiseAfterStartUpdateScript(this);

        raised.Should().BeTrue();
    }

    [Fact]
    public void Subscribe_FromMultipleThreads_IsSafe()
    {
        var events = new AutoUpdateEvents();
        var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();

        Parallel.For(0, 200, _ =>
        {
            try
            {
                void Handler(object? sender, AutoUpdateCancelEventArgs args)
                {
                }

                events.BeforeCheckSource += Handler;
                events.BeforeCheckSource -= Handler;
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        });

        exceptions.Should().BeEmpty();
    }
}
