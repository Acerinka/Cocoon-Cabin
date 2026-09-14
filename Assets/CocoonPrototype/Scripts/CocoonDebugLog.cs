using UnityEngine;

namespace CocoonPrototype
{
    public static class CocoonDebugLog
    {
        private const string Prefix = "[Cocoon]";

        public static bool Enabled { get; set; } = true;
        public static bool VerboseEnabled { get; set; } = false;

        static CocoonDebugLog()
        {
            Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            Application.SetStackTraceLogType(LogType.Warning, StackTraceLogType.ScriptOnly);
        }

        public static void Info(string category, string message, Object context = null)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.Log(Format(category, message), context);
        }

        public static void Warn(string category, string message, Object context = null)
        {
            if (!Enabled)
            {
                return;
            }

            Debug.LogWarning(Format(category, message), context);
        }

        public static void Verbose(string category, string message, Object context = null)
        {
            if (!Enabled || !VerboseEnabled)
            {
                return;
            }

            Debug.Log(Format(category, message), context);
        }

        private static string Format(string category, string message)
        {
            return Prefix + "[" + category + "][" + Time.time.ToString("0.00") + "s] " + message;
        }
    }
}
