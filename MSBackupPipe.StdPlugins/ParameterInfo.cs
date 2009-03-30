using System;
using System.Collections.Generic;
using System.Text;

namespace MSBackupPipe.StdPlugins
{
    public class ParameterInfo
    {
        public bool AllowMultipleValues { get; internal set; }
        public bool IsRequired { get; internal set; }

        internal static void ValidateParams(Dictionary<string, ParameterInfo> paramSchema, Dictionary<string, List<string>> config)
        {
            if (config.Comparer != StringComparer.InvariantCultureIgnoreCase)
            {
                throw new ArgumentException(string.Format("Programming error: The config dictionary must be initialized with StringComparer.InvariantCultureIgnoreCase."));
            }

            foreach (string optionName in config.Keys)
            {
                ParameterInfo paramInfo;
                if (!paramSchema.TryGetValue(optionName, out paramInfo))
                {
                    throw new ArgumentException(string.Format("The parameter, {0}, is not a valid option.", optionName));
                }
                else
                {
                    List<string> optionValues = config[optionName];

                    if (optionValues == null || optionValues.Count == 0)
                    {
                        throw new ArgumentException(string.Format("Programming error: The parameter, {0}, cannot be null or empty.", optionName));
                    }

                    if (!paramInfo.AllowMultipleValues)
                    {
                        if (optionValues.Count > 1)
                        {
                            throw new ArgumentException(string.Format("The parameter, {0}, must be specified only once.", optionName));
                        }
                    }
                }
            }

            foreach (string schemaParam in paramSchema.Keys)
            {
                if (paramSchema[schemaParam].IsRequired)
                {
                    if (!config.ContainsKey(schemaParam))
                    {
                        throw new ArgumentException(string.Format("The parameter, {0}, is required.", schemaParam));
                    }
                }
            }


        }
    }
}
