using System.Net;
using System.Security.Cryptography;

using SCNET.Authentication.Server.Infrastructure;

namespace SCNET.Authentication.Server.Endpoints;

public static class BrowserEndpoints
{
    public static void MapBrowserLogin(this WebApplication app)
    {
        app.MapGet(
            "/login/{transactionId}",
            (string transactionId, ProviderRuntime runtime, HttpContext context) =>
            {
                var subject = runtime.Execute(authority => authority.GetBrowserSubject(transactionId));
                if (subject is null) { return Results.NotFound(); }

                var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
                context.Response.Headers.ContentSecurityPolicy =
                    $"default-src 'none'; script-src 'nonce-{nonce}'; form-action 'self'; base-uri 'none'; frame-ancestors 'none'";
                var html = $$"""
                             <!doctype html><html lang="en"><meta charset="utf-8"><title>Approve SCNET login</title>
                             <h1>{{WebUtility.HtmlEncode(runtime.Service.Description.DisplayName)}}</h1>
                             <p>Only approve a request you started in your game. The following key will be authorized:</p>
                             <p><code>{{WebUtility.HtmlEncode(subject)}}</code></p>
                             <form method="post">
                               <label>Account <input name="account" autocomplete="username" maxlength="128" required></label><br>
                               <label>Password <input name="password" type="password" autocomplete="current-password" maxlength="1024" required></label>
                               <input id="secret" name="browserSecret" type="hidden">
                               <p><button type="submit">Log in and approve this identity</button></p>
                             </form>
                             <script nonce="{{nonce}}">
                               document.getElementById('secret').value = location.hash.slice(1);
                               history.replaceState(null, '', location.pathname);
                             </script></html>
                             """;
                return Results.Content(html, "text/html");
            }
        ).RequireRateLimiting("browser");
        app.MapPost(
            "/login/{transactionId}",
            async (string transactionId, ProviderRuntime runtime, HttpContext context) =>
            {
                if (!context.Request.HasFormContentType) { return Results.BadRequest(); }

                var form = await context.Request.ReadFormAsync(context.RequestAborted);
                var account = form["account"].ToString();
                var password = form["password"].ToString();
                if (!runtime.VerifyPassword(account, password))
                {
                    return Results.Content(
                        "Login failed. Start the login again in your game.",
                        "text/plain",
                        statusCode: 403
                    );
                }

                var approved = runtime.Execute(
                    authority => authority.ApproveBrowser(
                        transactionId,
                        form["browserSecret"].ToString(),
                        account,
                        DateTimeOffset.UtcNow
                    ),
                    true
                );
                return Results.Content(
                    approved ? "Approved. Return to your game." : "Approval failed or expired. Return to your game.",
                    "text/plain",
                    statusCode: approved ? 200 : 403
                );
            }
        ).RequireRateLimiting("browser");
    }
}
