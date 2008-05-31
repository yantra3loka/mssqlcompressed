/*
	Copyright 2008 Clay Lenhart <clay@lenharts.net>


	This file is part of MSSQL Compressed Backup.

    MSSQL Compressed Backup is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    Foobar is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with Foobar.  If not, see <http://www.gnu.org/licenses/>.
*/



using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

using VirtualBackupDevice;

namespace MSSQLBackupPipe
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {


                Dictionary<string, Type> pipelineComponents = LoadPipelineComponents();


                bool isBackup;
                string deviceName = Guid.NewGuid().ToString();
                string filename;
                string databaseName;
                List<ConfigPair> pipelineConfig = ParseArgs(args, pipelineComponents, out databaseName, out filename, out isBackup);

                //NotifyWhenReady notifyWhenReady = new NotifyWhenReady(deviceName, isBackup);
                using (DeviceThread device = new DeviceThread())
                {
                    using (SqlThread sql = new SqlThread())
                    {
                        sql.PreConnect(databaseName, deviceName, isBackup);
                        device.PreConnect(isBackup, deviceName, filename, pipelineConfig);
                        device.ConnectInAnoterThread();
                        sql.ConnectInAnoterThread();
                        Exception e = sql.WaitForCompletion();
                        if (e != null)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }

                        e = device.WaitForCompletion();
                        if (e != null)
                        {
                            Console.WriteLine(e.Message);
                            Console.WriteLine(e.StackTrace);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine(e.StackTrace);

                Exception ie = e;
                while (ie.InnerException != null)
                {
                    ie = ie.InnerException;
                }
                Console.WriteLine(ie.Message);



                PrintUsage();
                //             Console.WriteLine(e.StackTrace);


            }

        }




        //private class NotifyWhenReady : INotifyWhenReady
        //{
        //    private bool mIsBackup;
        //    private string mDeviceName;

        //    public NotifyWhenReady(string deviceName, bool isBackup)
        //    {
        //        mIsBackup = isBackup;
        //        mDeviceName = deviceName;
        //    }

        //    #region INotifyWhenReady Members

        //    public void Ready()
        //    {
        //        if (mIsBackup)
        //        {
        //            Console.WriteLine("Ready for backup command, like");
        //            Console.WriteLine(string.Format("BACKUP DATABASE [database] TO VIRTUAL_DEVICE='{0}';", mDeviceName));
        //        }
        //        else
        //        {
        //            Console.WriteLine("Ready for restore command, like");
        //            Console.WriteLine(string.Format("RESTORE DATABASE [database] FROM VIRTUAL_DEVICE='{0}';", mDeviceName));
        //        }
        //    }

        //    #endregion
        //}



        private static Dictionary<string, Type> LoadPipelineComponents()
        {
            Dictionary<string, Type> result = new Dictionary<string, Type>();

            DirectoryInfo binDir = new FileInfo(Assembly.GetEntryAssembly().Location).Directory;

            foreach (FileInfo file in binDir.GetFiles("*.dll"))
            {

                Assembly dll = null;
                try
                {
                    dll = Assembly.LoadFrom(file.FullName);
                }
                catch
                { }
                if (dll != null)
                {
                    FindPlugins(dll, result);
                }
            }

            return result;
        }



        private static void FindPlugins(Assembly dll, Dictionary<string, Type> result)
        {
            foreach (Type t in dll.GetTypes())
            {
                try
                {
                    if (t.IsPublic)
                    {
                        if (!((t.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract))
                        {
                            if (t.GetInterface("IBackupTransformer") != null)
                            {
                                IBackupTransformer test = t.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupTransformer;
                                if (test != null)
                                {
                                    string name = test.GetName().ToLowerInvariant();
                                    if (result.ContainsKey(name))
                                    {
                                        throw new ArgumentException(string.Format("plugin found twice: {0}", name));
                                    }
                                    result.Add(name, t);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
        }



        private static List<ConfigPair> ParseArgs(string[] args, Dictionary<string, Type> pipelineComponents, out string databaseName, out string filename, out bool isBackup)
        {
            bool databaseNameFound = false;
            bool pipelineFound = false;
            bool directionFound = false;

            List<ConfigPair> pipeline = null;
            isBackup = true;
            databaseName = null;
            filename = null;

            int i = 0;
            while (i < args.Length)
            {
                string arg = args[i];
                switch (arg)
                {
                    case "--db":
                        if (databaseNameFound)
                        {
                            throw new ArgumentException("The database name cannot be set twice.");
                        }

                        i++;
                        if (i >= args.Length)
                        {
                            throw new ArgumentException("Please provide a database name after the --db switch.");
                        }

                        databaseName = args[i].Trim();
                        if (databaseName.Length >= 2 && databaseName[0] == '[' && databaseName[databaseName.Length - 1] == ']')
                        {
                            databaseName = databaseName.Substring(1, databaseName.Length - 2);
                        }

                        databaseNameFound = true;
                        break;

                    case "-p":
                        if (pipelineFound)
                        {
                            throw new ArgumentException("The pipeline cannot be set twice.");
                        }

                        i++;
                        if (i >= args.Length)
                        {
                            pipeline = new List<ConfigPair>(0);
                        }
                        else
                        {
                            pipeline = BuildPipelineFromString(args[i], pipelineComponents, out filename);
                        }


                        pipelineFound = true;
                        break;

                    case "-b":
                        if (directionFound)
                        {
                            throw new ArgumentException("The direction (backup or restore) cannot be set twice.");
                        }

                        isBackup = true;

                        directionFound = true;
                        break;

                    case "-r":
                        if (directionFound)
                        {
                            throw new ArgumentException("The direction (backup or restore) cannot be set twice.");
                        }

                        isBackup = false;

                        directionFound = true;
                        break;

                    default:
                        throw new ArgumentException(string.Format("Unknown switch: {0}.", args[i]));

                }

                i++;
            }

            if (!databaseNameFound)
            {
                throw new ArgumentException("Required --db switch, database name, is missing.");
            }

            if (!pipelineFound)
            {
                throw new ArgumentException("Required -p switch, pipeline, is missing.");
            }

            if (!directionFound)
            {
                throw new ArgumentException("You must provide either a -b or -r switch to indicate if this is for backup or restore.");
            }

            return pipeline;
        }



        private static List<ConfigPair> BuildPipelineFromString(string pipelineString, Dictionary<string, Type> pipelineComponents, out string filename)
        {
            string[] components = pipelineString.Split('|');

            for (int i = 0; i < components.Length; i++)
            {
                components[i] = components[i].Trim();
            }

            List<ConfigPair> results = new List<ConfigPair>(components.Length);

            for (int i = 0; i < components.Length - 1; i++)
            {
                string componentString = components[i];

                ConfigPair config = new ConfigPair();

                string componentName;
                string configString;

                int pPos = componentString.IndexOf('(');
                if (pPos < 0)
                {
                    componentName = componentString;
                    configString = "";
                }
                else
                {
                    componentName = componentString.Substring(0, pPos).Trim();

                    if (componentString.Substring(componentString.Length - 1, 1) != ")")
                    {
                        throw new ArgumentException(string.Format("Invalid pipeline.  The closing parenthesis not found: {0}", componentString));
                    }

                    configString = componentString.Substring(pPos + 1, componentString.Length - pPos - 2);
                }

                Type foundType;
                if (pipelineComponents.TryGetValue(componentName.ToLowerInvariant(), out foundType))
                {
                    config.ConfigString = configString;
                    config.TransformationType = foundType;
                }
                else
                {
                    throw new ArgumentException(string.Format("Plugin not found: {0}", componentName));
                }

                results.Add(config);

            }

            // the last entry must be the destination file in the format: file://c:\etc

            string destFileName = components[components.Length - 1];
            Uri destUri = new Uri(destFileName);
            if (!destUri.IsFile)
            {
                throw new ArgumentException(string.Format("The destination must be a file starting with file://: {0}", destFileName));
            }

            filename = destUri.LocalPath;



            return results;
        }


        private static void PrintUsage()
        {
            //TODO: PrintUsage
        }
    }
}
