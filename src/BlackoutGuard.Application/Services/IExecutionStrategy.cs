using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackoutGuard.Application.Services;

public interface IExecutionStrategy
{
    Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken ct = default);
}
