using System.Text.Json;

namespace Plana.Core.Plugins;

public sealed class PluginProtocolSession(Stream stream) : IAsyncDisposable
{
    private readonly StreamReader reader = new(stream, leaveOpen: true);
    private readonly StreamWriter writer = new(stream, leaveOpen: true) { AutoFlush = true };
    private readonly SemaphoreSlim requestGate = new(1, 1);

    public async Task<PluginContributionsPayload> InitializeAsync(
        PluginManifest manifest,
        string culture,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var hello = await ReadAsync(deadline.Token);
        if (hello.Type != "hello") throw new PluginProtocolException($"Expected hello, received '{hello.Type}'.");
        var helloPayload = hello.Payload.Deserialize<PluginHelloPayload>(PluginProtocol.JsonOptions)
            ?? throw new PluginProtocolException("The hello payload is missing.");
        if (!string.Equals(helloPayload.PluginId, manifest.Id, StringComparison.OrdinalIgnoreCase))
        {
            throw new PluginProtocolException($"Plugin ID mismatch: expected '{manifest.Id}', received '{helloPayload.PluginId}'.");
        }
        if (!string.Equals(helloPayload.HostApi, manifest.HostApi, StringComparison.Ordinal))
        {
            throw new PluginProtocolException($"Host API mismatch: expected '{manifest.HostApi}', received '{helloPayload.HostApi}'.");
        }

        var requestId = Guid.NewGuid().ToString("N");
        await WriteAsync(PluginProtocol.Envelope(
            requestId,
            "initialize",
            new PluginInitializePayload(culture, manifest.Capabilities.Order().ToArray())), deadline.Token);
        var ready = await ReadAsync(deadline.Token);
        if (ready.Type != "ready" || ready.RequestId != requestId)
        {
            throw new PluginProtocolException("The Plugin did not acknowledge initialize with a matching ready response.");
        }
        var contributionEnvelope = await ReadAsync(deadline.Token);
        if (contributionEnvelope.Type != "contributeActions")
        {
            throw new PluginProtocolException($"Expected contributeActions, received '{contributionEnvelope.Type}'.");
        }
        var contributionPayload = contributionEnvelope.Payload.Deserialize<PluginContributionsPayload>(PluginProtocol.JsonOptions)
            ?? throw new PluginProtocolException("The contributeActions payload is missing.");
        var contributions = PluginContributionPolicy.Validate(manifest, contributionPayload);
        await WriteAsync(PluginProtocol.Envelope(
            contributionEnvelope.RequestId,
            "actionsAccepted",
            new { actions = contributions.Actions.Length, tools = contributions.Tools!.Length, contextCommands = contributions.ContextCommands!.Length, contentProviders = contributions.ContentProviders!.Length }), deadline.Token);
        return contributions;
    }

    public async Task<PluginInvokeResultPayload> InvokeAsync(
        string actionId,
        IReadOnlySet<string> approvedActionCapabilities,
        Func<PluginHostRequestPayload, CancellationToken, Task<PluginHostResponsePayload>> handleHostRequest,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        await requestGate.WaitAsync(cancellationToken);
        try
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(timeout);
            var requestId = Guid.NewGuid().ToString("N");
            await WriteAsync(PluginProtocol.Envelope(requestId, "invoke", new PluginInvokePayload(actionId)), deadline.Token);
            while (true)
            {
                var response = await ReadAsync(deadline.Token);
                if (response.Type == "result")
                {
                    if (response.RequestId != requestId)
                    {
                        throw new PluginProtocolException("The Plugin result response has a mismatched request ID.");
                    }
                    return response.Payload.Deserialize<PluginInvokeResultPayload>(PluginProtocol.JsonOptions)
                        ?? throw new PluginProtocolException("The result payload is missing.");
                }
                if (response.Type != "hostRequest")
                {
                    throw new PluginProtocolException($"Expected result or hostRequest, received '{response.Type}'.");
                }
                var hostRequest = response.Payload.Deserialize<PluginHostRequestPayload>(PluginProtocol.JsonOptions)
                    ?? throw new PluginProtocolException("The hostRequest payload is missing.");
                PluginHostResponsePayload hostResponse;
                if (!PluginHostRequestPolicy.IsAllowed(hostRequest.Kind, approvedActionCapabilities))
                {
                    hostResponse = new PluginHostResponsePayload(false, "The Plugin Action is not approved for this host capability.");
                }
                else
                {
                    hostResponse = await handleHostRequest(hostRequest, deadline.Token);
                }
                await WriteAsync(PluginProtocol.Envelope(
                    response.RequestId,
                    "hostResponse",
                    hostResponse), deadline.Token);
            }
        }
        finally
        {
            requestGate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await writer.DisposeAsync();
        reader.Dispose();
        requestGate.Dispose();
    }

    private async Task<PluginEnvelope> ReadAsync(CancellationToken cancellationToken)
    {
        var line = await reader.ReadLineAsync(cancellationToken)
            ?? throw new PluginProtocolException("The Plugin closed the protocol stream.");
        if (line.Length > PluginProtocol.MaximumMessageCharacters)
        {
            throw new PluginProtocolException("The Plugin protocol message is too large.");
        }
        var envelope = JsonSerializer.Deserialize<PluginEnvelope>(line, PluginProtocol.JsonOptions)
            ?? throw new PluginProtocolException("The Plugin protocol message is empty.");
        if (envelope.ProtocolVersion != PluginProtocol.Version
            || string.IsNullOrWhiteSpace(envelope.RequestId)
            || string.IsNullOrWhiteSpace(envelope.Type))
        {
            throw new PluginProtocolException("The Plugin protocol envelope has an invalid version, requestId, or type.");
        }
        return envelope;
    }

    private Task WriteAsync(PluginEnvelope envelope, CancellationToken cancellationToken) =>
        writer.WriteLineAsync(JsonSerializer.Serialize(envelope, PluginProtocol.JsonOptions).AsMemory(), cancellationToken);
}
