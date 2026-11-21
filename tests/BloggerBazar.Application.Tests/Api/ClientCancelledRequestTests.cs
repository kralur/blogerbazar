using System.Text.Json;
using BloggerBazar.Api.Middleware;
using BloggerBazar.Application.Notifications;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BloggerBazar.Application.Tests.Api;

// A request the person abandoned (left the screen, typed the next letter of a search) is not a server error: before
// this it was logged as an unhandled exception, answered with 500 and counted towards the owners' error alert (D60).
public sealed class ClientCancelledRequestTests
{
    [Fact]
    public async Task A_request_the_client_dropped_is_marked_499_without_a_body_or_an_error_in_the_log()
    {
        var log = new RecordingLogger<ExceptionHandlingMiddleware>();
        var context = CreateContext(clientGone: true);
        var middleware = new ExceptionHandlingMiddleware(log, current => throw new OperationCanceledException(current.RequestAborted));

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.Equal(0, context.Response.Body.Length);
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
    }

    [Fact]
    public async Task A_database_call_cancelled_with_the_request_counts_the_same()
    {
        var log = new RecordingLogger<ExceptionHandlingMiddleware>();
        var context = CreateContext(clientGone: true);
        var middleware = new ExceptionHandlingMiddleware(log, _ => throw new TaskCanceledException("A task was canceled."));

        await middleware.InvokeAsync(context);

        Assert.Equal(499, context.Response.StatusCode);
        Assert.DoesNotContain(log.Entries, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact]
    public async Task A_cancellation_that_is_not_the_clients_stays_a_server_error()
    {
        var log = new RecordingLogger<ExceptionHandlingMiddleware>();
        var context = CreateContext(clientGone: false);
        var middleware = new ExceptionHandlingMiddleware(log, _ => throw new TaskCanceledException("The request to another service timed out."));

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var problem = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("internal_error", problem.RootElement.GetProperty("code").GetString());
        var entry = Assert.Single(log.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.IsType<TaskCanceledException>(entry.Exception);
    }

    [Fact]
    public async Task An_answer_already_on_its_way_keeps_its_status()
    {
        var context = CreateContext(clientGone: true);
        context.Features.Set<Microsoft.AspNetCore.Http.Features.IHttpResponseFeature>(new StartedResponse());
        var middleware = new ExceptionHandlingMiddleware(new RecordingLogger<ExceptionHandlingMiddleware>(), _ => throw new OperationCanceledException());

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Dropped_requests_never_reach_the_owners_error_alert()
    {
        var monitor = new ErrorSpikeMonitor(new ErrorSpikeOptions { ServerErrors = 2 });
        var spike = new ErrorSpikeMiddleware(
            current => new ExceptionHandlingMiddleware(NullLogger<ExceptionHandlingMiddleware>.Instance, _ => throw new OperationCanceledException()).InvokeAsync(current),
            monitor,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            NullLogger<ErrorSpikeMiddleware>.Instance);

        for (var i = 0; i < 50; i++)
        {
            await spike.InvokeAsync(CreateContext(clientGone: true));
        }

        // The counter is still at zero: the first real server error is number one, the second one raises the alert.
        Assert.Null(monitor.Record(StatusCodes.Status500InternalServerError));
        Assert.NotNull(monitor.Record(StatusCodes.Status500InternalServerError));
    }

    private static DefaultHttpContext CreateContext(bool clientGone)
    {
        var context = new DefaultHttpContext { RequestAborted = new CancellationToken(clientGone) };
        context.Request.Path = "/api/bloggers";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class StartedResponse : Microsoft.AspNetCore.Http.Features.HttpResponseFeature
    {
        public override bool HasStarted => true;
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, exception));
    }
}
