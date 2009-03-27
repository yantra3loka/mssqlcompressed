using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;

using MSBackupPipe.StdPlugins;
using MSBackupPipe.VirtualBackupDevice;

namespace MSSQLBackupPipe.Common
{
    public static class BackupPipeSystem
    {
        public static Dictionary<string, Type> LoadTransformComponents()
        {
            return LoadComponents("IBackupTransformer");
        }
        public static Dictionary<string, Type> LoadDatabaseComponents()
        {
            return LoadComponents("IBackupDatabase");
        }
        public static Dictionary<string, Type> LoadStorageComponents()
        {
            return LoadComponents("IBackupStorage");
        }



        public static void Backup(ConfigPair databaseConfig, List<ConfigPair> pipelineConfig, ConfigPair storageConfig, IUpdateNotification updateNotifier)
        {
            BackupOrRestore(true, storageConfig, databaseConfig, pipelineConfig, updateNotifier);
        }


        public static void Restore(ConfigPair storageConfig, List<ConfigPair> pipelineConfig, ConfigPair databaseConfig, IUpdateNotification updateNotifier)
        {
            BackupOrRestore(false, storageConfig, databaseConfig, pipelineConfig, updateNotifier);
        }




        private static void BackupOrRestore(bool isBackup, ConfigPair storageConfig, ConfigPair databaseConfig, List<ConfigPair> pipelineConfig, IUpdateNotification updateNotifier)
        {

            string deviceSetName = Guid.NewGuid().ToString();

            IBackupStorage storage = storageConfig.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupStorage;
            IBackupDatabase databaseComp = databaseConfig.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupDatabase;


            try
            {

                int numDevices = storage.GetNumberOfDevices(storageConfig.ConfigString);



                //NotifyWhenReady notifyWhenReady = new NotifyWhenReady(deviceName, isBackup);
                using (VirtualDeviceSet deviceSet = new VirtualDeviceSet())
                {

                    using (SqlThread sql = new SqlThread())
                    {
                        bool sqlStarted = false;
                        bool sqlFinished = false;
                        ExecutionExceptions exceptions = new ExecutionExceptions();

                        try
                        {

                            string instanceName = databaseComp.GetInstanceName(databaseConfig.ConfigString);
                            string clusterNetworkName = databaseComp.GetClusterNetworkName(databaseConfig.ConfigString);
                            List<string> deviceNames = sql.PreConnect(clusterNetworkName, instanceName, deviceSetName, numDevices, databaseComp, databaseConfig.ConfigString, isBackup, updateNotifier);

                            using (DisposableList<Stream> fileStreams = new DisposableList<Stream>(isBackup ? storage.GetBackupWriter(storageConfig.ConfigString) : storage.GetRestoreReader(storageConfig.ConfigString)))
                            using (DisposableList<Stream> topOfPilelines = new DisposableList<Stream>(CreatePipeline(pipelineConfig, fileStreams, isBackup)))
                            {

                                VirtualDeviceSetConfig config = new VirtualDeviceSetConfig();
                                config.Features = FeatureSet.PipeLike;
                                config.DeviceCount = (uint)topOfPilelines.Count;
                                deviceSet.CreateEx(instanceName, deviceSetName, config);
                                sql.BeginExecute();
                                sqlStarted = true;
                                deviceSet.GetConfiguration(TimeSpan.FromMinutes(1));
                                List<VirtualDevice> devices = new List<VirtualDevice>();

                                foreach (string devName in deviceNames)
                                {
                                    devices.Add(deviceSet.OpenDevice(devName));
                                }

                                using (DisposableList<DeviceThread> threads = new DisposableList<DeviceThread>(devices.Count))
                                {
                                    for (int i = 0; i < devices.Count; i++)
                                    {
                                        DeviceThread dThread = new DeviceThread();
                                        threads.Add(dThread);
                                        dThread.Initialize(isBackup, topOfPilelines[i], devices[i], deviceSet);
                                    }
                                    foreach (DeviceThread dThread in threads)
                                    {
                                        dThread.BeginCopy();
                                    }

                                    updateNotifier.OnStart();
                                    //Console.WriteLine(string.Format("{0} started", isBackup ? "Backup" : "Restore"));

                                    Exception sqlE = sql.EndExecute();
                                    sqlFinished = true;

                                    if (sqlE != null)
                                    {
                                        exceptions.ThreadException = sqlE;
                                    }

                                    foreach (DeviceThread dThread in threads)
                                    {
                                        Exception devE = dThread.EndCopy();
                                        if (devE != null)
                                        {
                                            exceptions.DeviceExceptions.Add(devE);
                                        }
                                    }


                                }
                            }
                        }
                        finally
                        {
                            if (sqlStarted && !sqlFinished)
                            {
                                Exception sqlE = sql.EndExecute();
                                sqlFinished = true;
                                if (sqlE != null)
                                {
                                    exceptions.ThreadException = sqlE;
                                }
                            }
                        }

                        if (exceptions.HasExceptions)
                        {
                            throw exceptions;
                        }
                    }
                }
            }
            catch
            {
                storage.CleanupOnAbort();
                throw;
            }

        }




        private static Stream[] CreatePipeline(List<ConfigPair> pipelineConfig, IList<Stream> fileStreams, bool isBackup)
        {
            List<Stream> result = new List<Stream>(fileStreams.Count);

            foreach (Stream fileStream in fileStreams)
            {
                Stream topStream = fileStream;

                for (int i = pipelineConfig.Count - 1; i >= 0; i--)
                {
                    ConfigPair config = pipelineConfig[i];

                    IBackupTransformer tran = config.TransformationType.GetConstructor(new Type[0]).Invoke(new object[0]) as IBackupTransformer;
                    if (tran == null)
                    {
                        throw new ArgumentException(string.Format("Unable to create pipe component: {0}", config.TransformationType.Name));
                    }
                    topStream = isBackup ? tran.GetBackupWriter(config.ConfigString, topStream) : tran.GetRestoreReader(config.ConfigString, topStream);
                }
                result.Add(topStream);
            }
            return result.ToArray();
        }



        private static Dictionary<string, Type> LoadComponents(string interfaceName)
        {
            Dictionary<string, Type> result = new Dictionary<string, Type>(StringComparer.InvariantCultureIgnoreCase);

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
                    FindPlugins(dll, result, interfaceName);
                }
            }

            return result;
        }



        private static void FindPlugins(Assembly dll, Dictionary<string, Type> result, string interfaceName)
        {
            foreach (Type t in dll.GetTypes())
            {
                try
                {
                    if (t.IsPublic)
                    {
                        if (!((t.Attributes & TypeAttributes.Abstract) == TypeAttributes.Abstract))
                        {
                            if (t.GetInterface(interfaceName) != null)
                            {
                                object o = t.GetConstructor(new Type[0]).Invoke(new object[0]);


                                IBackupPlugin test = o as IBackupPlugin;

                                if (test != null)
                                {
                                    string name = test.Name.ToLowerInvariant();

                                    if (name.Contains("|") || name.Contains("("))
                                    {
                                        throw new ArgumentException(string.Format("The name of the plugin, {0}, cannot contain these characters: |, (", name));
                                    }

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
                catch (Exception e)
                {
                    throw new Exception(string.Format("Warning: Plugin not loaded due to error: {0}", e.Message), e);
                }
            }
        }

    }
}
