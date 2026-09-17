using System.Net;
using System.Text;
using System.Text.Json;
using AfterSalesAI.Application;
using AfterSalesAI.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace AfterSalesAI.UnitTests;

public sealed class LlmAasAnswerGeneratorTests
{
    private const string CompletedResponse = """{"choices":[{"finish_reason":"stop","message":{"content":"Track the order through delivery, then provide evidence and follow up on any claim or return."}}]}""";

    [Fact]
    public void Options_AreConfiguredWhenAllLlmAasCredentialsAreProvided()
    {
        var options = new LlmAasOptions
        {
            ClientId = "test-client-id",
            ClientSecret = "test-client-secret",
            VirtualKey = "test-virtual-key"
        };

        Assert.True(options.IsConfigured);
    }

    [Fact]
    public async Task Generate_UsesReasoningBudgetAndShortSummaryInstructionsWithCompleteDocument()
    {
        using var harness = new Harness(CompletedResponse);
        var content = "Order to delivery. " + new string('x', 13_000) + " Claims and returns: collect evidence and follow up.";

        var result = await harness.Generator.GenerateAsync(new("Explain Business Process Document in dealer-friendly terms.", [new("Business Process Document", content)], LlmAnswerMode.DocumentSummary));

        Assert.StartsWith("Track the order", result);
        using var payload = JsonDocument.Parse(harness.Chat.LastRequestBody!);
        Assert.Equal(4096, payload.RootElement.GetProperty("max_tokens").GetInt32());
        Assert.Equal("smart-router", payload.RootElement.GetProperty("model").GetString());
        var messages = payload.RootElement.GetProperty("messages");
        var instruction = messages[0].GetProperty("content").GetString();
        Assert.Contains("80-120 words", instruction);
        Assert.Contains("exactly three short practical bullets", instruction);
        Assert.Contains("rather than copying excerpts", instruction);
        Assert.Contains("no live operational records are supplied", instruction);
        Assert.DoesNotContain("Current status:", instruction);
        Assert.Contains(content, messages[1].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Generate_UsesDirectTheoreticalGuidanceWithoutStatusFormatting()
    {
        using var harness = new Harness(CompletedResponse);

        await harness.Generator.GenerateAsync(new("How do I manage a delayed delivery?", [new("Standard Operating Procedure", "Check the tracking status and communicate the update.")]));

        using var payload = JsonDocument.Parse(harness.Chat.LastRequestBody!);
        var instruction = payload.RootElement.GetProperty("messages")[0].GetProperty("content").GetString();
        Assert.Contains("Answer the theoretical question directly", instruction);
        Assert.DoesNotContain("Current status:", instruction);
        Assert.DoesNotContain("exactly three short bullets", instruction);
    }

    [Fact]
    public async Task Generate_RejectsReasoningOnlyLengthLimitedCompletionAndLogsOnlyMetadata()
    {
        using var harness = new Harness("""{"choices":[{"finish_reason":"length","message":{"content":null,"reasoning_content":"PRIVATE MODEL REASONING"}}],"usage":{"completion_tokens":500,"completion_tokens_details":{"reasoning_tokens":500}}}""");

        var result = await harness.Generator.GenerateAsync(Request());

        Assert.Null(result);
        var log = Assert.Single(harness.Logger.Messages, message => message.Contains("returned no usable complete answer", StringComparison.Ordinal));
        Assert.Contains("length", log);
        Assert.Contains("reasoning tokens: 500", log);
        Assert.DoesNotContain("PRIVATE MODEL REASONING", log);
    }

    [Theory]
    [InlineData("{\"choices\":[{\"finish_reason\":\"length\",\"message\":{\"content\":\"Incomplete answer\"}}]}")]
    [InlineData("{\"choices\":[{\"finish_reason\":\"content_filter\",\"message\":{\"content\":\"Filtered content\"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":null}}]}")]
    [InlineData("{\"choices\":[{\"message\":{}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"  \"}}]}")]
    [InlineData("{\"choices\":[{\"message\":{\"content\":\"Text\",\"refusal\":\"Refused\"}}]}")]
    [InlineData("{\"choices\":[null,42]}")]
    [InlineData("{\"choices\":[]}")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("not JSON")]
    public async Task Generate_ReturnsNullForIncompleteOrMalformedAnswers(string response)
    {
        using var harness = new Harness(response);

        Assert.Null(await harness.Generator.GenerateAsync(Request()));
    }

    [Fact]
    public async Task Generate_ReadsTextPartsWithoutExposingReasoningParts()
    {
        using var harness = new Harness("""{"choices":[{"finish_reason":"stop","message":{"content":[{"type":"reasoning","text":"PRIVATE MODEL REASONING"},{"type":"text","text":"Check your order."},{"type":"text","text":"Follow up on delivery."}]}}]}""");

        var answer = await harness.Generator.GenerateAsync(Request());

        Assert.Equal("Check your order.\nFollow up on delivery.", answer);
    }

    [Fact]
    public async Task Generate_DoesNotSendSilentlyTruncatedContext()
    {
        using var harness = new Harness(CompletedResponse);

        var answer = await harness.Generator.GenerateAsync(new("Summarize Business Process Document.", [new("Business Process Document", new string('x', 33_000))]));

        Assert.Null(answer);
        Assert.Null(harness.Chat.LastRequestBody);
        Assert.Null(harness.Token.LastRequestBody);
    }

    [Fact]
    public async Task Generate_HandlesProviderTimeoutWithoutMaskingCallerCancellation()
    {
        using var harness = new Harness(CompletedResponse);
        harness.Chat.Failure = new TaskCanceledException("Provider timeout");

        Assert.Null(await harness.Generator.GenerateAsync(Request()));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => harness.Generator.GenerateAsync(Request(), cancellation.Token));
    }

    [Fact]
    public async Task Generate_LogsSafeProfileMetricsForProviderTimeout()
    {
        using var harness = new Harness(CompletedResponse);
        harness.Chat.Failure = new TaskCanceledException("Provider timeout");
        var request = new LlmAnswerGenerationRequest("Explain the process.", [new("FAQ", "Sensitive approved document context.")]);

        Assert.Null(await harness.Generator.GenerateAsync(request));

        var profile = Assert.Single(harness.Logger.Messages, message => message.Contains("LLMaaS generation profile", StringComparison.Ordinal));
        Assert.Contains("outcome ProviderTimeout", profile);
        Assert.Contains("sources 1", profile);
        Assert.Contains("context characters", profile);
        Assert.Contains("estimated input tokens", profile);
        Assert.Contains("token acquisition milliseconds", profile);
        Assert.Contains("provider request milliseconds", profile);
        Assert.DoesNotContain(request.Sources.Single().Content, profile, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generate_DoesNotLogRawProviderErrorBody()
    {
        using var harness = new Harness("PRIVATE PROVIDER ERROR BODY", HttpStatusCode.BadRequest);

        Assert.Null(await harness.Generator.GenerateAsync(Request()));
        Assert.DoesNotContain(harness.Logger.Messages, message => message.Contains("PRIVATE PROVIDER ERROR BODY", StringComparison.Ordinal));
    }

    private static LlmAnswerGenerationRequest Request() => new("Explain Business Process Document.", [new("Business Process Document", "Track order delivery and claim resolution.")]);

    private sealed class Harness : IDisposable
    {
        private readonly HttpClient _chatClient;
        private readonly HttpClient _tokenClient;
        public RecordingHandler Chat { get; }
        public RecordingHandler Token { get; }
        public RecordingLogger Logger { get; } = new();
        public LlmAasAnswerGenerator Generator { get; }

        public Harness(string response, HttpStatusCode status = HttpStatusCode.OK)
        {
            Chat = new RecordingHandler(response, status);
            Token = new RecordingHandler("""{"access_token":"test-access-token","expires_in":3600}""", HttpStatusCode.OK);
            _chatClient = new HttpClient(Chat) { BaseAddress = new Uri("https://example.test/") };
            _tokenClient = new HttpClient(Token);
            Generator = new LlmAasAnswerGenerator(_chatClient, new TokenClientFactory(_tokenClient), Options.Create(new LlmAasOptions
            {
                BaseUrl = "https://example.test/", TokenEndpoint = "https://example.test/token",
                ClientId = "test-client", ClientSecret = "test-secret", VirtualKey = "test-key"
            }), Logger);
        }

        public void Dispose()
        {
            _chatClient.Dispose();
            _tokenClient.Dispose();
        }
    }

    private sealed class TokenClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class RecordingHandler(string response, HttpStatusCode status) : HttpMessageHandler
    {
        public string? LastRequestBody { get; private set; }
        public Exception? Failure { get; set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (Failure is not null) throw Failure;
            return new HttpResponseMessage(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };
        }
    }

    private sealed class RecordingLogger : ILogger<LlmAasAnswerGenerator>
    {
        public List<string> Messages { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
    }
}
