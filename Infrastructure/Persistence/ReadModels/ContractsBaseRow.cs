namespace BonyadRazi.Portal.Infrastructure.Persistence.ReadModels;

public sealed class ContractsBaseRow
{
    public Guid ContractsCode { get; set; }
    public int? ContractNo { get; set; }

    public bool Void { get; set; }

    public Guid CompanyInvoice { get; set; }
}