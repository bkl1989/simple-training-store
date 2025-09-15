namespace Contracts;

public record AskForOrchestratorStatus (Guid CorrelationId);
public record SendOrchestratorStatus (Guid CorrelationId, string status);
