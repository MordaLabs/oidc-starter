namespace Backend.Auth;

internal enum OperatorMutationResult { Success, Missing, Conflict, StorageUnavailable }

internal sealed record OperatorRequestRecord(
    Guid Id, string Email, DateTimeOffset CreatedAt, string Status,
    DateTimeOffset? DecidedAt, Guid? InvitationId);
