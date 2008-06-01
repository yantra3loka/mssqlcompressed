using System;
using System.Collections.Generic;
using System.Text;

namespace MSSQLBackupPipe.StdPlugins
{
    public static class ConfigUtil
    {
        public static Dictionary<string, string> ParseConfig(string s)
        {
            string[] pairs = s.Split(';');

            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (string pair in pairs)
            {
                string[] nameValue = pair.Split(new char[] {'='}, 2);
                string name = nameValue[0].Trim();
                string val = nameValue.Length > 1 ? nameValue[1].Trim() : null;

                name = name.Replace("\\s", ";");
                name = name.Replace("\\p", "|");
                name = name.Replace("\\\\", "\\");

                val = val.Replace("\\s", ";");
                val = val.Replace("\\p", "|");
                val = val.Replace("\\\\", "\\");


                result.Add(name, val);

            }

            return result;
        }
    }
}
