using PiiGateway.Core.Domain.Entities;
using PiiGateway.Core.Interfaces.Repositories;
using PiiGateway.Infrastructure.Data;

namespace PiiGateway.Infrastructure.Repositories;

public class OrganizationRepository : IOrganizationRepository
{
    private readonly PiiGatewayDbContext _context;

    public OrganizationRepository(PiiGatewayDbContext context)
    {
        _context = context;
    }

    public async Task<Organization?> GetByIdAsync(Guid id)
    {
        return await _context.Organizations.FindAsync(id);
    }

    public async Task<Organization> CreateAsync(Organization organization)
    {
        _context.Organizations.Add(organization);
        await _context.SaveChangesAsync();
        return organization;
    }
}
