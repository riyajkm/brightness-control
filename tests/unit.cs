#:property TargetFramework=net10.0-windows10.0.19041.0
#:property PublishAot=false
#:property BuiltInComInteropSupport=true
#:property UseWindowsForms=true
#:property Nullable=disable
#:package System.Management@10.0.0
// Unit tests for the capabilities parser and brightness-matching curves, against the built app.
// Usage (from the repo root, after build.ps1):  dotnet run --file tests/unit.cs -- bin/BrightnessControl.dll
using System; using System.Collections; using System.Collections.Generic; using System.Linq; using System.Reflection;
var asm = Assembly.LoadFrom(System.IO.Path.GetFullPath(args.Length > 0 ? args[0] : "bin/BrightnessControl.dll"));
var C = asm.GetType("BrightnessControl.Capabilities");
int fails = 0;
void Check(string what, bool ok) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) fails++; }
string Dump(object caps) {
  var vcp = (IDictionary)C.GetField("Vcp").GetValue(caps);
  var parts = new List<string>();
  foreach (DictionaryEntry e in vcp) { var l = (List<byte>)e.Value; parts.Add(((byte)e.Key).ToString("X2") + (l.Count > 0 ? "(" + string.Join(" ", l.Select(b => b.ToString("X2"))) + ")" : "")); }
  return string.Join(" ", parts);
}
object Parse(string s) => C.GetMethod("Parse").Invoke(null, new object[] { s });
bool Has(object c, byte code) => (bool)C.GetMethod("Has").Invoke(c, new object[] { code });
List<byte> Values(object c, byte code) => (List<byte>)C.GetMethod("Values").Invoke(c, new object[] { code });

var vs = Parse("(prot(monitor)type(LCD)model(VA2759SMH)cmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 0B 0C 10 12 14(01 08 06 05 04 0B) 16 18 1A 52 60(01 11) 62 6C 6E 70 87 8D(01 02) AC AE B2 B6 C6 C8 C9 CA(01 02) CC(02 03 04 0a 05 16 09 06 0C 07 0d 01) D6(01 04 05) DF E1(00 01))mswhql(1)asset_eep(40)mccs_ver(2.2))");
Console.WriteLine("ViewSonic: " + Dump(vs));
Check("VS model", (string)C.GetField("Model").GetValue(vs) == "VA2759SMH");
Check("VS contrast", Has(vs, 0x12));
Check("VS presets 01 08 06 05 04 0B", string.Join(" ", Values(vs, 0x14).Select(b => b.ToString("X2"))) == "01 08 06 05 04 0B");
Check("VS inputs 01 11", string.Join(" ", Values(vs, 0x60).Select(b => b.ToString("X2"))) == "01 11");
Check("VS lowercase hex in CC", Values(vs, 0xCC).Contains(0x0A) && Values(vs, 0xCC).Contains(0x0D));
Check("VS doesn't list FD", !Has(vs, 0xFD));

var msi = Parse("(prot(monitor)type(lcd)MStarcmds(01 02 03 07 0C E3 F3)vcp(02 04 05 08 10 12 14(05 08 0B 0C) 16 18 1A 52 60( 11 12 0F 10) AA(01 02) AC AE B2 B6 C6 C8 C9 D6( 05 ) DC(00 02 03 05 ) DF FD)mccs_ver(2.1)mswhql(1))");
Console.WriteLine("MSI: " + Dump(msi));
Check("MSI inputs with leading space", string.Join(" ", Values(msi, 0x60).Select(b => b.ToString("X2"))) == "11 12 0F 10");
Check("MSI D6( 05 )", string.Join(" ", Values(msi, 0xD6).Select(b => b.ToString("X2"))) == "05");
Check("MSI no volume 62", !Has(msi, 0x62));
Check("MSI no black level 6C", !Has(msi, 0x6C));
Check("MSI FD at end", Has(msi, 0xFD));
Check("MSI model absent", C.GetField("Model").GetValue(msi) == null);
Check("empty string", Dump(Parse("")) == "");
Check("garbage", Dump(Parse("vcp(10 12 1")) == "10 12");
Check("nested list", Dump(Parse("vcp(14(05 08(01 02) 0B) 10)")) == "14(05 08 0B) 10");

var M = asm.GetType("BrightnessControl.Matching");
List<int[]> Make(params int[][] p) => (List<int[]>)M.GetMethod("MakeCurve").Invoke(null, new object[] { p.ToList() });
int ToP(List<int[]> c, int l) => (int)M.GetMethod("ToPercent").Invoke(null, new object[] { c, l });
int ToL(List<int[]> c, int p) => (int)M.GetMethod("ToLevel").Invoke(null, new object[] { c, p });
var curve = Make(new[] { 30, 20 }, new[] { 80, 60 });
Check("curve at points", ToP(curve, 30) == 20 && ToP(curve, 80) == 60);
Check("curve between", ToP(curve, 55) == 40);
Check("curve extrapolate low clamps", ToP(curve, 0) == 0);
Check("curve extrapolate high", ToP(curve, 100) == 76);
Check("inverse", ToL(curve, 40) == 55);
Check("no curve = identity", ToP(null, 37) == 37 && ToL(null, 37) == 37);
var bad = Make(new[] { 80, 30 }, new[] { 30, 50 });  // user set bright lower than dim
Check("monotonic fix", bad[0][0] == 30 && bad[1][1] > bad[0][1]);
var flat = Make(new[] { 30, 50 }, new[] { 30, 50 });
Check("duplicate level fixed", flat[1][0] > flat[0][0]);
Console.WriteLine(fails == 0 ? "ALL PASSED" : fails + " FAILED"); return fails == 0 ? 0 : 1;

