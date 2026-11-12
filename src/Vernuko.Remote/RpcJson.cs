using System.Text.Json;
using System.Text.Json.Serialization;
using Vernuko.Core;

namespace Vernuko.Remote;

/// <summary>JSON used between the main computer and connected computers (both run the same Vernuko version).</summary>
public static class RpcJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            IncludeFields = false,
            NumberHandling = JsonNumberHandling.Strict,
        };
        o.Converters.Add(new JsonStringEnumConverter());
        o.Converters.Add(new ResultConverterFactory());
        o.Converters.Add(new ReadOnlySetConverterFactory());
        o.MakeReadOnly(populateMissingResolver: true);
        return o;
    }

    /// <summary>Arguments in declaration order (CancellationToken excluded), each written with its declared type.</summary>
    public static byte[] WriteArguments(IReadOnlyList<(Type Type, object? Value)> arguments)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms))
        {
            w.WriteStartArray();
            foreach (var (type, value) in arguments)
            {
                JsonSerializer.Serialize(w, value, type, Options);
            }

            w.WriteEndArray();
        }

        return ms.ToArray();
    }
}

/// <summary>Result / Result&lt;T&gt; as {"ok":true,"value":…} or {"ok":false,"error":{"code":…,"message":…}}.</summary>
internal sealed class ResultConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) =>
        typeToConvert == typeof(Result) || (typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(Result<>));

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        typeToConvert == typeof(Result)
            ? new PlainResultConverter()
            : (JsonConverter)Activator.CreateInstance(typeof(ValueResultConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class PlainResultConverter : JsonConverter<Result>
    {
        public override Result Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var (ok, error, _) = ReadEnvelope<object>(ref reader, options, readValue: false);
            return ok ? Result.Success() : Result.Failure(error!);
        }

        public override void Write(Utf8JsonWriter writer, Result value, JsonSerializerOptions options) => WriteEnvelope<object>(writer, value, null, false, options);
    }

    private sealed class ValueResultConverter<T> : JsonConverter<Result<T>>
    {
        public override Result<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var (ok, error, value) = ReadEnvelope<T>(ref reader, options, readValue: true);
            return ok ? Result.Success(value!) : Result.Failure<T>(error!);
        }

        public override void Write(Utf8JsonWriter writer, Result<T> value, JsonSerializerOptions options) =>
            WriteEnvelope(writer, value, value.IsSuccess ? value.Value : default, true, options);
    }

    private static void WriteEnvelope<T>(Utf8JsonWriter writer, Result result, T? value, bool hasValue, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteBoolean("ok", result.IsSuccess);
        if (result.IsSuccess)
        {
            if (hasValue)
            {
                writer.WritePropertyName("value");
                JsonSerializer.Serialize(writer, value, options);
            }
        }
        else
        {
            writer.WriteStartObject("error");
            writer.WriteString("code", result.Error.Code);
            writer.WriteString("message", result.Error.Message);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    private static (bool Ok, Error? Error, T? Value) ReadEnvelope<T>(ref Utf8JsonReader reader, JsonSerializerOptions options, bool readValue)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException("Result object expected.");
        }

        bool? ok = null;
        Error? error = null;
        T? value = default;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            var name = reader.GetString();
            reader.Read();
            switch (name)
            {
                case "ok":
                    ok = reader.GetBoolean();
                    break;
                case "error":
                    string? code = null, message = null;
                    while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
                    {
                        var p = reader.GetString();
                        reader.Read();
                        if (p == "code")
                        {
                            code = reader.GetString();
                        }
                        else if (p == "message")
                        {
                            message = reader.GetString();
                        }
                        else
                        {
                            reader.Skip();
                        }
                    }

                    error = new Error(code ?? "remote.error", message ?? string.Empty);
                    break;
                case "value" when readValue:
                    value = JsonSerializer.Deserialize<T>(ref reader, options);
                    break;
                default:
                    reader.Skip();
                    break;
            }
        }

        if (ok is null || (ok == false && error is null))
        {
            throw new JsonException("Incomplete result.");
        }

        return (ok.Value, error, value);
    }
}

/// <summary>IReadOnlySet&lt;T&gt; (e.g. profile features) is read back as a HashSet.</summary>
internal sealed class ReadOnlySetConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert.IsGenericType && typeToConvert.GetGenericTypeDefinition() == typeof(IReadOnlySet<>);

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(SetConverter<>).MakeGenericType(typeToConvert.GetGenericArguments()[0]))!;

    private sealed class SetConverter<T> : JsonConverter<IReadOnlySet<T>>
    {
        public override IReadOnlySet<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            JsonSerializer.Deserialize<List<T>>(ref reader, options)?.ToHashSet() ?? [];

        public override void Write(Utf8JsonWriter writer, IReadOnlySet<T> value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, value.ToList(), options);
    }
}
