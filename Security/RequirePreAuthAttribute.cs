using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace SecureGate1.Security;

[AttributeUsage(AttributeTargets.Class)]
public sealed class RequirePreAuthAttribute : Attribute, IAsyncPageFilter
{
    private readonly string[] _stages;

    public RequirePreAuthAttribute(params string[] stages)
    {
        _stages = stages;
    }

    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var auth = await context.HttpContext.AuthenticateAsync(AuthSchemes.PreAuth);
        var stage = auth.Principal?.FindFirst(AppClaims.PreAuthStage)?.Value;
        var stageMatches = _stages.Length == 0 || (stage is not null && _stages.Contains(stage));
        if (!auth.Succeeded || !stageMatches)
        {
            context.Result = new RedirectToPageResult(auth.Succeeded ? "/Account/Continue" : "/Account/Login");
            return;
        }

        await next();
    }
}
