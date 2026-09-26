using System.Reflection;
using System.Text;
using OpenLogViewer.Engine.Host.Protocol;

string outputPath = args.Length > 0
    ? args[0]
    : throw new ArgumentException("Usage: ProtocolGen <output .ts path>");

var emitted = new List<Type>();
var visited = new HashSet<Type>();

foreach (var (_, request, result) in Commands.All)
{
    Visit(request);
    Visit(result);
}
Visit(typeof(HelloPayload));
Visit(typeof(ErrorDto));

var sb = new StringBuilder();

foreach (var type in emitted)
{
    if (type == typeof(EmptyResult))
        continue;

    sb.AppendLine($"export interface {TsName(type)} {{");
    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        sb.AppendLine($"  {CamelCase(prop.Name)}: {TsType(prop.PropertyType)};");
    sb.AppendLine("}");
    sb.AppendLine();
}

sb.AppendLine("export interface RequestPayloads {");
foreach (var (name, request, _) in Commands.All)
    sb.AppendLine($"  {name}: {TsType(request)};");
sb.AppendLine("}");
sb.AppendLine();
sb.AppendLine("export interface ResultTypes {");
foreach (var (name, _, result) in Commands.All)
    sb.AppendLine($"  {name}: {TsType(result)};");
sb.AppendLine("}");
sb.AppendLine();
sb.AppendLine("export type Command = keyof RequestPayloads;");

File.WriteAllText(outputPath, sb.ToString());
Console.WriteLine($"wrote {Path.GetFullPath(outputPath)} ({emitted.Count} types, {Commands.All.Length} commands)");

void Visit(Type type)
{
    type = Unwrap(type);
    if (!IsContractRecord(type) || !visited.Add(type))
        return;
    foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        Visit(prop.PropertyType);
    emitted.Add(type);
}

static Type Unwrap(Type type)
{
    type = Nullable.GetUnderlyingType(type) ?? type;
    if (type.IsArray)
        return Unwrap(type.GetElementType()!);
    if (type.IsGenericType)
    {
        var def = type.GetGenericTypeDefinition();
        if (def == typeof(IReadOnlyDictionary<,>) || def == typeof(Dictionary<,>))
            return Unwrap(type.GetGenericArguments()[1]);
        if (def == typeof(IReadOnlyList<>) || def == typeof(List<>))
            return Unwrap(type.GetGenericArguments()[0]);
    }
    return type;
}

static bool IsContractRecord(Type type)
    => type.Namespace == typeof(Commands).Namespace && !type.IsEnum && !type.IsAbstract && type.IsClass;

static string TsName(Type type) => type.Name;

static string TsType(Type type)
{
    var nullable = Nullable.GetUnderlyingType(type);
    if (nullable is not null)
        return $"({TsType(nullable)} | null)";
    if (type.IsArray)
        return $"{TsType(type.GetElementType()!)}[]";
    if (type.IsGenericType)
    {
        var def = type.GetGenericTypeDefinition();
        var genericArgs = type.GetGenericArguments();
        if (def == typeof(IReadOnlyDictionary<,>) || def == typeof(Dictionary<,>))
            return $"Record<{TsType(genericArgs[0])}, {TsType(genericArgs[1])}>";
        if (def == typeof(IReadOnlyList<>) || def == typeof(List<>))
            return $"{TsType(genericArgs[0])}[]";
    }
    if (type == typeof(string)) return "string";
    if (type == typeof(bool)) return "boolean";
    if (type == typeof(int) || type == typeof(long) || type == typeof(short) ||
        type == typeof(float) || type == typeof(double) || type == typeof(decimal) ||
        type == typeof(byte))
        return "number";
    if (type == typeof(EmptyResult)) return "Record<string, never>";
    if (IsContractRecord(type)) return TsName(type);
    throw new NotSupportedException($"No TypeScript mapping for {type}. Add one to ProtocolGen.");
}

static string CamelCase(string name)
    => char.ToLowerInvariant(name[0]) + name[1..];
