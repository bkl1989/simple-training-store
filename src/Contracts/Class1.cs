namespace Contracts;

// Orchestrator
public record AskForOrchestratorStatus(Guid CorrelationId);
public record SendOrchestratorStatus(Guid CorrelationId, string status);

// Order
public record AskForOrderServiceStatus(Guid CorrelationId);
public record SendOrderServiceStatus(Guid CorrelationId, string status);

// Auth
public record AskForAuthServiceStatus(Guid CorrelationId);
public record SendAuthServiceStatus(Guid CorrelationId, string status);

// Learner
public record AskForLearnerServiceStatus(Guid CorrelationId);
public record SendLearnerServiceStatus(Guid CorrelationId, string status);
