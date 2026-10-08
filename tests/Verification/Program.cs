using System.Globalization;
using Mono.Cecil;
using Mono.Cecil.Cil;

int checks = 0;
void Check(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    checks++;
}
void Reject(string text) => Check(!TrainerNumbers.TryAmount(text, out _), "Accepted invalid amount: " + text);
CultureInfo originalCulture = CultureInfo.CurrentCulture;
try
{
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
    foreach (string text in new[] { "", " ", "-1", "NaN", "Infinity", "-Infinity", "1e999", "1,000", "abc" }) Reject(text);
    foreach (var sample in new[] { ("0", 0d), ("1000", 1000d), ("1e6", 1000000d), (" 1.5 ", 1.5d) })
        Check(TrainerNumbers.TryAmount(sample.Item1, out var amount) && amount == sample.Item2, "Valid amount failed: " + sample.Item1);
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    Check(TrainerNumbers.TryAmount("1,5", out var comma) && comma == 1.5, "Decimal comma failed.");
    Check(TrainerNumbers.TryAmount("1.5", out var dot) && dot == 1.5, "Invariant decimal failed.");
    Check(TrainerNumbers.TryAmount("1e6", out var exponential) && exponential == 1000000, "Localized exponent failed.");
    Check(TrainerNumbers.TryBalance(20, 5, true, out var added) && added == 25, "Add failed.");
    Check(TrainerNumbers.TryBalance(20, 5, false, out var set) && set == 5, "Set failed.");
    Check(TrainerNumbers.TryBalance(20, 0, false, out var zero) && zero == 0, "Set zero failed.");
    Check(!TrainerNumbers.TryBalance(double.MaxValue, double.MaxValue, true, out _), "Overflow was accepted.");
    Check(!TrainerNumbers.TryBalance(10, -1, true, out _), "Negative change was accepted.");
    Check(!TrainerNumbers.TryBalance(double.NaN, 1, true, out _), "Invalid current balance was accepted.");
    Check(!TrainerNumbers.TryBalance(10, double.PositiveInfinity, false, out _), "Infinite set was accepted.");
    Check(TrainerNumbers.ClampSpeed(float.NaN) == 1, "NaN speed did not reset.");
    Check(TrainerNumbers.ClampSpeed(float.PositiveInfinity) == 1, "Infinite speed did not reset.");
    Check(Math.Abs(TrainerNumbers.ClampSpeed(-1) - 0.1f) < 0.0001f, "Minimum speed failed.");
    Check(TrainerNumbers.ClampSpeed(100) == 50, "Maximum speed failed.");
    Check(TrainerNumbers.ClampSpeed(5) == 5, "Normal speed changed.");
    Check(TrainerNumbers.ClampScale(float.NaN) == 1, "NaN UI scale failed.");
    Check(TrainerNumbers.ClampScale(3) == 2, "Maximum UI scale failed.");
    Check(TrainerNumbers.ClampScale(0) == 0.75f, "Minimum UI scale failed.");
}
finally { CultureInfo.CurrentCulture = originalCulture; }
Console.WriteLine($"PASS: {checks} numeric boundary and locale checks.");

if (args.Length == 0) return;
string managed = Path.GetFullPath(args[0]);
using var game = ModuleDefinition.ReadModule(Path.Combine(managed, "Assembly-CSharp.dll"));
TypeDefinition Type(string name)
{
    var split = name.Split('/');
    var type = game.Types.Single(x => x.FullName == split[0]);
    foreach (string nested in split.Skip(1)) type = type.NestedTypes.Single(x => x.Name == nested);
    return type;
}
MethodDefinition Method(string type, string name, string returns, bool isStatic, params string[] parameters)
{
    var methods = Type(type).Methods.Where(x => x.Name == name).ToArray();
    Check(methods.Length == 1, $"Missing or overloaded game hook: {type}.{name}");
    var method = methods[0];
    Check(method.ReturnType.FullName == returns && method.IsStatic == isStatic &&
        method.Parameters.Select(x => x.ParameterType.FullName).SequenceEqual(parameters), $"Signature changed: {type}.{name}");
    return method;
}
void Field(string type, string name, string valueType)
{
    Check(Type(type).Fields.Any(x => x.Name == name && x.FieldType.FullName == valueType), $"Field changed: {type}.{name}");
}
foreach (string type in new[] { "PingSimulation", "BugSimulation", "UptimeSimulation", "SequelDevelopSimulation" })
    Method(type, "OnUpdateSimulation", "System.Void", false, "System.Single");
Method("SequelDevelopSimulation", "Registered", "System.Void", false, "System.Nullable`1<UIRegistry>");
Method("SequelDevelopSimulation", "Unregistered", "System.Void", false);
foreach (string method in new[] { "UnlockAllUpgrades", "UnlockAllResearch", "UnlockAllDatacenters" })
    Method("GameController", method, "System.Void", false);
Method("GameTimer", "get_GameSpeedMultiplier", "System.Single", false);
Method("GameTimer", "set_GameSpeedMultiplier", "System.Void", false, "System.Single");
Method("SequelDevelopSimulation", "FinishDevelopment", "System.Void", false);
Check(Type("SequelDevelopSimulation").Methods.Single(x => x.Name == "OnUpdateSimulation").Body.Instructions.Any(
    x => x.Operand is MethodReference method && method.Name == "FinishDevelopment"), "Development no longer completes through the simulation.");
Method("DatabaseCommands/DatacenterCommands", "Restore", "System.Void", false, "Datacenter");
Field("Database", "Disposed", "System.Boolean");
Field("Database", "State", "DatabaseState");
foreach (string name in new[] { "Money", "Players" }) Field("DatabaseState/ResourceState", name, "R3.ReactiveProperty`1<System.Double>");
foreach (string name in new[] { "Ping", "Bugs", "Uptime", "Load" }) Field("DatabaseState/ResourceState", name, "R3.ReactiveProperty`1<System.Single>");
foreach (string name in new[] { "Fans", "Data" }) Field("DatabaseState/PrestigeState", name, "R3.ReactiveProperty`1<System.Double>");
Field("DatabaseState/StudioState", "Paused", "R3.ReactiveProperty`1<System.Boolean>");
foreach (string name in new[] { "GameDesign", "Art", "Netcode", "Marketing", "Qa" })
    Field("DatabaseState/SequelState/SequelProgressState", name, "R3.ReactiveProperty`1<System.Single>");
Field("DatabaseState/SequelState/SequelProgressState", "FactorRange", "R3.ReactiveProperty`1<UnityEngine.Vector2>");
Console.WriteLine($"PASS: {checks} total checks, including installed game metadata.");
if (args.Length > 1)
{
    using var trainer = ModuleDefinition.ReadModule(Path.GetFullPath(args[1]));
    var plugin = trainer.Types.Single(x => x.Name == "Plugin");
    Check(plugin.BaseType.FullName == "BepInEx.Unity.Mono.BaseUnityPlugin", "Wrong BepInEx plugin base type.");
    Check(plugin.CustomAttributes.Any(x => x.AttributeType.FullName == "BepInEx.BepInPlugin"), "Missing plugin metadata.");
    Check(plugin.Methods.Any(x => x.Name == "OnDestroy" && x.Body.Instructions.Any(
        i => i.Operand is MethodReference m && m.Name == "UnpatchSelf")), "Plugin does not remove its Harmony patches.");
    Console.WriteLine($"PASS: {checks} total checks, including compiled plugin metadata.");
}
