namespace BonyadRazi.Shared.Contracts.Companies;

public sealed class CompanyContractReportDto
{
    public Guid ContractsCode { get; set; }
    public int ContractNo { get; set; }
    public int ContractYear { get; set; }
    public DateTime? CompletedDate { get; set; }
    public CompanyContractReportType ReportType { get; set; }
}