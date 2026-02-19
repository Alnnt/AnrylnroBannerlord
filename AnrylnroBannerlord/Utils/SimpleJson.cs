using AnrylnroBannerlord.Network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AnrylnroBannerlord.Utils
{
    internal static class SimpleJson
    {
        public static string SerializePlayers(List<PlayerSnapshot> players)
        {
            var sb = new StringBuilder();
            sb.Append("[");

            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];

                sb.Append("{");
                AppendString(sb, "Name", p.Name);
                sb.Append(",");
                AppendNumber(sb, "Kill", p.Kill);
                sb.Append(",");
                AppendNumber(sb, "Death", p.Death);
                sb.Append(",");
                AppendNumber(sb, "Score", p.Score);
                sb.Append(",");
                AppendString(sb, "Team", p.Team);
                sb.Append("}");

                if (i < players.Count - 1)
                    sb.Append(",");
            }

            sb.Append("]");
            return sb.ToString();
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append("\"");
            sb.Append(key);
            sb.Append("\":\"");
            sb.Append(Escape(value));
            sb.Append("\"");
        }

        private static void AppendNumber(StringBuilder sb, string key, int value)
        {
            sb.Append("\"");
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value);
        }

        private static string Escape(string s)
        {
            if (string.IsNullOrEmpty(s))
                return "";

            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
