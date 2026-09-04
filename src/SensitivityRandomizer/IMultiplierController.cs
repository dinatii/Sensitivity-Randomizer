using System;
using System.Threading;
using System.Threading.Tasks;

namespace SensitivityRandomizer
{
    public interface IMultiplierController
    {
        TimeSpan WriteDelay { get; }
        Task<MultiplierReadback> InspectAsync(CancellationToken cancellationToken);
        Task<MultiplierReadback> ApplyAsync(double multiplier, CancellationToken cancellationToken);
        Task<MultiplierReadback> ResetAsync(CancellationToken cancellationToken);
    }
}
