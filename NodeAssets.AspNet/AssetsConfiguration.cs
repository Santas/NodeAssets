﻿using System;
using System.IO;
using System.Web.Routing;
using NodeAssets.Core;

namespace NodeAssets.AspNet
{
    public class AssetsConfiguration : IAssetsConfiguration
    {
        public IAssetsConfiguration ConfigureCompilers(Func<ICompilerConfiguration, ICompilerConfiguration> compilerManagerFunc)
        {
            CompilerConfiguration = compilerManagerFunc(new CompilerConfiguration());
            return this;
        }

        public IAssetsConfiguration ConfigureSourceManager(Func<ISourceManagerConfiguration, ISourceManagerConfiguration> sourceManagerFunc)
        {
            SourceConfiguration = sourceManagerFunc(new SourceManagerConfiguration());
            return this;
        }

        public IAssetsConfiguration Cache(bool cache = true)
        {
            UseCache = cache;
            return this;
        }

        public IAssetsConfiguration Compress(bool compress = true)
        {
            UseCompress = compress;
            return this;
        }

        public IAssetsConfiguration LiveCss(bool live = true, string signalRNamespace = "nodeassets")
        {
            IsLiveCss = live;
            Namespace = signalRNamespace;
            return this;
        }

        public IAssetsConfiguration SetRouteHandlerFunction(Func<string, FileInfo, IAssetsConfiguration, IRouteHandler> routeHandler)
        {
            RouteHandlerFunction = routeHandler;
            return this;
        }

        public bool UseCache { get; private set; }
        public bool UseCompress { get; private set; }
        public bool IsLiveCss { get; private set; }
        public string Namespace { get; private set; }
        public ICompilerConfiguration CompilerConfiguration { get; private set; }
        public ISourceManagerConfiguration SourceConfiguration { get; private set; }
        public Func<string, FileInfo, IAssetsConfiguration, IRouteHandler> RouteHandlerFunction { get; private set; }
    }
}
