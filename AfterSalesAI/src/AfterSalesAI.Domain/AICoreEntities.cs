namespace AfterSalesAI.Domain;

public sealed class CoreTenant
{
    public Guid TenantId { get; set; }
    public string TenantCode { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TenantSystemPrompt { get; set; } = string.Empty;
    public Guid DatabaseConfigurationId { get; set; }
    public bool IsWrapperApiEnabled { get; set; }
    public bool IsActive { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
    public DateTimeOffset UpdatedDate { get; set; }
}
public sealed class CoreDatabaseConfiguration
{
    public Guid DatabaseConfigurationId { get; set; }
    public Guid TenantId { get; set; }
    public string ConfigurationReference { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
public sealed class CoreOperation
{
    public Guid OperationId { get; set; }
    public Guid TenantId { get; set; }
    public string OperationName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string RelativeUrl { get; set; } = string.Empty;
    public string HttpMethod { get; set; } = string.Empty;
    public string Headers { get; set; } = "{}";
    public string RequestParameters { get; set; } = "{}";
    public string RequestBodyTemplate { get; set; } = "{}";
    public string ResponseMapping { get; set; } = "{}";
    public int TimeoutSeconds { get; set; }
    public bool IsWrapperApi { get; set; }
    public string StoredProcedureName { get; set; } = string.Empty;
    public string AllowedParameters { get; set; } = "{}";
    public bool IsActive { get; set; }
}
public sealed class CoreDocument
{
    public Guid DocumentId { get; set; }
    public Guid TenantId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ExtractedText { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTimeOffset UploadedDate { get; set; }
}
public sealed class CoreDocumentChunk
{
    public Guid ChunkId { get; set; }
    public Guid TenantId { get; set; }
    public Guid DocumentId { get; set; }
    public int ChunkSequence { get; set; }
    public string ChunkContent { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
}
public sealed class CoreChatSession
{
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedDate { get; set; }
}
public sealed class CoreChatMessage
{
    public Guid MessageId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedDate { get; set; }
}
