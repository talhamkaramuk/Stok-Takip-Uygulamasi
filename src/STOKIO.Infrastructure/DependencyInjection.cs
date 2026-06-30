using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using STOKIO.Application.Abstractions;
using STOKIO.Infrastructure.Persistence;
using STOKIO.Infrastructure.Security;
using STOKIO.Infrastructure.Services;

namespace STOKIO.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

        services.AddDbContext<StokioDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddSingleton(Options.Create(AuthOptions.FromConfiguration(configuration)));
        services.AddSingleton(Options.Create(JwtOptions.FromConfiguration(configuration)));
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IUserManagementService, UserManagementService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IWarehouseService, WarehouseService>();
        services.AddScoped<IInventoryCountService, InventoryCountService>();
        services.AddScoped<ISalesOrderService, SalesOrderService>();
        services.AddScoped<IPurchaseRequestService, PurchaseRequestService>();
        services.AddScoped<IShipmentService, ShipmentService>();
        services.AddScoped<IReturnRequestService, ReturnRequestService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IExportService, ExportService>();
        services.AddScoped<IExportJobService, ExportJobService>();
        services.AddScoped<IExportJobProcessor, ExportJobProcessor>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddSingleton<ExportJobFileStore>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<IJwtTokenService, JwtTokenService>();
        services.AddScoped<AuditWriter>();
        services.AddScoped<WarehouseStockLedger>();
        services.AddScoped<IdempotencyService>();
        services.AddScoped<DbTransactionRunner>();

        return services;
    }
}
