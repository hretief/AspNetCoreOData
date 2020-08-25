﻿// Copyright (c) Microsoft Corporation.  All rights reserved.
// Licensed under the MIT License.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using Microsoft.OData;
using Microsoft.OData.UriParser;

namespace Microsoft.AspNetCore.OData.Abstracts
{
    /// <summary>
    /// A class for managing per-route service containers.
    /// </summary>
    public class PerRouteContainer : IPerRouteContainer
    {
        private ConcurrentDictionary<string, IServiceProvider> _perRouteContainers;

        /// <summary>
        /// Initializes a new instance of the <see cref="PerRouteContainer"/> class.
        /// </summary>
        public PerRouteContainer()
        {
            _perRouteContainers = new ConcurrentDictionary<string, IServiceProvider>();
        }

        /// <summary>
        /// Gets or sets a function to build an <see cref="IContainerBuilder"/>
        /// </summary>
        public Func<IContainerBuilder> BuilderFactory { get; set; }

        /// <summary>
        /// Create a root container for a given route name.
        /// </summary>
        /// <param name="routeName">The route name.</param>
        /// <param name="configureAction">The configuration actions to apply to the container.</param>
        /// <returns>An instance of <see cref="IServiceProvider"/> to manage services for a route.</returns>
        public virtual IServiceProvider CreateServiceProvider(string routeName, Action<IContainerBuilder> configureAction)
        {
            IContainerBuilder builder = CreateContainerBuilderWithCoreServices();

            configureAction?.Invoke(builder);

            IServiceProvider serviceProvider = builder.BuildContainer();
            if (serviceProvider == null)
            {
                throw Error.InvalidOperation(SRResources.NullContainer);
            }

            _perRouteContainers.AddOrUpdate(routeName, serviceProvider, (k, v) => serviceProvider);

            return serviceProvider;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="routeName"></param>
        /// <returns></returns>
        public virtual IServiceProvider GetServiceProvider(string routeName)
        {
            IServiceProvider rootContainer;
            if (_perRouteContainers.TryGetValue(routeName, out rootContainer))
            {
                return rootContainer;
            }

            return null;
        }

        /// <summary>
        /// Create a container builder with the default OData services.
        /// </summary>
        /// <returns>An instance of <see cref="IContainerBuilder"/> to manage services.</returns>
        protected IContainerBuilder CreateContainerBuilderWithCoreServices()
        {
            IContainerBuilder builder;
            if (this.BuilderFactory != null)
            {
                builder = this.BuilderFactory();
                if (builder == null)
                {
                    throw Error.InvalidOperation(SRResources.NullContainerBuilder);
                }
            }
            else
            {
                builder = new DefaultContainerBuilder();
            }

            builder.AddDefaultODataServices();

            // Set Uri resolver to by default enabling unqualified functions/actions and case insensitive match.
            builder.AddService(
                ServiceLifetime.Singleton,
                typeof(ODataUriResolver),
                sp => new UnqualifiedODataUriResolver { EnableCaseInsensitive = true });

            return builder;
        }
    }
}
