namespace Backend.Auth;

internal enum InvitationDecisionReason
{
    BoundIdentity,
    NewBinding,
    UnsupportedProvider,
    InvalidSubject,
    InvalidEmail,
    BindingMissing,
    InvitationMissing,
    ApprovalRequired,
    Expired,
    Revoked,
    AlreadyBound,
    IdentityConflict,
    StorageUnavailable
}

internal readonly record struct InvitationDecision(
    bool Allowed,
    InvitationDecisionReason Reason,
    Guid? InvitationId = null)
{
    public static InvitationDecision Allow(InvitationDecisionReason reason, Guid invitationId)
        => new(true, reason, invitationId);

    public static InvitationDecision Deny(InvitationDecisionReason reason)
        => new(false, reason);
}
