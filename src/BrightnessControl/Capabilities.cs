using System;
using System.Collections.Generic;
using System.Globalization;

namespace BrightnessControl
{
    // A monitor's MCCS capabilities string, e.g.
    //   (prot(monitor)type(LCD)model(VA2759SMH)cmds(01 02 03)vcp(02 10 12 14(01 05 08) 60(11 0F))mccs_ver(2.2))
    // Only features listed under vcp(...) are offered: monitors often answer reads for codes they don't support.
    class Capabilities
    {
        // VCP code -> allowed values. An empty list means a continuous control (0..max).
        public readonly Dictionary<byte, List<byte>> Vcp = new Dictionary<byte, List<byte>>();
        public string Model;

        public bool Has(byte code) { return Vcp.ContainsKey(code); }

        public List<byte> Values(byte code)
        {
            List<byte> v;
            return Vcp.TryGetValue(code, out v) ? v : new List<byte>();
        }

        // Tolerates the sloppy strings real monitors send: spaces inside brackets ("60( 11 12 )"),
        // tags run together ("MStarcmds("), lowercase hex, and nested lists.
        public static Capabilities Parse(string s)
        {
            var caps = new Capabilities();
            if (string.IsNullOrEmpty(s)) return caps;
            caps.Model = Section(s, "model");
            string vcp = Section(s, "vcp");
            if (vcp == null) return caps;

            int i = 0;
            while (i < vcp.Length)
            {
                byte code;
                if (!ReadHex(vcp, ref i, out code)) { i++; continue; }
                var values = new List<byte>();
                int j = i;
                while (j < vcp.Length && vcp[j] == ' ') j++;
                if (j < vcp.Length && vcp[j] == '(')
                {
                    int end = Close(vcp, j);
                    string inner = vcp.Substring(j + 1, end - j - 1);
                    int k = 0, depth = 0;
                    while (k < inner.Length)
                    {
                        // Only take the top level of a nested list.
                        if (inner[k] == '(') { depth++; k++; continue; }
                        if (inner[k] == ')') { depth--; k++; continue; }
                        byte v;
                        if (depth == 0 && ReadHex(inner, ref k, out v)) values.Add(v);
                        else k++;
                    }
                    i = end + 1;
                }
                caps.Vcp[code] = values;
            }
            return caps;
        }

        // Contents of "name(...)", matched case-insensitively.
        static string Section(string s, string name)
        {
            int at = s.IndexOf(name + "(", StringComparison.OrdinalIgnoreCase);
            while (at >= 0)
            {
                // Skip matches that are the tail of a longer tag, like "vcpname(" when looking for "name(".
                if (at == 0 || !char.IsLetter(s[at - 1]))
                {
                    int open = at + name.Length;
                    return s.Substring(open + 1, Close(s, open) - open - 1);
                }
                at = s.IndexOf(name + "(", at + 1, StringComparison.OrdinalIgnoreCase);
            }
            return null;
        }

        static int Close(string s, int open)
        {
            int depth = 0;
            for (int i = open; i < s.Length; i++)
            {
                if (s[i] == '(') depth++;
                else if (s[i] == ')' && --depth == 0) return i;
            }
            return s.Length;   // unbalanced: take the rest
        }

        static bool ReadHex(string s, ref int i, out byte value)
        {
            value = 0;
            while (i < s.Length && s[i] == ' ') i++;
            if (i + 1 >= s.Length || !IsHex(s[i]) || !IsHex(s[i + 1])) return false;
            if (i + 2 < s.Length && IsHex(s[i + 2])) return false;   // not a 2-digit token
            value = byte.Parse(s.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            i += 2;
            return true;
        }

        static bool IsHex(char c) { return Uri.IsHexDigit(c); }
    }
}
