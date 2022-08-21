using System.Threading.Tasks;

namespace Furtherance.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
