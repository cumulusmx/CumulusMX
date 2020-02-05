using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Autofac;
using CumulusMX.Common;
using McMaster.NETCore.Plugins;

namespace CumulusMX.Extensions
{
    public class ExtensionLoader
    {
        private static readonly log4net.ILog log = log4net.LogManager.GetLogger("cumulus", System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);
        private readonly ExtensionLoaderSettings _settings;


        public ExtensionLoader(ExtensionLoaderSettings settings)
        {
            this._settings = settings;
        }


        public Dictionary<Type, Type[]> GetExtensions()
        {
            log.Info($"Loading extensions from path '{_settings.Path}'");
            if (!Directory.Exists(_settings.Path))
            {
                log.Info($"Creating missing extensions directory '{_settings.Path}'");
                Directory.CreateDirectory(_settings.Path);
            }

            Dictionary<Type,Type[]> foundExtensions = new Dictionary<Type, Type[]>();
            var directoryInfo = new DirectoryInfo(_settings.Path);
            var extensionDirectories = directoryInfo.EnumerateDirectories();
            var extensionInterfaceType = typeof(IExtension);
            var passiveInterfaceType = typeof(IPassive);

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

                        // Register all extension types (those that implement IExtension)
                        var types = assembly.GetTypes().Where(t => t.GetInterfaces().Any(i => i.FullName.Equals(extensionInterfaceType.FullName)));

                        foreach (var type in types)
                        {
                            var identifier = (ExtensionIdentifierAttribute)type.GetCustomAttributes()
                                .FirstOrDefault(x => x.GetType() == typeof(ExtensionIdentifierAttribute));

                            if (identifier != null)
                                foreach (var @interface in type.GetInterfaces())
                                {
                                    AutofacWrapper.Instance.Builder.RegisterType(type)
                                        .Keyed(identifier.Identifier, @interface).AsImplementedInterfaces();
                                }

                            AutofacWrapper.Instance.Builder.RegisterType(type);
                            foundExtensions.Add(type,type.GetInterfaces());
                        }

                        // Register all types that implement IPassive - these are needed for dependency injection
                        var passiveTypes = assembly
                            .GetTypes()
                            .Where(t => t.GetInterfaces().Any(i => i.FullName.Equals(passiveInterfaceType.FullName)));

                        foreach (var type in passiveTypes)
                        {
                            AutofacWrapper.Instance.Builder.RegisterType(type);
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
