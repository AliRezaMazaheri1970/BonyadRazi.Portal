namespace BonyadRazi.Portal.Application.Documents;

public sealed record CompanyInvoiceDocument(
    string FileName,
    byte[] Content,
    string ContentType);