using System.Reflection;
using System.Text;

// Emits I{Service} interfaces (Task-returning public instance methods) from the compiled services.
var asm = typeof(Vernuko.Application.Services.CustomerService).Assembly;
string[] names = ["AuthService", "UserAdminService", "CustomerService", "CatalogService", "AppointmentService", "PackageService", "PaymentService",
    "ReportService", "AuditService", "SettingsService", "MessagingService", "CommissionService", "InventoryService", "WaitlistService",
    "CustomerRecordsService", "PrivateNotesService", "AutoMessagingService", "BackupService", "LicenseService"];
var ns = new SortedSet<string>(StringComparer.Ordinal);
var nctx = new NullabilityInfoContext();
var body = new StringBuilder();

Queue<string?>? tupleNames = null;
string Fmt(Type t, NullabilityInfo? n)
{
    if (t.IsGenericType && t.FullName!.StartsWith("System.ValueTuple`", StringComparison.Ordinal))
    {
        var els = t.GetGenericArguments();
        var labels = els.Select(_ => tupleNames is { Count: > 0 } q ? q.Dequeue() : null).ToList();
        return "(" + string.Join(", ", els.Select((e, i) => Fmt(e, n?.GenericTypeArguments.ElementAtOrDefault(i)) + (labels[i] is { } l ? " " + l : ""))) + ")";
    }
    if (Nullable.GetUnderlyingType(t) is { } u) return Fmt(u, n?.GenericTypeArguments.FirstOrDefault()) + "?";
    var nullable = n is { ReadState: NullabilityState.Nullable } && !t.IsValueType ? "?" : "";
    if (t.IsArray) return Fmt(t.GetElementType()!, n?.ElementType) + "[]" + nullable;
    string? alias = Type.GetTypeCode(t) switch
    {
        TypeCode.Boolean => "bool", TypeCode.Int32 => "int", TypeCode.Int64 => "long", TypeCode.String => "string", TypeCode.Byte => "byte",
        TypeCode.Decimal => "decimal", TypeCode.Double => "double", _ => null,
    };
    if (t == typeof(object)) alias = "object";
    if (alias is not null && !t.IsEnum) return alias + nullable;
    if (t.Namespace is { } space) ns.Add(space);
    var name = t.Name;
    if (t.IsGenericType)
    {
        name = name[..name.IndexOf('`')];
        var args = t.GetGenericArguments();
        var parts = args.Select((a, i) => Fmt(a, n is not null && i < n.GenericTypeArguments.Length ? n.GenericTypeArguments[i] : null));
        name += "<" + string.Join(", ", parts) + ">";
    }
    if (t.IsNested) name = t.DeclaringType!.Name + "." + name;
    return name + nullable;
}

string Param(ParameterInfo p)
{
    tupleNames = new Queue<string?>(p.GetCustomAttribute<System.Runtime.CompilerServices.TupleElementNamesAttribute>()?.TransformNames ?? []);
    var text = Fmt(p.ParameterType, nctx.Create(p)) + " " + p.Name + (p.HasDefaultValue ? " = " + Default(p) : "");
    tupleNames = null;
    return text;
}

string Default(ParameterInfo p)
{
    var v = p.DefaultValue;
    if (v is null) return p.ParameterType.IsValueType && Nullable.GetUnderlyingType(p.ParameterType) is null ? "default" : "null";
    if (v is bool b) return b ? "true" : "false";
    if (v is string s) return "\"" + s.Replace("\"", "\\\"") + "\"";
    var pt = Nullable.GetUnderlyingType(p.ParameterType) ?? p.ParameterType;
    if (pt.IsEnum) return pt.Name + "." + Enum.GetName(pt, v);
    return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)!;
}

foreach (var name in names)
{
    var type = asm.GetTypes().Single(t => t.Name == name && t.Namespace == "Vernuko.Application.Services");
    body.AppendLine($"/// <summary>Remotable contract of <see cref=\"{name}\"/> (generated from its public async methods).</summary>");
    body.AppendLine($"public partial interface I{name}");
    body.AppendLine("{");
    var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
        .Where(m => !m.IsSpecialName && typeof(Task).IsAssignableFrom(m.ReturnType)).OrderBy(m => m.MetadataToken).ToList();
    var first = true;
    foreach (var m in methods)
    {
        if (!first) body.AppendLine();
        first = false;
        var ret = Fmt(m.ReturnType, nctx.Create(m.ReturnParameter));
        var ps = m.GetParameters().Select(Param);
        body.AppendLine($"    {ret} {m.Name}({string.Join(", ", ps)});");
    }
    body.AppendLine("}");
    body.AppendLine();
}

var sb = new StringBuilder();
foreach (var n in ns.Where(n => n != "Vernuko.Application.Services" && n != "System" && n != "System.Threading.Tasks" && n != "System.Collections.Generic" && n != "System.Threading"))
    sb.AppendLine($"using {n};");
sb.AppendLine();
sb.Insert(0, "// Generated from the compiled services. Regenerate with the interface generator (docs/MULTI_PC.md) when a service changes;\n// ServiceContractTests fails if a public async service method is missing here.\n");
sb.AppendLine("namespace Vernuko.Application.Services;");
sb.AppendLine();
sb.Append(body.ToString().TrimEnd()).AppendLine();
File.WriteAllText(args[0], sb.ToString());
Console.WriteLine($"{names.Length} interfaces written");
