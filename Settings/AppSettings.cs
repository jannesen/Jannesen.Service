using System;
using System.Configuration;
using System.IO;

namespace Jannesen.Service.Settings
{
    public static class AppSettings
    {
        public static string ProgramExe => System.Reflection.Assembly.GetEntryAssembly().Location;
        public static string ProgramDirectory => Path.GetDirectoryName(ProgramExe);

        public static string GetSetting(string name)
        {
            return GetSetting(name, null) ?? throw new AppSettingException("Missing appSetting '" + name + "'.");
        }
        public static string GetSetting(string name, string defaultValue)
        {
            string value = ConfigurationManager.AppSettings[name];

            if (value == null)
                return defaultValue;

            try
            {
                int pbegin = 0;
                while (pbegin < value.Length && (pbegin = value.IndexOf("${", pbegin, StringComparison.Ordinal)) >= 0)
                {
                    var pend = value.IndexOf('}', pbegin + 1);
                    if (pend < 0)
                    {
                        throw new AppSettingException("Missing '}' in ${<name>}.");
                    }
                    var expname = value.Substring(pbegin + 2, pend - pbegin - 2);
                    var expvalue = GetExpandSetting(expname)
                                    ?? throw new AppSettingException("Can't find '" + expname + "' in appSettings or environment.");
                    value = string.Concat(value.Substring(0, pbegin), expvalue, value.Substring(pend + 1));
                    pbegin = pend + 1;
                }
            }
            catch (Exception err)
            {
                throw new AppSettingException("Failed to expand appSetting '" + name + "'.", err);
            }

            return value;
        }

        public static string GetExpandSetting(string name)
        {
            string value;

            if ((value = GetSetting(name, null)) != null)
            {
                return value;
            }

            if ((value = Environment.GetEnvironmentVariable(name)) != null)
            {
                return value;
            }

            switch (name)
            {
                case "ProgramDirectory": return ProgramDirectory;
            }

            return null;
        }
    }
}
