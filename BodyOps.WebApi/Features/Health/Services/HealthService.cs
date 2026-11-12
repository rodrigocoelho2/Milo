using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Accounts.Repositories;
using BodyOps.WebApi.Features.Health.Models.DTOs;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Health.Services;

/// <summary>
/// The health check: also proves the database answers.
/// </summary>
/// <param name="userRepository">Account data access.</param>
/// <param name="options">The BodyOps options.</param>
internal sealed class HealthService(
    IUserRepository userRepository,
    IOptions<BodyOpsOptions> options) : IHealthService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IOptions<BodyOpsOptions> _options = options;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<HealthDTO>> GetAsync()
    {
        var count = await _userRepository.CountAsync();
        return ServiceApiResponse<HealthDTO>.Success(new HealthDTO
        {
            FirstRun = count == 0,
            Registration = count == 0 || _options.Value.AllowRegistration,
            Discord = _options.Value.DiscordEnabled,
        });
    }
}
