namespace AfterSalesAI.Domain;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<Application> Applications { get; set; } = new List<Application>();
    public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = new List<KnowledgeDocument>();
    public ICollection<KnowledgeChunk> KnowledgeChunks { get; set; } = new List<KnowledgeChunk>();
}

public sealed class Application
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public ICollection<IntegrationSource> IntegrationSources { get; set; } = new List<IntegrationSource>();
    public ICollection<ToolDefinition> ToolDefinitions { get; set; } = new List<ToolDefinition>();
    public ICollection<KnowledgeDocument> KnowledgeDocuments { get; set; } = new List<KnowledgeDocument>();
    public ICollection<KnowledgeChunk> KnowledgeChunks { get; set; } = new List<KnowledgeChunk>();
}

public enum IntegrationSourceType
{
    Api = 1,
    Database = 2
}

public sealed class IntegrationSource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public IntegrationSourceType Type { get; set; }
    public string Protocol { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ConfigurationReference { get; set; } = string.Empty;
    public bool IsReadOnly { get; set; }
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public ICollection<ToolDefinition> ToolDefinitions { get; set; } = new List<ToolDefinition>();
}

public sealed class ToolDefinition
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid IntegrationSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string InputSchemaJson { get; set; } = "{}";
    public string OutputSchemaJson { get; set; } = "{}";
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public IntegrationSource IntegrationSource { get; set; } = null!;
}

public sealed class KnowledgeDocument
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}

public sealed class KnowledgeChunk
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ApplicationId { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";

    public Tenant Tenant { get; set; } = null!;
    public Application Application { get; set; } = null!;
    public KnowledgeDocument Document { get; set; } = null!;
}
