using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using CumulusMX.Extensions.Station;
using McMaster.NETCore.Plugins;

namespace CumulusMX.Extensions
{
    public class ExtensionLoader
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ExtensionLoaderSettings _settings;


        public ExtensionLoader(ExtensionLoaderSettings settings)
        {
            this._settings = settings;
        }


        public IEnumerable<ExtensionDescriptor> GetExtensions()
        {
            log.Info($"Loading extensions from path '{_settings.Path}'");
            if (!Directory.Exists(_settings.Path))
            {
                log.Error($"Extensions directory '{_settings.Path}' does not exist");
                throw new Exception($"Extensions directory '{_settings.Path}' does not exist");
            }
            List<ExtensionDescriptor> foundExtensions = new List<ExtensionDescriptor>();
            var extensionDirectories = new DirectoryInfo(_settings.Path).EnumerateDirectories();
            foreach (var directory in extensionDirectories)
            {
                string filePath = Path.Combine(directory.FullName, directory.Name + ".dll");
                if (File.Exists(filePath))
                {
                    try
                    {
                        log.Debug($"Found possible extension at '{filePath}'");
                        var loader = PluginLoader.CreateFromAssemblyFile(filePath, PluginLoaderOptions.PreferSharedTypes);
                        var assembly = loader.LoadDefaultAssembly();
                        var interfaceType = typeof(IExtension);
                        var types = assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i.FullName.Equals(interfaceType.FullName)));
                        foreach (var type in types)
                        {
                            var extension = (IExtension)Activator.CreateInstance(type);
                            var extensionDescriptor = new ExtensionDescriptor(extension.Identifier, extension);

                            foundExtensions.Add(extensionDescriptor);
                        }
                    }
                    catch (ReflectionTypeLoadException ex)
                    {
                        foreach (var lex in ex.LoaderExceptions)
                        {
                            log.Error($"Loader exception error creating instance of connector.", lex);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error($"Error creating instance of connector.", ex);
                    }
                }
            }

            return foundExtensions;
        }

    }
}
