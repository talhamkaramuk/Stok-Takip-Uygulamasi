using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using STOKIO.Application.Dtos.Auth;
using STOKIO.Application.Dtos.Counts;
using STOKIO.Application.Dtos.Exports;
using STOKIO.Application.Dtos.Operations;
using STOKIO.Application.Dtos.Products;
using STOKIO.Application.Dtos.Stock;
using STOKIO.Domain.Enums;

namespace STOKIO.Tests.E2E;

public sealed class OperationalSmokeTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    [E2EFact]
    [Trait("Layer", "E2ESmoke")]
    public async Task CoreOperationalFlow_CompletesAgainstRunningApi()
    {
        using var httpClient = new HttpClient
        {
            BaseAddress = new Uri(E2ETestSettings.BaseUrl!, UriKind.Absolute)
        };
        var suffix = Guid.CreateVersion7().ToString("N")[..10];
        var tenantSlug = $"e2e-{suffix}";
        var email = $"owner-{suffix}@stokio.test";
        const string password = "SmokeTest-12345!";
        var barcode = $"868{suffix[..9]}";

        await PostAsync<AuthResponse>(
            httpClient,
            "/api/v1/auth/register-tenant",
            new RegisterTenantRequest("STOKIO E2E", tenantSlug, "Smoke Owner", email, password));

        var login = await PostAsync<AuthResponse>(
            httpClient,
            "/api/v1/auth/login",
            new LoginRequest(tenantSlug, email, password));
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

        var product = await PostAsync<ProductDto>(
            httpClient,
            "/api/v1/products",
            new CreateProductRequest(
                $"E2E-SKU-{suffix}",
                "E2E Smoke Product",
                "Created by E2E smoke test",
                "Smoke",
                2,
                10,
                [barcode]));

        await PostAsync<StockMovementDto>(
            httpClient,
            "/api/v1/stock/movements",
            new CreateStockMovementRequest(product.Id, StockMovementType.Out, 2, "E2E stock out"));
        await PostAsync<StockMovementDto>(
            httpClient,
            "/api/v1/stock/movements",
            new CreateStockMovementRequest(product.Id, StockMovementType.In, 4, "E2E stock in"));

        var count = await PostAsync<InventoryCountDto>(
            httpClient,
            "/api/v1/counts",
            new CreateInventoryCountRequest($"E2E Count {suffix}"));
        await PostAsync<JsonElement>(
            httpClient,
            $"/api/v1/counts/{count.Id}/items/scan",
            new ScanCountItemRequest(barcode, 12));
        await PostAsync<InventoryCountDto>(
            httpClient,
            $"/api/v1/counts/{count.Id}/close",
            new CloseInventoryCountRequest(ApplyDifferences: true));

        var order = await PostAsync<SalesOrderDto>(
            httpClient,
            "/api/v1/orders",
            new CreateSalesOrderRequest(
                "E2E Customer",
                null,
                "E2E sales order",
                [new OperationItemRequest(product.Id, 2)]));
        await PostAsync<ShipmentDto>(
            httpClient,
            "/api/v1/shipments",
            new CreateShipmentRequest(
                order.Id,
                "E2E Customer",
                null,
                null,
                "E2E shipment",
                [new OperationItemRequest(product.Id, 1)]));
        await PostAsync<ReturnRequestDto>(
            httpClient,
            "/api/v1/returns",
            new CreateReturnRequestRequest(
                order.Id,
                "E2E Customer",
                null,
                "E2E return",
                [new OperationItemRequest(product.Id, 1)]));

        var exportJob = await PostAsync<ExportJobDto>(
            httpClient,
            "/api/v1/exports/jobs",
            new CreateExportJobRequest(ExportJobType.CurrentStock));

        Assert.NotEqual(Guid.Empty, exportJob.Id);
    }

    private static async Task<TResponse> PostAsync<TResponse>(HttpClient httpClient, string path, object request)
    {
        using var response = await httpClient.PostAsJsonAsync(path, request, JsonOptions);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions)
            ?? throw new InvalidOperationException($"The response body for {path} was empty.");
    }
}
