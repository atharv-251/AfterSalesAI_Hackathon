using AfterSalesAI.Application;
using AfterSalesAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace AfterSalesAI.Infrastructure;

public sealed class CoreChatService(AICoreDbContext db, CoreTenantGuard guard, TenantAssistantService assistant, TenantLlmContext? llmContext = null)
{
    public async Task<AssistantResponse> HandleAsync(AssistantRequest request, CancellationToken cancellationToken)
    {
        await guard.RequireAsync(request.TenantId, cancellationToken: cancellationToken);
        CoreChatSession session;
        if (request.SessionId.HasValue)
        {
            session = await db.Sessions.SingleOrDefaultAsync(x => x.TenantId == request.TenantId && x.SessionId == request.SessionId, cancellationToken)
                ?? throw new TenantAccessException();
        }
        else
        {
            session = new CoreChatSession { TenantId = request.TenantId, SessionId = Guid.NewGuid(), CreatedDate = DateTimeOffset.UtcNow };
        }
        if (llmContext is not null)
        {
            var previous = await db.Messages.AsNoTracking().Where(x => x.TenantId == request.TenantId && x.SessionId == session.SessionId)
                .OrderByDescending(x => x.CreatedDate).Take(10).ToArrayAsync(cancellationToken);
            var selected = new List<(string Role, string Content)>();
            var characters = 0;
            foreach (var message in previous)
            {
                if (characters + message.Content.Length > 12000) break;
                selected.Add((message.Role, message.Content));
                characters += message.Content.Length;
            }
            selected.Reverse();
            llmContext.History = selected;
        }
        var response = await assistant.HandleAsync(request, cancellationToken);
        if (!request.SessionId.HasValue) db.Sessions.Add(session);
        var now = DateTimeOffset.UtcNow;
        db.Messages.AddRange(
            new CoreChatMessage { MessageId = Guid.NewGuid(), TenantId = request.TenantId, SessionId = session.SessionId, Role = "user", Content = request.Message, CreatedDate = now },
            new CoreChatMessage { MessageId = Guid.NewGuid(), TenantId = request.TenantId, SessionId = session.SessionId, Role = "assistant", Content = response.Answer, CreatedDate = now.AddTicks(1) });
        await db.SaveChangesAsync(cancellationToken);
        return response with { SessionId = session.SessionId };
    }
}
