using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Vernuko.Core;

namespace Vernuko.Remote;

/// <summary>What the main computer sends back for one call.</summary>
public sealed record RpcReply(int Status, byte[] Body)
{
    public static RpcReply Fail(int status, Error error) =>
        new(status, JsonSerializer.SerializeToUtf8Bytes(new RpcError(error.Code, error.Message), RpcJson.Options));
}

public sealed record RpcError(string Code, string Message);

/// <summary>
/// Main computer side: runs one allowed contract method against a remote session's own services. Only methods of
/// <see cref="RpcContracts.All"/> are reachable; host-only calls are refused. Permission and license checks happen
/// inside the services exactly as for the main computer's own screens.
/// </summary>
public sealed class RpcDispatcher(ILogger<RpcDispatcher> logger)
{
    public async Task<RpcReply> InvokeAsync(string contract, string method, byte[] body, IServiceProvider sessionServices, CancellationToken ct)
    {
        var key = contract + "/" + method;
        if (!RpcContracts.Methods.TryGetValue(key, out var target))
        {
            return RpcReply.Fail(404, new Error("remote.unknown_call", "Bilinmeyen işlem."));
        }

        if (RpcContracts.HostOnly.Contains(key))
        {
            return RpcReply.Fail(403, RpcContracts.HostOnlyError);
        }

        var parameters = target.Method.GetParameters();
        var args = new object?[parameters.Length];
        try
        {
            using var doc = JsonDocument.Parse(body.Length == 0 ? "[]"u8.ToArray() : body);
            var values = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : [];
            var index = 0;
            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(CancellationToken))
                {
                    args[i] = ct;
                }
                else if (index < values.Count)
                {
                    args[i] = values[index++].Deserialize(p.ParameterType, RpcJson.Options);
                }
                else
                {
                    args[i] = p.HasDefaultValue ? p.DefaultValue : null;
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return RpcReply.Fail(400, new Error("remote.bad_request", "İstek okunamadı."));
        }

        var implementation = sessionServices.GetRequiredService(target.Contract);
        Task task;
        try
        {
            task = (Task)target.Method.Invoke(implementation, args)!;
            await task;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var inner = ex is TargetInvocationException { InnerException: { } i } ? i : ex;
            logger.LogError(inner, "Remote call {Call} failed", key);
            return RpcReply.Fail(500, new Error("remote.error", "Ana bilgisayarda beklenmeyen bir hata oluştu."));
        }

        var returnType = target.Method.ReturnType;
        if (!returnType.IsGenericType)
        {
            return new RpcReply(200, []);
        }

        var valueType = returnType.GetGenericArguments()[0];
        var value = returnType.GetProperty(nameof(Task<object>.Result))!.GetValue(task);
        return new RpcReply(200, JsonSerializer.SerializeToUtf8Bytes(value, valueType, RpcJson.Options));
    }
}
