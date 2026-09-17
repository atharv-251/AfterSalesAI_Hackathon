using System.Text.RegularExpressions;

namespace AfterSalesAI.Application;

public sealed class AssistantService(
    IAfterSalesOperations operations,
    IKnowledgeRetriever knowledgeRetriever,
    ILlmAnswerGenerator llmAnswerGenerator,
    IKnowledgeLibrary? knowledgeLibrary = null)
{
    private const string KnowledgeGenerationUnavailable = "I couldn't generate an AI answer from the available documents right now. Please try again shortly. You can read the source text in Knowledge & SOP in the meantime.";

    public async Task<AssistantResponse> HandleAsync(AssistantRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.", nameof(request.Message));

        var intent = ClassifyIntent(request.Message);
        var operationalQuestion = IsOperationalQuestion(request.Message);
        if (operationalQuestion)
        {
            var operationalResults = operations.Search(request.Message);
            if (operationalResults.Records.Count > 0)
            {
                var responseLength = SelectResponseLength(request.Message, intent);
                var selectedRecords = SelectRelevantRecords(operationalResults.Records, request.Message, responseLength);
                var retrievalSummary = CreateValidatedRetrievalSummary(request.Message, operationalResults.Records, selectedRecords);
                var operationalSources = selectedRecords.Select(record => record.Source).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                var fallbackAnswer = IsPartAvailabilityQuestion(request.Message)
                    ? CreatePartAvailabilityAnswer(request.Message, operationalResults.Records)
                    : CreateOperationalFallback(operationalResults.Records, selectedRecords, retrievalSummary, intent, responseLength);
                var answer = await GenerateOperationalAnswerAsync(request.Message, selectedRecords, retrievalSummary, operationalResults.Records.Count,
                    intent, responseLength, fallbackAnswer, cancellationToken);
                var tools = new List<string> { "search_operational_records" };

                if (RequiresProcessGuidance(request.Message))
                {
                    var guidance = await GetKnowledgeAnswerAsync(request, cancellationToken);
                    if (guidance is not null)
                    {
                        answer = $"{answer}\n\nRelevant process guidance:\n{guidance.Answer}";
                        operationalSources = operationalSources.Concat(guidance.Sources).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        tools.Add("search_sop");
                    }
                }

                return new AssistantResponse(answer, "OPERATIONAL_DATA", operationalSources, tools);
            }
        }

        var documentSummary = await TrySummarizeDocumentAsync(request, cancellationToken);
        if (documentSummary is not null) return documentSummary;

        var order = operations.GetOrders().FirstOrDefault(item => request.Message.Contains(item.OrderId, StringComparison.OrdinalIgnoreCase));
        if (order is not null)
        {
            var fallbackAnswer = CreateOrderStatusAnswer(order);
            var answer = await GenerateDirectOperationalAnswerAsync(request.Message,
                new OperationalSearchRecord("Orders", "API: Order and Delivery", fallbackAnswer), fallbackAnswer, intent, cancellationToken);
            return new AssistantResponse(answer, "API", ["API: Order and Delivery"], ["get_order_status", "get_delivery_status"]);
        }
        var part = operations.GetInventory().FirstOrDefault(item => request.Message.Contains(item.PartNumber, StringComparison.OrdinalIgnoreCase));
        if (part is not null)
        {
            var fallbackAnswer = CreateInventoryStatusAnswer(part);
            var answer = await GenerateDirectOperationalAnswerAsync(request.Message,
                new OperationalSearchRecord("Inventory", "API: Parts and Inventory", fallbackAnswer), fallbackAnswer, intent, cancellationToken);
            return new AssistantResponse(answer, "API", ["API: Parts and Inventory"], ["get_part_inventory"]);
        }
        var claim = operations.GetClaims().FirstOrDefault(item => request.Message.Contains(item.ClaimId, StringComparison.OrdinalIgnoreCase));
        if (claim is not null)
        {
            var fallbackAnswer = CreateClaimStatusAnswer(claim);
            var answer = await GenerateDirectOperationalAnswerAsync(request.Message,
                new OperationalSearchRecord("Claims", "API: Claims", fallbackAnswer), fallbackAnswer, intent, cancellationToken);
            return new AssistantResponse(answer, "API", ["API: Claims"], ["get_claim_status"]);
        }
        if (request.Message.Contains("dashboard", StringComparison.OrdinalIgnoreCase)
            || request.Message.Contains("alert", StringComparison.OrdinalIgnoreCase))
        {
            var dashboard = operations.GetDashboard();
            var fallbackAnswer = CreateDashboardStatusAnswer(dashboard);
            var answer = await GenerateDirectOperationalAnswerAsync(request.Message,
                new OperationalSearchRecord("Dashboard", "API: Dealer Dashboard", fallbackAnswer), fallbackAnswer, intent, cancellationToken);
            return new AssistantResponse(answer, "API", ["API: Dealer Dashboard"], ["get_dashboard_alerts"]);
        }
        var knowledge = await knowledgeRetriever.SearchAsync(request.TenantId, request.ApplicationId ?? Guid.Empty, request.Message, cancellationToken: cancellationToken);
        var sources = knowledge.Select(item => $"Document: {item.DocumentName}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        if (knowledge.Count > 0)
        {
            var answer = await GenerateAnswerAsync(
                request.Message,
                KnowledgeGenerationUnavailable,
                knowledge.Select(item => new LlmGroundingSource(item.DocumentName, item.Content)).ToArray(),
                cancellationToken);
            return new AssistantResponse(answer, "KNOWLEDGE", sources, ["search_sop"]);
        }
        return new AssistantResponse(operationalQuestion
            ? "No matching operational records or tenant-scoped knowledge documents were found."
            : "No tenant-scoped knowledge document or approved live-data record matched the request.", "NO_MATCH", Array.Empty<string>(), Array.Empty<string>());
    }

    private async Task<AssistantResponse?> GetKnowledgeAnswerAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        var knowledge = await knowledgeRetriever.SearchAsync(request.TenantId, request.ApplicationId ?? Guid.Empty, request.Message, cancellationToken: cancellationToken);
        if (knowledge.Count == 0) return null;

        var sources = knowledge.Select(item => $"Document: {item.DocumentName}")
            .Distinct(StringComparer.OrdinalIgnoreCase).Take(2).ToArray();
        var answer = await GenerateAnswerAsync(request.Message, KnowledgeGenerationUnavailable,
            knowledge.Select(item => new LlmGroundingSource(item.DocumentName, item.Content)).ToArray(), cancellationToken);
        return new AssistantResponse(answer, "KNOWLEDGE", sources, ["search_sop"]);
    }

    private static bool IsOperationalQuestion(string message) =>
        new[] { "customer", "dealer", "order", "purchase order", "delivery", "shipment", "tracking", "claim", "part", "inventory", "stock", "bom", "dashboard", "alert" }
            .Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase))
        || message.Any(char.IsDigit);

    private static bool RequiresProcessGuidance(string message) =>
        new[] { "how do", "how to", "process", "procedure", "steps", "sop", "documentation", "manual" }
            .Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static bool IsPartAvailabilityQuestion(string message) =>
        OperationalSearchMatcher.ExtractPartNumbers(message).Count > 0
        && new[] { "available", "availability", "inventory", "stock" }
            .Any(term => message.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static string CreatePartAvailabilityAnswer(string message, IReadOnlyCollection<OperationalSearchRecord> records)
    {
        var requestedParts = OperationalSearchMatcher.ExtractPartNumbers(message);
        var partRecords = records.Where(record => record.Category is "Parts" or "Inventory"
            && requestedParts.Any(partNumber => record.Content.Contains(partNumber, StringComparison.OrdinalIgnoreCase))).ToArray();
        var inventoryRecords = partRecords.Where(record => record.Category == "Inventory").ToArray();
        var totalAvailable = inventoryRecords.Sum(record => ReadIntegerField(record.Content, "available "));
        var availability = totalAvailable > 0 ? $"Available — {totalAvailable} unit(s) across {inventoryRecords.Length} warehouse location(s)." : "Out of stock in the retrieved warehouse records.";
        return $"Part availability:\n• {availability}\n" + string.Join('\n', partRecords.Select(record => $"• {record.Content}"));
    }

    private async Task<string> GenerateOperationalAnswerAsync(
        string message,
        IReadOnlyCollection<OperationalSearchRecord> records,
        string retrievalSummary,
        int totalRecordsFound,
        AssistantIntent intent,
        ResponseLength responseLength,
        string fallbackAnswer,
        CancellationToken cancellationToken)
    {
        var sources = new[] { new LlmGroundingSource("Validated operational retrieval summary", retrievalSummary) }.Concat(records.Select(record => new LlmGroundingSource(
            $"{record.Category} ({record.Source})", record.Content)));
        if (IsPartAvailabilityQuestion(message)) sources = sources.Append(new LlmGroundingSource("Validated part availability", fallbackAnswer));
        var answer = await GenerateAnswerAsync(message, fallbackAnswer, sources.ToArray(), cancellationToken,
            LlmAnswerMode.OperationalResponse, intent, responseLength, totalRecordsFound);
        return IsOperationalAnswerSafe(message, records, answer) ? answer : fallbackAnswer;
    }

    private async Task<string> GenerateDirectOperationalAnswerAsync(
        string message,
        OperationalSearchRecord record,
        string fallbackAnswer,
        AssistantIntent intent,
        CancellationToken cancellationToken)
    {
        var answer = await GenerateAnswerAsync(message, fallbackAnswer,
            [new LlmGroundingSource($"Validated {record.Category} live record", record.Content)], cancellationToken,
            LlmAnswerMode.OperationalResponse, intent, ResponseLength.Concise, 1);
        return IsOperationalAnswerSafe(message, [record], answer) ? answer : fallbackAnswer;
    }

    private static AssistantIntent ClassifyIntent(string message)
    {
        if (RequiresProcessGuidance(message) && !IsOperationalQuestion(message)) return AssistantIntent.ProcessGuidance;
        if (message.Contains("compare", StringComparison.OrdinalIgnoreCase) || message.Contains("difference", StringComparison.OrdinalIgnoreCase)) return AssistantIntent.Comparison;
        if (message.Contains("status", StringComparison.OrdinalIgnoreCase) || message.Contains("delayed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("exception", StringComparison.OrdinalIgnoreCase) || message.Contains("track", StringComparison.OrdinalIgnoreCase)) return AssistantIntent.StatusOrException;
        if (message.Contains("customer", StringComparison.OrdinalIgnoreCase) || message.Contains("dealer", StringComparison.OrdinalIgnoreCase)
            || message.Contains("details", StringComparison.OrdinalIgnoreCase) || message.Contains("all", StringComparison.OrdinalIgnoreCase)) return AssistantIntent.EntityLookup;
        return AssistantIntent.Factual;
    }

    private static ResponseLength SelectResponseLength(string message, AssistantIntent intent)
    {
        if (message.Contains("complete records", StringComparison.OrdinalIgnoreCase) || message.Contains("expand", StringComparison.OrdinalIgnoreCase)) return ResponseLength.Detailed;
        return intent is AssistantIntent.Factual or AssistantIntent.StatusOrException ? ResponseLength.Concise : ResponseLength.Medium;
    }

    private static IReadOnlyCollection<OperationalSearchRecord> SelectRelevantRecords(
        IReadOnlyCollection<OperationalSearchRecord> records,
        string message,
        ResponseLength responseLength)
    {
        var perCategory = responseLength switch { ResponseLength.Concise => 1, ResponseLength.Medium => 2, _ => 4 };
        var priority = new[] { "Customer records", "Orders", "Deliveries", "Claims", "Parts", "Inventory" };
        var categories = SelectRelevantCategories(message);
        var requestedOrders = OperationalSearchMatcher.ExtractPurchaseOrderIds(message);
        if (requestedOrders.Count > 0)
        {
            return records.Where(record => categories.Contains(record.Category) && IsRelatedToRequestedOrder(record, requestedOrders))
                .GroupBy(record => record.Category == "Orders" ? $"Orders:{ReadField(record.Content, "Order ")}" : $"{record.Category}:{record.Content}")
                .Select(group => group.First()).ToArray();
        }
        return records.Where(record => categories.Contains(record.Category)).GroupBy(record => record.Category).OrderBy(group => Array.IndexOf(priority, group.Key))
            .SelectMany(group => group.OrderByDescending(record => OperationalSearchMatcher.Matches(message, record.Content)).Take(perCategory)).ToArray();
    }

    private static bool IsRelatedToRequestedOrder(OperationalSearchRecord record, IReadOnlySet<string> requestedOrders) => record.Category switch
    {
        "Orders" => requestedOrders.Contains(ReadField(record.Content, "Order ")),
        "Deliveries" => requestedOrders.Contains(ReadField(record.Content, "order ")),
        "Claims" => requestedOrders.Contains(ReadField(record.Content, "order ")),
        _ => false
    };

    private static bool IsIdentifierScopedAnswerSafe(string message, IReadOnlyCollection<OperationalSearchRecord> records, string answer)
    {
        var requestedOrders = OperationalSearchMatcher.ExtractPurchaseOrderIds(message);
        var requestedParts = Regex.Matches(message, @"\bP-\d+\b", RegexOptions.IgnoreCase).Select(match => match.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (requestedOrders.Count == 0 && requestedParts.Count == 0) return true;

        var allowedClaims = records.Where(record => record.Category == "Claims").Select(record => ReadField(record.Content, "Claim ")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var allowedShipments = records.Where(record => record.Category == "Deliveries").Select(record => ReadField(record.Content, "Shipment ")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return !answer.Contains("presentation subset", StringComparison.OrdinalIgnoreCase)
            && !answer.Contains("no detailed record", StringComparison.OrdinalIgnoreCase)
            && Regex.Matches(answer, @"\bPO-\d{4}-\d+\b", RegexOptions.IgnoreCase).All(match => requestedOrders.Count == 0 || requestedOrders.Contains(match.Value))
            && Regex.Matches(answer, @"\bP-\d+\b", RegexOptions.IgnoreCase).All(match => requestedParts.Count == 0 || requestedParts.Contains(match.Value))
            && Regex.Matches(answer, @"\bCLM?-\d+\b", RegexOptions.IgnoreCase).All(match => allowedClaims.Contains(match.Value))
            && Regex.Matches(answer, @"\bSHP-\d+\b", RegexOptions.IgnoreCase).All(match => allowedShipments.Contains(match.Value));
    }

    private static bool IsOperationalAnswerSafe(string message, IReadOnlyCollection<OperationalSearchRecord> records, string answer) =>
        IsIdentifierScopedAnswerSafe(message, records, answer) && IsPartAvailabilityAnswerSafe(message, records, answer);

    private static bool IsPartAvailabilityAnswerSafe(string message, IReadOnlyCollection<OperationalSearchRecord> records, string answer)
    {
        if (!IsPartAvailabilityQuestion(message)) return true;
        var availableQuantity = records.Where(record => record.Category == "Inventory")
            .Sum(record => ReadIntegerField(record.Content, "available "));
        return availableQuantity <= 0 || !new[] { "out of stock", "unavailable", "not available", "no stock" }
            .Any(term => answer.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlySet<string> SelectRelevantCategories(string message)
    {
        if (message.Contains("customer", StringComparison.OrdinalIgnoreCase) || message.Contains("dealer", StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(["Customer records", "Orders", "Deliveries", "Claims"]);
        if (message.Contains("part", StringComparison.OrdinalIgnoreCase) || message.Contains("inventory", StringComparison.OrdinalIgnoreCase) || message.Contains("stock", StringComparison.OrdinalIgnoreCase))
            return new HashSet<string>(["Parts", "Inventory", "Orders", "Deliveries"]);
        if (message.Contains("claim", StringComparison.OrdinalIgnoreCase)) return new HashSet<string>(["Claims", "Orders", "Deliveries"]);
        return new HashSet<string>(["Orders", "Deliveries", "Claims"]);
    }

    private static string CreateValidatedRetrievalSummary(string message, IReadOnlyCollection<OperationalSearchRecord> allRecords,
        IReadOnlyCollection<OperationalSearchRecord> selectedRecords)
    {
        var categories = allRecords.GroupBy(record => record.Category).ToDictionary(group => group.Key, group => group.ToArray());
        var orders = categories.GetValueOrDefault("Orders", Array.Empty<OperationalSearchRecord>());
        var deliveries = categories.GetValueOrDefault("Deliveries", Array.Empty<OperationalSearchRecord>());
        var claims = categories.GetValueOrDefault("Claims", Array.Empty<OperationalSearchRecord>());
        var uniqueOrderRecords = orders.GroupBy(record => ReadField(record.Content, "Order "), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Key.Length > 0).Select(group => group.First()).ToArray();
        var uniqueOrders = uniqueOrderRecords.Select(record => ReadField(record.Content, "Order ")).ToArray();
        var statuses = uniqueOrderRecords.Select(record => ReadField(record.Content, "status ")).Where(value => value.Length > 0)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase).Select(group => $"{group.Key}: {group.Count()}").ToArray();
        var delayedDeliveries = deliveries.Count(record => record.Content.Contains("status Delayed", StringComparison.OrdinalIgnoreCase)
            || record.Content.Contains("status Exception", StringComparison.OrdinalIgnoreCase));
        var openClaims = claims.Count(record => record.Content.Contains("status Open", StringComparison.OrdinalIgnoreCase));
        var customer = categories.GetValueOrDefault("Customer records", Array.Empty<OperationalSearchRecord>()).FirstOrDefault();
        var requestedCustomer = ExtractRequestedCustomer(message);
        var customerValidated = string.IsNullOrWhiteSpace(requestedCustomer) || customer?.Content.Contains(requestedCustomer, StringComparison.OrdinalIgnoreCase) == true;
        var lines = new List<string>
        {
            $"Validated complete retrieval: {allRecords.Count} related records were retrieved from the authoritative operational database; this is not a UI page or dashboard subset.",
            $"Requested customer: {requestedCustomer ?? "not specified"}; direct customer match validated: {(customerValidated ? "yes" : "no") }.",
            $"Related orders retrieved: {uniqueOrders.Length}. Order status breakdown: {(statuses.Length == 0 ? "none" : string.Join(", ", statuses))}.",
            $"Delayed or exception deliveries retrieved: {delayedDeliveries}. Open claims retrieved: {openClaims}.",
            $"Records selected for presentation: {selectedRecords.Count}."
        };
        return string.Join('\n', lines);
    }

    private static string? ExtractRequestedCustomer(string message)
    {
        var marker = message.Contains("customer name", StringComparison.OrdinalIgnoreCase) ? "customer name"
            : message.Contains("customer", StringComparison.OrdinalIgnoreCase) ? "customer"
            : message.Contains("dealer", StringComparison.OrdinalIgnoreCase) ? "dealer" : null;
        if (marker is null) return null;
        var value = message[(message.IndexOf(marker, StringComparison.OrdinalIgnoreCase) + marker.Length)..].Trim(' ', '.', '?', '!', ':');
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static string ReadField(string content, string marker)
    {
        var index = content.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return string.Empty;
        var value = content[(index + marker.Length)..];
        var semicolon = value.IndexOf(';');
        var comma = value.IndexOf(',');
        var end = new[] { semicolon, comma }.Where(position => position >= 0).DefaultIfEmpty(-1).Min();
        return (end < 0 ? value : value[..end]).Trim(' ', '.', ',');
    }

    private static int ReadIntegerField(string content, string marker) =>
        int.TryParse(ReadField(content, marker), out var value) ? value : 0;

    private static string CreateOperationalFallback(
        IReadOnlyCollection<OperationalSearchRecord> allRecords,
        IReadOnlyCollection<OperationalSearchRecord> selectedRecords,
        string retrievalSummary,
        AssistantIntent intent,
        ResponseLength responseLength)
    {
        var lines = new List<string>();
        var customer = selectedRecords.FirstOrDefault(record => record.Category == "Customer records");
        if (customer is not null) lines.Add($"Customer overview:\n• {customer.Content}");

        if (intent is AssistantIntent.EntityLookup or AssistantIntent.Comparison)
        {
            var delayed = allRecords.Count(record => record.Content.Contains("delayed", StringComparison.OrdinalIgnoreCase)
                || record.Content.Contains("exception", StringComparison.OrdinalIgnoreCase));
            var openClaims = allRecords.Count(record => record.Category == "Claims" && record.Content.Contains("status Open", StringComparison.OrdinalIgnoreCase));
            lines.Add($"Key insights:\n• {retrievalSummary.Split('\n')[2]}"
                + (delayed > 0 ? $"\n• {delayed} record(s) indicate a delay or exception." : string.Empty)
                + (openClaims > 0 ? $"\n• {openClaims} open claim(s) require attention." : string.Empty));
        }

        var relevant = selectedRecords.Where(record => record.Category != "Customer records").ToArray();
        if (relevant.Length > 0) lines.Add($"{(intent == AssistantIntent.StatusOrException ? "Current status" : "Relevant records")}:\n"
            + string.Join('\n', relevant.Select(record => $"• {record.Content}")));
        if (intent == AssistantIntent.StatusOrException && allRecords.Any(record => record.Content.Contains("delayed", StringComparison.OrdinalIgnoreCase)
            || record.Content.Contains("exception", StringComparison.OrdinalIgnoreCase)))
            lines.Add("Recommended next step:\n• Review the reported delivery exception and follow up with the carrier or customer using the retrieved shipment details.");
        if (allRecords.Count > selectedRecords.Count && responseLength != ResponseLength.Detailed)
            lines.Add($"I found {allRecords.Count} matching records. Ask for complete records or a specific order, delivery, claim, or part to expand the detail.");
        return string.Join("\n\n", lines);
    }

    private async Task<AssistantResponse?> TrySummarizeDocumentAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        if (knowledgeLibrary is null) return null;
        var message = request.Message.Trim();
        string[] prefixes = ["Explain ", "Summarize ", "Summarise ", "Give me a summary of ", "Give a summary of "];
        var prefix = prefixes.FirstOrDefault(value => message.StartsWith(value, StringComparison.OrdinalIgnoreCase));
        if (prefix is null) return null;

        var subject = message[prefix.Length..];
        var documents = await knowledgeLibrary.GetDocumentsAsync(request.TenantId, request.ApplicationId ?? Guid.Empty, cancellationToken);
        var document = documents.Where(item => !string.IsNullOrWhiteSpace(item.Title)
                && subject.StartsWith(item.Title, StringComparison.OrdinalIgnoreCase)
                && (subject.Length == item.Title.Length || char.IsWhiteSpace(subject[item.Title.Length])
                    || subject[item.Title.Length] is '.' or '?' or '!'))
            .OrderByDescending(item => item.Title.Length).FirstOrDefault();
        if (document is null) return null;

        var answer = await GenerateAnswerAsync(request.Message, KnowledgeGenerationUnavailable,
            [new LlmGroundingSource(document.Title, document.Content)], cancellationToken, LlmAnswerMode.DocumentSummary);
        return new AssistantResponse(answer, "KNOWLEDGE", [$"Document: {document.Title}"], ["search_sop"]);
    }

    private async Task<string> GenerateAnswerAsync(
        string userMessage,
        string fallbackAnswer,
        IReadOnlyCollection<LlmGroundingSource> groundingSources,
        CancellationToken cancellationToken,
        LlmAnswerMode mode = LlmAnswerMode.TheoreticalGuidance,
        AssistantIntent intent = AssistantIntent.Factual,
        ResponseLength responseLength = ResponseLength.Medium,
        int totalRecordsFound = 0)
    {
        if (!llmAnswerGenerator.IsAvailable)
            return fallbackAnswer;

        var answer = await llmAnswerGenerator.GenerateAsync(
            new LlmAnswerGenerationRequest(userMessage, groundingSources, mode, intent, responseLength, totalRecordsFound), cancellationToken);
        return string.IsNullOrWhiteSpace(answer) ? fallbackAnswer : answer;
    }

    private static string CreateOrderStatusAnswer(DemoOrder order)
    {
        var lines = new List<string>
        {
            $"• Order {order.OrderId}: {order.Status}",
            $"• Delivery: {order.DeliveryStatus}",
            $"• Requested delivery: {order.ExpectedDelivery:dd MMM yyyy}",
            $"• Part: {order.PartNumber}"
        };
        if (!string.IsNullOrWhiteSpace(order.Alert)) lines.Add($"• Shipment update: {order.Alert}");
        if (!string.IsNullOrWhiteSpace(order.Carrier)) lines.Add($"• Carrier: {order.Carrier}");
        if (!string.IsNullOrWhiteSpace(order.TrackingNumber)) lines.Add($"• Tracking: {order.TrackingNumber}");
        if (order.ShipmentEstimatedDelivery is not null) lines.Add($"• Carrier estimated delivery: {order.ShipmentEstimatedDelivery:dd MMM yyyy}");
        return string.Join('\n', lines);
    }

    private static string CreateInventoryStatusAnswer(DemoPartStock part) => string.Join('\n',
        $"• Part {part.PartNumber}: {part.Description}",
        $"• Available quantity: {part.AvailableQuantity}",
        $"• Location: {part.Plant}",
        $"• Reorder level: {part.ReorderLevel}",
        $"• Stock status: {(part.AvailableQuantity <= 0 ? "Out of stock" : part.AvailableQuantity <= part.ReorderLevel ? "Low stock" : "Available")}");

    private static string CreateClaimStatusAnswer(DemoClaim claim) => string.Join('\n',
        $"• Claim {claim.ClaimId}: {claim.Status}",
        $"• Order: {claim.OrderId}",
        $"• Reason: {claim.Reason}",
        $"• Created: {claim.CreatedOn:dd MMM yyyy}");

    private static string CreateDashboardStatusAnswer(DemoDashboard dashboard)
    {
        var lines = new List<string>
        {
            $"• Open orders: {dashboard.OpenOrders}", $"• Orders in delivery: {dashboard.OrdersInDelivery}",
            $"• Delivery exceptions: {dashboard.DeliveryExceptions}", $"• Open claims: {dashboard.OpenClaims}",
            $"• Low-stock parts: {dashboard.LowStockParts}"
        };
        lines.AddRange(dashboard.RecentAlerts.Select(alert => $"• Alert: {alert}"));
        return string.Join('\n', lines);
    }
}
