using BonyadRazi.Portal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BonyadRazi.Portal.SecurityTests;

// ✅ جای Program همین Program پروژه Api است (همون که الان داری)
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // ✅ اینجا فقط DbContext مربوط به Audit را InMemory می‌کنیم
            // نام DbContext را دقیقاً مطابق پروژه خودت بگذار:
            // RasfPortalDbContext یا RasfPortaDbContext (هر چی داخل Infrastructure داری)

            // 1) ثبت قبلی DbContext را حذف کن
            var descriptors = services
                .Where(d => d.ServiceType.FullName != null &&
                            (d.ServiceType.FullName.Contains("DbContextOptions") ||
                             d.ServiceType.FullName.Contains("RasfPortalDbContext") ||
                             d.ServiceType.FullName.Contains("RasfPortaDbContext")))
                .ToList();

            foreach (var d in descriptors)
                services.Remove(d);

            // 2) DbContext InMemory را اضافه کن
            // ⛔ این خط را مطابق نام DbContext خودت تنظیم کن:
            services.AddDbContext<RasfPortalDbContext>(opt =>
                opt.UseInMemoryDatabase("RasfPortal_TestDb"));

            // 3) دیتابیس را بساز
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<RasfPortalDbContext>();
            db.Database.EnsureCreated();
        });
    }
}