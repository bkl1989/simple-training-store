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

//TODO: uniform order for aggregateID
public record CreateLearnerUser(Guid CorrelationId, string firstName, string lastName, Guid aggregateId);

public record LearnerUserCreated(Guid CorrelationId);

public record CreateOrderUser(Guid CorrelationId, Guid aggregateId);

public record OrderUserCreated(Guid CorrelationId);

//Create Course

public record CreateCourse(Guid AggregateId, string Title, string Description, int price);

public record CreateCourseSagaStarted(Guid CorrelationId, Guid AggregateId, string Title, string Description, int price);