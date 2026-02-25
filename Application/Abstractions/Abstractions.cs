using BonyadRazi.Portal.Application.Documents;
using BonyadRazi.Shared.Contracts.Companies;
using Shared.Entity;

namespace BonyadRazi.Portal.Application.Abstractions;

public interface ICompanyInvoiceReportService
{
    Task<IReadOnlyCollection<CompanyInvoiceDto>> GetInvoicesByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CompanyWorkflowDto>> GetWorkflowByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<CompanyContractReportDto>> GetContractReportsByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default);

    Task<CompanyInvoiceDocument?> GetContractReportPdfAsync(Guid companyCode, Guid contractsCode, CompanyContractReportType reportType, CancellationToken cancellationToken = default);

    Task<CompanyInvoiceDocument?> GetInvoicePdfAsync(Guid companyCode, Guid masterBillCode, CancellationToken cancellationToken = default);
}