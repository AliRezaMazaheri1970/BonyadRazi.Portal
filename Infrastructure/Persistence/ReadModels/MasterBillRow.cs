namespace BonyadRazi.Portal.Infrastructure.Persistence.ReadModels;

public sealed class MasterBillRow
{
    public Guid MasterBillsCode { get; set; }
    public Guid? ContractCode { get; set; }

    public int? BillNo { get; set; }
    public DateTime BillDate { get; set; }
    public decimal TotalPrice { get; set; }

    public bool IsVoid { get; set; }
    public bool InformalFactor { get; set; }
}