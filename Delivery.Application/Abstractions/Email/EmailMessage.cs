namespace Delivery.Application.Abstractions.Email;

public sealed record EmailMessage(
    string To,
    string Subject,
    string TextBody,
    string? HtmlBody = null);
