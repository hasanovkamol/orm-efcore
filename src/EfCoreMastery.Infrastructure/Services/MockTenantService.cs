using EfCoreMastery.Application.Interfaces;

namespace EfCoreMastery.Infrastructure.Services;

public class MockTenantService : ITenantService
{
    private readonly int _tenantId;

    public MockTenantService(int tenantId = 1)
    {
        _tenantId = tenantId;
    }

    public int GetCurrentTenantId() => _tenantId;
}
