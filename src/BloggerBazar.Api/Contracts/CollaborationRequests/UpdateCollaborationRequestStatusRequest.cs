using BloggerBazar.Domain.Enums;

namespace BloggerBazar.Api.Contracts.CollaborationRequests;

public sealed record UpdateCollaborationRequestStatusRequest(CollaborationRequestStatus Status);
