using System.Reflection;
using System.Text.Json;
using Vernuko.Core;

namespace Vernuko.Remote;

/// <summary>Raised for calls that do not return a Result when the main computer cannot answer.</summary>
public sealed class RemoteCallException(Error error) : Exception(error.Message)
{
    public Error Error { get; } = error;
}

/// <summary>Carries one call to the main computer (HTTPS in the app, an in-memory host in tests).</summary>
public interface IRpcChannel
{
    /// <summary>Returns the reply body, or an error (network, session expired, refused).</summary>
    Task<(byte[]? Body, Error? Error)> CallAsync(string contract, string method, byte[] arguments, CancellationToken ct);
}

/// <summary>
/// Connected computer side: implements a service contract by sending every call to the main computer.
/// A call returning Result gets the failure as a Result (screens already show those); other calls throw
/// <see cref="RemoteCallException"/>, which the screens' error handling shows as a message.
/// </summary>
public class RpcProxy : DispatchProxy
{
    private static readonly MethodInfo CallGeneric = typeof(RpcProxy).GetMethod(nameof(CallAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private IRpcChannel _channel = null!;
    private string _contract = string.Empty;

    public static T Create<T>(IRpcChannel channel)
        where T : class
    {
        var proxy = Create<T, RpcProxy>();
        var p = (RpcProxy)(object)proxy;
        p._channel = channel;
        p._contract = typeof(T).Name;
        return proxy;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        ArgumentNullException.ThrowIfNull(targetMethod);
        if (!typeof(Task).IsAssignableFrom(targetMethod.ReturnType))
        {
            throw new NotSupportedException($"{_contract}.{targetMethod.Name} is not a remote call.");
        }

        args ??= [];
        var ct = CancellationToken.None;
        var payload = new List<(Type, object?)>();
        var parameters = targetMethod.GetParameters();
        for (var i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].ParameterType == typeof(CancellationToken))
            {
                ct = args[i] is CancellationToken token ? token : default;
            }
            else
            {
                payload.Add((parameters[i].ParameterType, args[i]));
            }
        }

        var body = RpcJson.WriteArguments(payload);
        var returnType = targetMethod.ReturnType;
        return returnType.IsGenericType
            ? CallGeneric.MakeGenericMethod(returnType.GetGenericArguments()[0]).Invoke(this, [targetMethod.Name, body, ct])
            : CallVoidAsync(targetMethod.Name, body, ct);
    }

    private async Task CallVoidAsync(string method, byte[] body, CancellationToken ct)
    {
        var (_, error) = await _channel.CallAsync(_contract, method, body, ct);
        if (error is not null)
        {
            throw new RemoteCallException(error);
        }
    }

    private async Task<T> CallAsync<T>(string method, byte[] body, CancellationToken ct)
    {
        var (reply, error) = await _channel.CallAsync(_contract, method, body, ct);
        if (error is not null)
        {
            return Failure<T>(error);
        }

        try
        {
            return JsonSerializer.Deserialize<T>(reply ?? [], RpcJson.Options)!;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return Failure<T>(new Error("remote.bad_reply", "Ana bilgisayarın yanıtı okunamadı. İki bilgisayarda da aynı Vernuko sürümü olmalı."));
        }
    }

    private static T Failure<T>(Error error)
    {
        if (typeof(T) == typeof(Result))
        {
            return (T)(object)Result.Failure(error);
        }

        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Result<>))
        {
            var failure = typeof(Result).GetMethods().Single(m => m.Name == nameof(Result.Failure) && m.IsGenericMethodDefinition)
                .MakeGenericMethod(typeof(T).GetGenericArguments()[0]);
            return (T)failure.Invoke(null, [error])!;
        }

        throw new RemoteCallException(error);
    }
}
