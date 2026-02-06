using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

using System.Threading;
using System.Threading.Tasks;

namespace PathingAPI.RateLimit;

public sealed class RateLimitFilter : IAsyncActionFilter
{
    private static int isBusy;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (Interlocked.CompareExchange(ref isBusy, 1, 0) != 0)
        {
            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
            return;
        }

        try
        {
            await next();
        }
        finally
        {
            Interlocked.Exchange(ref isBusy, 0);
        }
    }
}
