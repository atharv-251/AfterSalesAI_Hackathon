using AfterSalesAI.Application;
using Xunit;

namespace AfterSalesAI.UnitTests;

public sealed class AssistantServiceTests
{
    [Fact]
    public async Task HandleAsync_ReturnsKnowledgeResponseForRelevantResult()
    {
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever(), new UnavailableLlmAnswerGenerator());
        var request = new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "How do I raise a claim?");

        var response = await service.HandleAsync(request);

        Assert.Equal("KNOWLEDGE", response.Decision);
        Assert.Contains("couldn't generate an AI answer", response.Answer);
        Assert.DoesNotContain("Submit required evidence", response.Answer);
        Assert.Contains("search_sop", response.ToolsExecuted!);
    }

    [Fact]
    public async Task HandleAsync_UsesConfiguredLlmForGroundedKnowledgeAnswer()
    {
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever(), llm);
        var request = new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "How do I raise a claim?");

        var response = await service.HandleAsync(request);

        Assert.Equal("LLM grounded answer", response.Answer);
        Assert.NotNull(llm.Request);
        Assert.Equal(request.Message, llm.Request.UserMessage);
        Assert.Contains(llm.Request.Sources, source => source.Name == "Claims SOP");
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForDirectOrderStatus()
    {
        var order = new DemoOrder("PO-2026-1008", "Dealer", "Shipped", "Delayed", new DateOnly(2026, 9, 26), "PART-1", "Contact the carrier and update the customer.");
        var retriever = new FakeKnowledgeRetriever();
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations([order]), retriever, llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Is purchase order PO-2026-1008 delayed?"));

        Assert.Equal("API", response.Decision);
        Assert.Equal("LLM grounded answer", response.Answer);
        Assert.Equal(["API: Order and Delivery"], response.Sources);
        Assert.Equal(["get_order_status", "get_delivery_status"], response.ToolsExecuted);
        Assert.Equal(0, retriever.CallCount);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
        Assert.Contains(llm.Request.Sources, source => source.Name == "Validated Orders live record");
    }

    [Fact]
    public async Task HandleAsync_UsesGroundedOperationalResponseForDashboardAlerts()
    {
        var dashboard = new DemoDashboard(12, 4, 2, 3, 70, 1, ["Shipment SHP-102 is delayed."]);
        var llm = new ConfiguredLlmAnswerGenerator("Prioritized dashboard response");
        var service = new AssistantService(new FakeOperations(dashboard: dashboard), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Which dashboard alerts need attention?"));

        Assert.Equal("API", response.Decision);
        Assert.Equal("Prioritized dashboard response", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
        Assert.Contains(llm.Request.Sources, source => source.Name == "Validated Dashboard live record");
    }

    [Fact]
    public async Task HandleAsync_ExplainsDeliveryProcessWhenLlmIsUnavailable()
    {
        var order = new DemoOrder("PO-2026-1067", "Dealer", "Shipped", "Delayed", new DateOnly(2026, 4, 3), "P-10033", "Shipment SHP-5023 is Delayed.", "DHL", "DH3898400928", new DateOnly(2026, 4, 1));
        var knowledge = new FakeKnowledgeRetriever([new("Delivery Status Main List", "delivery/api-delivery-status-main.md", "Delivery status overview.", 0, 1)]);
        var service = new AssistantService(new FakeOperations([order]), knowledge, new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "What delivery information is available for PO-2026-1067?"));

        Assert.StartsWith("• Order PO-2026-1067: Shipped", response.Answer);
        Assert.Contains("• Tracking: DH3898400928", response.Answer);
        Assert.DoesNotContain("What this means:", response.Answer);
        Assert.Equal(["API: Order and Delivery"], response.Sources);
        Assert.Equal(0, knowledge.CallCount);
    }

    [Fact]
    public async Task HandleAsync_UsesLlmOperationalResponseModeForEntityLookup()
    {
        var records = new OperationalSearchRecord[]
        {
            new("Customer records", "Database: Customer Records", "Customer Lisbon Garage; city Lisbon; status Active."),
            new("Orders", "Database: Orders & Deliveries", "Order PO-100; status Confirmed."),
            new("Orders", "Database: Orders & Deliveries", "Order PO-101; status Shipped."),
            new("Claims", "Database: Claims", "Claim CL-100; status Open.")
        };
        var llm = new ConfiguredLlmAnswerGenerator("Smart customer overview");
        var service = new AssistantService(new FakeOperations(searchResults: records), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Give me all details for customer Lisbon Garage"));

        Assert.Equal("Smart customer overview", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
        Assert.Equal(AssistantIntent.EntityLookup, llm.Request.Intent);
        Assert.Equal(ResponseLength.Medium, llm.Request.ResponseLength);
        Assert.Equal(4, llm.Request.TotalRecordsFound);
        var validationSource = Assert.Single(llm.Request.Sources.Where(source => source.Name == "Validated operational retrieval summary"));
        Assert.Contains("Requested customer: Lisbon Garage; direct customer match validated: yes", validationSource.Content);
        Assert.Contains("Related orders retrieved: 2", validationSource.Content);
        Assert.DoesNotContain("Database: Parts & Inventory", response.Sources);
    }

    [Fact]
    public async Task HandleAsync_SummarizesLargeOperationalResultSetWhenLlmIsUnavailable()
    {
        var records = Enumerable.Range(1, 5).Select(number => new OperationalSearchRecord(
            "Orders", "Database: Orders & Deliveries", $"Order PO-{number}; status Confirmed.")).ToArray();
        var service = new AssistantService(new FakeOperations(searchResults: records), new FakeKnowledgeRetriever([]), new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Give me all order details for Lisbon Garage"));

        Assert.Contains("5 matching records", response.Answer);
        Assert.Contains("Order PO-1", response.Answer);
        Assert.DoesNotContain("Order PO-3", response.Answer);
        Assert.Contains("Ask for complete records", response.Answer);
    }

    [Fact]
    public async Task HandleAsync_RejectsUnrelatedShipmentAndClaimFromOrderScopedLlmAnswer()
    {
        var records = new OperationalSearchRecord[]
        {
            new("Orders", "Database: Orders & Deliveries", "Order PO-2026-1095; status Confirmed."),
            new("Orders", "Database: Orders & Deliveries", "Order PO-2026-1008; status Shipped."),
            new("Deliveries", "Database: Orders & Deliveries", "Shipment SHP-1095; order PO-2026-1095; status In Transit."),
            new("Claims", "Database: Claims", "Claim CLM-1095; order PO-2026-1095; status Open."),
            new("Claims", "Database: Claims", "Claim CLM-4001; order PO-2026-1087; status Open.")
        };
        var llm = new ConfiguredLlmAnswerGenerator("PO-2026-1095 has shipment SHP-1095 and claim CLM-4001 for PO-2026-1087.");
        var service = new AssistantService(new FakeOperations(searchResults: records), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Give me all details for PO-2026-1095 and PO-2026-1008"));

        Assert.DoesNotContain("CLM-4001", response.Answer);
        Assert.DoesNotContain("PO-2026-1087", response.Answer);
        Assert.Contains("PO-2026-1095", response.Answer);
        Assert.Contains("PO-2026-1008", response.Answer);
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForDirectOrderStatusWithProcessQuestion()
    {
        var order = new DemoOrder("PO-2026-1067", "Dealer", "Shipped", "Delayed", new DateOnly(2026, 4, 3), "P-10033", "Shipment SHP-5023 is Delayed.", "DHL", "DH3898400928", new DateOnly(2026, 4, 1));
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations([order]), new FakeKnowledgeRetriever(), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "What delivery information is available for PO-2026-1067, and what does the delivery status documentation describe?"));

        Assert.Equal("LLM grounded answer", response.Answer);
        Assert.Equal(["API: Order and Delivery"], response.Sources);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsLiveInventoryForMentionedPart()
    {
        var inventory = new DemoPartStock("P-10033", "Brake Pad", "WH-NA-DET", 2, 5);
        var service = new AssistantService(new FakeOperations(inventory: [inventory]), new FakeKnowledgeRetriever([]), new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Is part P-10033 available?"));

        Assert.Equal("API", response.Decision);
        Assert.Contains("• Available quantity: 2", response.Answer);
        Assert.Contains("• Stock status: Low stock", response.Answer);
        Assert.Equal(["API: Parts and Inventory"], response.Sources);
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForDirectInventoryStatus()
    {
        var inventory = new DemoPartStock("P-10033", "Brake Pad", "WH-NA-DET", 2, 5);
        var llm = new ConfiguredLlmAnswerGenerator("P-10033 has low stock at WH-NA-DET with 2 units available.");
        var service = new AssistantService(new FakeOperations(inventory: [inventory]), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Is part P-10033 available?"));

        Assert.Equal("P-10033 has low stock at WH-NA-DET with 2 units available.", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsDeterministicAvailabilityForExplicitPartNumber()
    {
        var records = new OperationalSearchRecord[]
        {
            new("Parts", "Database: Parts & Inventory", "Part P-10013; Body Module 508; category Body; status Active; lead time 14 days."),
            new("Inventory", "Database: Parts & Inventory", "Inventory INV-0014; part P-10013; warehouse WH-IN-PUN; bin B1-6; on hand 60; reserved 2; available 58; reorder point 15; last count 28 Aug 2026."),
            new("Inventory", "Database: Parts & Inventory", "Inventory INV-0015; part P-10013; warehouse WH-EU-FRA; bin A10-8; on hand 40; reserved 4; available 36; reorder point 10; last count 12 Jun 2026.")
        };
        var llm = new ConfiguredLlmAnswerGenerator("P-10013 is unavailable.");
        var service = new AssistantService(new FakeOperations(searchResults: records), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Is part P-10013 available?"));

        Assert.Equal("OPERATIONAL_DATA", response.Decision);
        Assert.Contains("Available — 94 unit(s) across 2 warehouse location(s).", response.Answer);
        Assert.Contains("WH-IN-PUN", response.Answer);
        Assert.Contains("WH-EU-FRA", response.Answer);
        Assert.DoesNotContain("unavailable", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(llm.Request);
        Assert.Contains(llm.Request.Sources, source => source.Name == "Validated part availability");
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForValidatedPartAvailability()
    {
        var records = new OperationalSearchRecord[]
        {
            new("Parts", "Database: Parts & Inventory", "Part P-10013; Body Module 508; status Active."),
            new("Inventory", "Database: Parts & Inventory", "Inventory INV-0014; part P-10013; warehouse WH-IN-PUN; available 58; reorder point 15.")
        };
        var llm = new ConfiguredLlmAnswerGenerator("P-10013 is available at WH-IN-PUN with 58 units.");
        var service = new AssistantService(new FakeOperations(searchResults: records), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Is part P-10013 available?"));

        Assert.Equal("P-10013 is available at WH-IN-PUN with 58 units.", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
        Assert.Contains(llm.Request.Sources, source => source.Name == "Validated part availability");
    }

    [Fact]
    public async Task HandleAsync_ReturnsLiveClaimForMentionedClaimId()
    {
        var claim = new DemoClaim("CLM-4022", "PO-2026-1041", "Open", "Wrong Part", new DateOnly(2026, 3, 10));
        var service = new AssistantService(new FakeOperations(claims: [claim]), new FakeKnowledgeRetriever([]), new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "What is the status of claim CLM-4022?"));

        Assert.Equal("API", response.Decision);
        Assert.Contains("• Claim CLM-4022: Open", response.Answer);
        Assert.Contains("• Reason: Wrong Part", response.Answer);
        Assert.Equal(["API: Claims"], response.Sources);
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForDirectClaimStatus()
    {
        var claim = new DemoClaim("CLM-4022", "PO-2026-1041", "Open", "Wrong Part", new DateOnly(2026, 3, 10));
        var llm = new ConfiguredLlmAnswerGenerator("Claim CLM-4022 is open and needs review.");
        var service = new AssistantService(new FakeOperations(claims: [claim]), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "What is the status of claim CLM-4022?"));

        Assert.Equal("Claim CLM-4022 is open and needs review.", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
    }

    [Fact]
    public async Task HandleAsync_ReturnsCustomerAndRelatedOperationalRecordsBeforeKnowledge()
    {
        var records = new OperationalSearchRecord[]
        {
            new("Customer records", "Database: Customer Records", "Customer Lisbon Garage; city Lisbon; contact dealer@example.test."),
            new("Orders", "Database: Orders & Deliveries", "Order PO-100; part P-100; status Confirmed."),
            new("Claims", "Database: Claims", "Claim CL-100; order PO-100; status Open.")
        };
        var knowledge = new FakeKnowledgeRetriever();
        var service = new AssistantService(new FakeOperations(searchResults: records), knowledge, new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Give me all the details for customer name Lisbon Garage"));

        Assert.Equal("OPERATIONAL_DATA", response.Decision);
        Assert.Contains("Customer Lisbon Garage", response.Answer);
        Assert.Contains("Order PO-100", response.Answer);
        Assert.Contains("Claim CL-100", response.Answer);
        Assert.Contains("Database: Customer Records", response.Sources);
        Assert.Equal(0, knowledge.CallCount);
    }

    [Fact]
    public async Task HandleAsync_ReturnsLiveDashboardForAlertRequest()
    {
        var dashboard = new DemoDashboard(3, 2, 1, 4, 50, 2, ["Part P-10033 is at or below its reorder level."]);
        var service = new AssistantService(new FakeOperations(dashboard: dashboard), new FakeKnowledgeRetriever([]), new UnavailableLlmAnswerGenerator());

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Show the dealer dashboard alerts."));

        Assert.Equal("API", response.Decision);
        Assert.Contains("• Open orders: 3", response.Answer);
        Assert.Contains("• Alert: Part P-10033", response.Answer);
        Assert.Equal(["API: Dealer Dashboard"], response.Sources);
    }

    [Fact]
    public async Task HandleAsync_UsesLlmForDashboardAlerts()
    {
        var dashboard = new DemoDashboard(3, 2, 1, 4, 50, 2, ["Part P-10033 is at or below its reorder level."]);
        var llm = new ConfiguredLlmAnswerGenerator("The dashboard has two low-stock parts requiring attention.");
        var service = new AssistantService(new FakeOperations(dashboard: dashboard), new FakeKnowledgeRetriever([]), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Show the dealer dashboard alerts."));

        Assert.Equal("The dashboard has two low-stock parts requiring attention.", response.Answer);
        Assert.Equal(LlmAnswerMode.OperationalResponse, llm.Request!.Mode);
    }

    [Theory]
    [InlineData("Explain Business Process Document in dealer-friendly terms.")]
    [InlineData("Summarize Business Process Document.")]
    [InlineData("Give me a summary of Business Process Document")]
    public async Task HandleAsync_SummarizesOnlyNamedDocumentWithCompleteScopedContent(string message)
    {
        var document = new KnowledgeLibraryDocument(Guid.NewGuid(), "Business Process Document", "BUSINESS_PROCESS",
            "Business_Process_Document.pdf", "Order to delivery. " + new string('x', 13_000) + " Claims and returns: provide evidence and track resolution.", "Title page");
        var library = new FakeKnowledgeLibrary([document,
            new(Guid.NewGuid(), "User Manual", "USER_MANUAL", "User_Manual.pdf", "Unrelated manual text", "Manual")]);
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever(), llm, library);
        var request = new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), message);

        var response = await service.HandleAsync(request);

        Assert.Equal("LLM grounded answer", response.Answer);
        Assert.Equal("KNOWLEDGE", response.Decision);
        Assert.Equal(["Document: Business Process Document"], response.Sources);
        var source = Assert.Single(llm.Request!.Sources);
        Assert.Equal(LlmAnswerMode.DocumentSummary, llm.Request.Mode);
        Assert.Equal(document.Content, source.Content);
        Assert.Equal(request.TenantId, library.TenantId);
        Assert.Equal(request.ApplicationId, library.ApplicationId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HandleAsync_DoesNotPasteDocumentExcerptWhenGenerationReturnsNoAnswer(string? answer)
    {
        var document = new KnowledgeLibraryDocument(Guid.NewGuid(), "Business Process Document", "BUSINESS_PROCESS",
            "Business_Process_Document.pdf", "Private source title page and metadata", "Title page");
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever(), new ConfiguredLlmAnswerGenerator(answer), new FakeKnowledgeLibrary([document]));

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Explain Business Process Document in dealer-friendly terms."));

        Assert.Contains("couldn't generate an AI answer", response.Answer);
        Assert.DoesNotContain(document.Content, response.Answer);
        Assert.DoesNotContain("Relevant guidance from", response.Answer);
        Assert.Equal(["Document: Business Process Document"], response.Sources);
    }

    [Fact]
    public async Task HandleAsync_DoesNotUseNamedDocumentOutsideTheScopedLibrary()
    {
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever([]), llm, new FakeKnowledgeLibrary([]));

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "Explain Business Process Document."));

        Assert.Equal("NO_MATCH", response.Decision);
        Assert.Empty(response.Sources);
        Assert.Null(llm.Request);
    }

    private sealed class FakeKnowledgeLibrary(IReadOnlyCollection<KnowledgeLibraryDocument> documents) : IKnowledgeLibrary
    {
        public Guid TenantId { get; private set; }
        public Guid ApplicationId { get; private set; }

        public Task<IReadOnlyCollection<KnowledgeLibraryDocument>> GetDocumentsAsync(Guid tenantId, Guid applicationId, CancellationToken cancellationToken = default)
        {
            TenantId = tenantId;
            ApplicationId = applicationId;
            return Task.FromResult(documents);
        }
    }

    private sealed class FakeOperations(
        IReadOnlyCollection<DemoOrder>? orders = null,
        IReadOnlyCollection<DemoClaim>? claims = null,
        IReadOnlyCollection<DemoPartStock>? inventory = null,
        DemoDashboard? dashboard = null,
        IReadOnlyCollection<OperationalSearchRecord>? searchResults = null) : IAfterSalesOperations
    {
        public IReadOnlyCollection<DemoOrder> GetOrders() => orders ?? Array.Empty<DemoOrder>();
        public IReadOnlyCollection<DemoClaim> GetClaims() => claims ?? Array.Empty<DemoClaim>();
        public IReadOnlyCollection<DemoPartStock> GetInventory() => inventory ?? Array.Empty<DemoPartStock>();
        public DemoDashboard GetDashboard() => dashboard ?? new(0, 0, 0, 0, 0, 0, Array.Empty<string>());
        public OperationalSearchResult Search(string query) => new(searchResults ?? Array.Empty<OperationalSearchRecord>());
    }

    [Fact]
    public async Task HandleAsync_UsesTheoreticalGuidanceModeWhenNoLiveRecordOrNamedDocumentMatches()
    {
        var llm = new ConfiguredLlmAnswerGenerator();
        var service = new AssistantService(new FakeOperations(), new FakeKnowledgeRetriever(), llm);

        var response = await service.HandleAsync(new AssistantRequest(Guid.NewGuid(), Guid.NewGuid(), "How do I manage a delayed delivery?"));

        Assert.Equal("KNOWLEDGE", response.Decision);
        Assert.Equal(LlmAnswerMode.TheoreticalGuidance, llm.Request!.Mode);
    }

    private sealed class FakeKnowledgeRetriever(IReadOnlyCollection<KnowledgeSearchResult>? results = null) : IKnowledgeRetriever
    {
        public int CallCount { get; private set; }
        public Task<IReadOnlyCollection<KnowledgeSearchResult>> SearchAsync(Guid tenantId, Guid applicationId, string query, int maximumResults = 5, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(results ?? (IReadOnlyCollection<KnowledgeSearchResult>)[new("Claims SOP", "claims.md", "Submit required evidence for claims.", 0, 1)]);
        }
    }

    private sealed class UnavailableLlmAnswerGenerator : ILlmAnswerGenerator
    {
        public bool IsAvailable => false;

        public Task<string?> GenerateAsync(LlmAnswerGenerationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class ConfiguredLlmAnswerGenerator(string? answer = "LLM grounded answer") : ILlmAnswerGenerator
    {
        public bool IsAvailable => true;
        public LlmAnswerGenerationRequest? Request { get; private set; }

        public Task<string?> GenerateAsync(LlmAnswerGenerationRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(answer);
        }
    }
}
