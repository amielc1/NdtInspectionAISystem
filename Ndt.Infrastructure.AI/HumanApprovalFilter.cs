using Microsoft.SemanticKernel;
using System;
using System.Threading.Tasks;

namespace Ndt.Infrastructure.AI;

public class HumanApprovalFilter : IFunctionInvocationFilter
{
    private readonly Func<string, Task<bool>> _confirmationCallback;

    public HumanApprovalFilter(Func<string, Task<bool>> confirmationCallback)
    {
        _confirmationCallback = confirmationCallback;
    }

    public async Task OnFunctionInvocationAsync(FunctionInvocationContext context, Func<FunctionInvocationContext, Task> next)
    {
        if (context.Function.Name == "SubmitDefectReport")
        {
            bool confirmed = await _confirmationCallback("Do you approve submitting this report?");
            if (!confirmed)
            {
                context.Result = new FunctionResult(context.Function, "Action cancelled by the user.");
                return;
            }
        }

        await next(context);
    }
}
