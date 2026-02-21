using PiiGateway.Core.Domain.Entities;

namespace PiiGateway.Core.Interfaces.Repositories;

public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id);
    Task<Organization> CreateAsync(Organization organization);
}
