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

// Create User

public record CreateUser(Guid CorrelationId, string firstName, string lastName, string email, string password);

public record CreateUserSagaStarted (Guid CorrelationId, Guid AggregateId, string firstName, string lastName, string email);

public record CreateAuthUser(Guid CorrelationId, string email, string password, Guid aggregateId);

public record AuthUserCreated(Guid CorrelationId);