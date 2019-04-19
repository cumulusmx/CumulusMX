using System;
using System.Collections.Generic;
using System.Text;
using Autofac;
using Autofac.Core.Lifetime;

namespace CumulusMX.Common
{
    public class AutofacWrapper
    {
        private IContainer _container;
        private ContainerBuilder _builder;
        private ILifetimeScope _scope;
        public static AutofacWrapper Instance { get; }
        static AutofacWrapper()
        {
            Instance = new AutofacWrapper(); 
        }

        public ContainerBuilder Builder
        {
            get
            {
                if (_builder == null)
                    _builder = new ContainerBuilder();

                return _builder;
            }
        }

        public ILifetimeScope Scope
        {
            get
            {
                if (_scope == null)
                {
                    if (_container == null)
                        _container = _builder.Build();
                    _scope = _container.BeginLifetimeScope();
                }

                return _scope;
            }
        }

        public void EndLifetime()
        {
            _scope.Dispose();
            _scope = null;
            _container = null;
        }

        private AutofacWrapper()
        {
        }
    }
}
