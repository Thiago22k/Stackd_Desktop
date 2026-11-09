using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Stackd.Services;

/// <summary>
/// Inicio de sesión con Steam (OpenID 2.0). Abre la página oficial de Steam en el navegador y recibe la
/// respuesta en un listener local (127.0.0.1). Stackd nunca ve la contraseña: solo obtiene el SteamID64.
/// </summary>
public static class SteamOpenIdLogin
{
    private const string Endpoint = "https://steamcommunity.com/openid/login";
    private const string IdentifierSelect = "http://specs.openid.net/auth/2.0/identifier_select";
    private const string CallbackPath = "/steam/callback";
    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(3);
    private static readonly Regex ClaimedIdRegex = new(@"^https://steamcommunity\.com/openid/id/(\d{17})$", RegexOptions.Compiled);

    public static async Task<(string? SteamId, string? Error)> SignInAsync(HttpClient http, CancellationToken ct)
    {
        var port = GetFreePort();
        var prefix = $"http://127.0.0.1:{port}/";
        var returnTo = prefix.TrimEnd('/') + CallbackPath;

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            return (null, $"Couldn't start the local sign-in listener: {ex.Message}");
        }

        try
        {
            var parameters = new Dictionary<string, string>
            {
                ["openid.ns"] = "http://specs.openid.net/auth/2.0",
                ["openid.mode"] = "checkid_setup",
                ["openid.return_to"] = returnTo,
                ["openid.realm"] = prefix,
                ["openid.identity"] = IdentifierSelect,
                ["openid.claimed_id"] = IdentifierSelect
            };

            var url = Endpoint + "?" + string.Join("&",
                parameters.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                return (null, $"Couldn't open your browser: {ex.Message}");
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(Timeout);

            HttpListenerContext? callback = null;

            while (callback is null)
            {
                var pending = listener.GetContextAsync();
                var finished = await Task.WhenAny(pending, Task.Delay(System.Threading.Timeout.Infinite, timeoutCts.Token));

                if (finished != pending)
                {
                    // Evita una excepción sin observar cuando el listener se cierre.
                    _ = pending.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
                    return (null, ct.IsCancellationRequested
                        ? "Sign-in cancelled."
                        : "Sign-in timed out. Try again and finish it in your browser within 3 minutes.");
                }

                var context = await pending;
                if (context.Request.Url?.AbsolutePath == CallbackPath)
                {
                    callback = context;
                }
                else
                {
                    // Peticiones ajenas (por ejemplo el favicon del navegador).
                    context.Response.StatusCode = 404;
                    context.Response.Close();
                }
            }

            var (steamId, error) = await ValidateAsync(http, callback.Request.Url!.Query, returnTo, ct);
            await RespondAsync(callback.Response, steamId is not null, error);
            return (steamId, error);
        }
        catch (OperationCanceledException)
        {
            return (null, "Sign-in cancelled.");
        }
        finally
        {
            listener.Close();
        }
    }

    private static async Task<(string? SteamId, string? Error)> ValidateAsync(
        HttpClient http, string queryString, string returnTo, CancellationToken ct)
    {
        var query = HttpUtility.ParseQueryString(queryString);

        if (query["openid.mode"] == "cancel")
            return (null, "Sign-in was cancelled in Steam.");

        if (query["openid.mode"] != "id_res")
            return (null, "Steam returned an unexpected response.");

        if (query["openid.return_to"] != returnTo ||
            query["openid.op_endpoint"] != Endpoint)
            return (null, "The sign-in response didn't match the request.");

        var claimed = query["openid.claimed_id"];
        var match = claimed is null ? null : ClaimedIdRegex.Match(claimed);
        if (match is null || !match.Success)
            return (null, "Steam didn't return a valid account.");

        // Steam comprueba su propia firma: se le devuelven los mismos campos con mode=check_authentication.
        var form = new List<KeyValuePair<string, string>>();
        foreach (var key in query.AllKeys)
        {
            if (key is null || !key.StartsWith("openid.", StringComparison.Ordinal)) continue;
            var value = key == "openid.mode" ? "check_authentication" : query[key] ?? string.Empty;
            form.Add(new KeyValuePair<string, string>(key, value));
        }

        try
        {
            using var response = await http.PostAsync(Endpoint, new FormUrlEncodedContent(form), ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode || !body.Contains("is_valid:true", StringComparison.Ordinal))
                return (null, "Steam couldn't verify the sign-in. Try again.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return (null, $"Couldn't verify the sign-in with Steam: {ex.Message}");
        }

        return (match.Groups[1].Value, null);
    }

    private static async Task RespondAsync(HttpListenerResponse response, bool success, string? error)
    {
        var title = success ? "You're signed in" : "Sign-in failed";
        var message = success
            ? "You can close this tab and go back to Stackd."
            : WebUtility.HtmlEncode(error ?? "Something went wrong.");
        var color = success ? "#ffde59" : "#ef6b7b";

        var html = $$"""
            <!doctype html><html><head><meta charset="utf-8"><title>Stackd</title>
            <style>
              body{margin:0;height:100vh;display:grid;place-items:center;background:#0a0d17;color:#eceef5;font-family:'Segoe UI',sans-serif}
              main{text-align:center;max-width:420px;padding:32px}
              h1{margin:0 0 12px;color:{{color}};font-size:28px}
              p{margin:0;color:#a4a9bd;font-size:16px;line-height:1.5}
            </style></head>
            <body><main><h1>{{title}}</h1><p>{{message}}</p></main></body></html>
            """;

        var bytes = Encoding.UTF8.GetBytes(html);
        response.StatusCode = 200;
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = bytes.Length;
        await response.OutputStream.WriteAsync(bytes);
        response.Close();
    }

    private static int GetFreePort()
    {
        var tcp = new TcpListener(IPAddress.Loopback, 0);
        tcp.Start();
        var port = ((IPEndPoint)tcp.LocalEndpoint).Port;
        tcp.Stop();
        return port;
    }
}
