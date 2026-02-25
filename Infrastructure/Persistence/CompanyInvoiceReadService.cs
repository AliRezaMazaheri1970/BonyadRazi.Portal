using BonyadRazi.Portal.Application.Abstractions;
using BonyadRazi.Portal.Application.Documents;
using BonyadRazi.Shared.Contracts.Companies;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Entity;

namespace BonyadRazi.Portal.Infrastructure.Persistence;

public sealed class CompanyInvoiceReadService : ICompanyInvoiceReportService
{
    private readonly LaboratoryRasfReadDbContext _db;
    private readonly ILogger<CompanyInvoiceReadService> _logger;

    public CompanyInvoiceReadService(LaboratoryRasfReadDbContext db, ILogger<CompanyInvoiceReadService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<CompanyInvoiceDto>> GetInvoicesByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default)
    {
        if (companyCode == Guid.Empty)
            return [];

        try
        {
            var query =
                from mb in _db.MasterBills.AsNoTracking()
                join cb in _db.ContractsBase.AsNoTracking()
                    on mb.ContractCode equals (Guid?)cb.ContractsCode into cbj
                from cb in cbj.DefaultIfEmpty()
                where
                    mb.IsVoid == false &&
                    mb.InformalFactor == false &&
                    cb != null &&
                    cb.Void == false &&
                    cb.CompanyInvoice == companyCode
                orderby mb.BillDate descending, mb.MasterBillsCode descending
                select new CompanyInvoiceDto
                {
                    MasterBillCode = mb.MasterBillsCode,
                    BillNo = mb.BillNo.HasValue ? mb.BillNo.Value.ToString() : string.Empty,
                    ContractNo = cb!.ContractNo.HasValue ? cb.ContractNo.Value.ToString() : string.Empty,
                    BillDate = mb.BillDate,
                    TotalPrice = mb.TotalPrice
                };

            return await query.ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load invoices for CompanyCode {CompanyCode}.", companyCode);
            return [];
        }
    }

    // مرحله‌های بعد
    public Task<IReadOnlyCollection<CompanyWorkflowDto>> GetWorkflowByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<IReadOnlyCollection<CompanyContractReportDto>> GetContractReportsByCompanyAsync(Guid companyCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CompanyInvoiceDocument?> GetContractReportPdfAsync(Guid companyCode, Guid contractsCode, CompanyContractReportType reportType, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public Task<CompanyInvoiceDocument?> GetInvoicePdfAsync(Guid companyCode, Guid masterBillCode, CancellationToken cancellationToken = default) => throw new NotImplementedException();
}