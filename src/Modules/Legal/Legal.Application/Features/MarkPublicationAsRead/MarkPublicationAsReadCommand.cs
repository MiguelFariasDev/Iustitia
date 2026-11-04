using Advocacia.BuildingBlocks.Application.Messaging;

namespace Advocacia.Modules.Legal.Application.Features.MarkPublicationAsRead;

public sealed record MarkPublicationAsReadCommand(Guid PublicationId) : ICommand;
