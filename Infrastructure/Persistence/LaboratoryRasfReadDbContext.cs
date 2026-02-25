using BonyadRazi.Portal.Infrastructure.Persistence.ReadModels;
using Microsoft.EntityFrameworkCore;

namespace BonyadRazi.Portal.Infrastructure.Persistence;

public sealed class LaboratoryRasfReadDbContext : DbContext
{
    public LaboratoryRasfReadDbContext(DbContextOptions<LaboratoryRasfReadDbContext> options) : base(options) { }

    public DbSet<MasterBillRow> MasterBills => Set<MasterBillRow>();
    public DbSet<ContractsBaseRow> ContractsBase => Set<ContractsBaseRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MasterBillRow>(e =>
        {
            e.ToTable("MasterBills", "dbo");
            e.HasKey(x => x.MasterBillsCode);

            e.Property(x => x.MasterBillsCode).HasColumnName("MasterBillsCode");
            e.Property(x => x.ContractCode).HasColumnName("ContractCode");
            e.Property(x => x.BillNo).HasColumnName("BillNo");
            e.Property(x => x.BillDate).HasColumnName("BillDate");
            e.Property(x => x.TotalPrice).HasColumnName("TotalPrice");
            e.Property(x => x.IsVoid).HasColumnName("IsVoid");
            e.Property(x => x.InformalFactor).HasColumnName("InformalFactor");
        });

        modelBuilder.Entity<ContractsBaseRow>(e =>
        {
            e.ToTable("Contracts_Base", "dbo");
            e.HasKey(x => x.ContractsCode);

            e.Property(x => x.ContractsCode).HasColumnName("ContractsCode");
            e.Property(x => x.ContractNo).HasColumnName("ContractNo");
            e.Property(x => x.Void).HasColumnName("Void");
            e.Property(x => x.CompanyInvoice).HasColumnName("Company_Invoice");
        });
    }
}