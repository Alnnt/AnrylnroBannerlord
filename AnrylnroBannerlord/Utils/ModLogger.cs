using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TaleWorlds.Engine;
using TaleWorlds.Library;

namespace AnrylnroBannerlord.Utils
{
    internal static class ModLogger
    {
        public static void Log(string message, int logLevel = 0, Debug.DebugColor color = Debug.DebugColor.Green)
        {
            MBDebug.Print("Anrylnro Bannerlord :: " + message, logLevel, color);
        }

        public static void Warn(string message)
        {
            MBDebug.Print("Anrylnro Bannerlord :: " + message, 0, Debug.DebugColor.Yellow);
        }

        public static void Error(string message)
        {
            MBDebug.Print("Anrylnro Bannerlord :: " + message, 0, Debug.DebugColor.Red);
        }
    }
}
