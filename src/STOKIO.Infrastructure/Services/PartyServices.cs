using Microsoft.EntityFrameworkCore;
using STOKIO.Application.Abstractions;
using STOKIO.Application.Common;
using STOKIO.Application.Dtos.Parties;
using STOKIO.Domain.Entities;
using STOKIO.Infrastructure.Persistence;

namespace STOKIO.Infrastructure.Services;

public sealed class CustomerService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    AuditWriter auditWriter) : ICustomerService
{
    public async Task<PagedResult<CustomerDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.Customers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                (x.Phone != null && x.Phone.Contains(term)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var customers = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new CustomerDto(
                x.Id,
                x.Code,
                x.Name,
                x.ContactName,
                x.Email,
                x.Phone,
                x.TaxNumber,
                x.Address,
                x.Notes,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDto>(customers, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, null, cancellationToken);

        var customer = new Customer
        {
            TenantId = currentTenant.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            ContactName = Optional(request.ContactName),
            Email = Optional(request.Email),
            Phone = Optional(request.Phone),
            TaxNumber = Optional(request.TaxNumber),
            Address = Optional(request.Address),
            Notes = Optional(request.Notes)
        };

        dbContext.Customers.Add(customer);
        auditWriter.AddSnapshot("customer.created", nameof(Customer), customer.Id, null, Snapshot(customer));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var customer = await FindAsync(id, cancellationToken);
        var oldValue = Snapshot(customer);
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, customer.Id, cancellationToken);

        customer.Code = code;
        customer.Name = request.Name.Trim();
        customer.ContactName = Optional(request.ContactName);
        customer.Email = Optional(request.Email);
        customer.Phone = Optional(request.Phone);
        customer.TaxNumber = Optional(request.TaxNumber);
        customer.Address = Optional(request.Address);
        customer.Notes = Optional(request.Notes);
        customer.IsActive = request.IsActive;

        auditWriter.AddSnapshot("customer.updated", nameof(Customer), customer.Id, oldValue, Snapshot(customer));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(customer);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var customer = await FindAsync(id, cancellationToken);
        var oldValue = Snapshot(customer);
        customer.IsActive = false;
        auditWriter.AddSnapshot("customer.deactivated", nameof(Customer), customer.Id, oldValue, Snapshot(customer));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Customer> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return customer ?? throw new AppProblemException(404, "customer_not_found", "Customer was not found.");
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? currentCustomerId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Customers.AnyAsync(
            x => x.Code == code && (!currentCustomerId.HasValue || x.Id != currentCustomerId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "customer_code_exists", "A customer with this code already exists.");
        }
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }

    private static CustomerDto ToDto(Customer customer)
    {
        return new CustomerDto(
            customer.Id,
            customer.Code,
            customer.Name,
            customer.ContactName,
            customer.Email,
            customer.Phone,
            customer.TaxNumber,
            customer.Address,
            customer.Notes,
            customer.IsActive,
            customer.CreatedAt);
    }

    private static object Snapshot(Customer customer)
    {
        return new
        {
            customer.Id,
            customer.Code,
            customer.Name,
            customer.ContactName,
            customer.Email,
            customer.Phone,
            customer.TaxNumber,
            customer.Address,
            customer.Notes,
            customer.IsActive
        };
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed class SupplierService(
    StokioDbContext dbContext,
    ICurrentTenant currentTenant,
    AuditWriter auditWriter) : ISupplierService
{
    public async Task<PagedResult<SupplierDto>> ListAsync(string? search, bool? isActive, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var (normalizedPage, normalizedPageSize) = Paging.Normalize(page, pageSize);
        var query = dbContext.Suppliers.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(term) ||
                x.Name.ToLower().Contains(term) ||
                (x.Email != null && x.Email.ToLower().Contains(term)) ||
                (x.Phone != null && x.Phone.Contains(term)));
        }

        if (isActive.HasValue)
        {
            query = query.Where(x => x.IsActive == isActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var suppliers = await query
            .OrderBy(x => x.Name)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .Select(x => new SupplierDto(
                x.Id,
                x.Code,
                x.Name,
                x.ContactName,
                x.Email,
                x.Phone,
                x.TaxNumber,
                x.Address,
                x.Notes,
                x.IsActive,
                x.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<SupplierDto>(suppliers, normalizedPage, normalizedPageSize, totalCount);
    }

    public async Task<SupplierDto> CreateAsync(CreateSupplierRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, null, cancellationToken);

        var supplier = new Supplier
        {
            TenantId = currentTenant.TenantId,
            Code = code,
            Name = request.Name.Trim(),
            ContactName = Optional(request.ContactName),
            Email = Optional(request.Email),
            Phone = Optional(request.Phone),
            TaxNumber = Optional(request.TaxNumber),
            Address = Optional(request.Address),
            Notes = Optional(request.Notes)
        };

        dbContext.Suppliers.Add(supplier);
        auditWriter.AddSnapshot("supplier.created", nameof(Supplier), supplier.Id, null, Snapshot(supplier));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(supplier);
    }

    public async Task<SupplierDto> UpdateAsync(Guid id, UpdateSupplierRequest request, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var supplier = await FindAsync(id, cancellationToken);
        var oldValue = Snapshot(supplier);
        var code = NormalizeCode(request.Code);
        await EnsureCodeAvailableAsync(code, supplier.Id, cancellationToken);

        supplier.Code = code;
        supplier.Name = request.Name.Trim();
        supplier.ContactName = Optional(request.ContactName);
        supplier.Email = Optional(request.Email);
        supplier.Phone = Optional(request.Phone);
        supplier.TaxNumber = Optional(request.TaxNumber);
        supplier.Address = Optional(request.Address);
        supplier.Notes = Optional(request.Notes);
        supplier.IsActive = request.IsActive;

        auditWriter.AddSnapshot("supplier.updated", nameof(Supplier), supplier.Id, oldValue, Snapshot(supplier));
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToDto(supplier);
    }

    public async Task DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        EnsureTenant();
        var supplier = await FindAsync(id, cancellationToken);
        var oldValue = Snapshot(supplier);
        supplier.IsActive = false;
        auditWriter.AddSnapshot("supplier.deactivated", nameof(Supplier), supplier.Id, oldValue, Snapshot(supplier));
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Supplier> FindAsync(Guid id, CancellationToken cancellationToken)
    {
        var supplier = await dbContext.Suppliers.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return supplier ?? throw new AppProblemException(404, "supplier_not_found", "Supplier was not found.");
    }

    private async Task EnsureCodeAvailableAsync(string code, Guid? currentSupplierId, CancellationToken cancellationToken)
    {
        var exists = await dbContext.Suppliers.AnyAsync(
            x => x.Code == code && (!currentSupplierId.HasValue || x.Id != currentSupplierId.Value),
            cancellationToken);

        if (exists)
        {
            throw new AppProblemException(409, "supplier_code_exists", "A supplier with this code already exists.");
        }
    }

    private void EnsureTenant()
    {
        if (!currentTenant.HasTenant)
        {
            throw new AppProblemException(401, "tenant_required", "Tenant context is required.");
        }
    }

    private static SupplierDto ToDto(Supplier supplier)
    {
        return new SupplierDto(
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.TaxNumber,
            supplier.Address,
            supplier.Notes,
            supplier.IsActive,
            supplier.CreatedAt);
    }

    private static object Snapshot(Supplier supplier)
    {
        return new
        {
            supplier.Id,
            supplier.Code,
            supplier.Name,
            supplier.ContactName,
            supplier.Email,
            supplier.Phone,
            supplier.TaxNumber,
            supplier.Address,
            supplier.Notes,
            supplier.IsActive
        };
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
