﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NodeAssets.Core.Compilers;

namespace NodeAssets.Core.SourceManager
{
    public sealed class DefaultSourceManager : ISourceManager, IDisposable
    {
        private readonly DirectoryInfo _compilationDirectory;
        private readonly string _compileExtension;
        private readonly bool _combine;
        private readonly ISourceCompiler _compiler;

        private IPile _pile;
        private ICompilerConfiguration _compilerManager;

        public DefaultSourceManager(bool combine, string compileExtension, string compilationDirectory, ISourceCompiler compiler)
        {
            _compilationDirectory = new DirectoryInfo(compilationDirectory);
            if(!_compilationDirectory.Exists)
            {
                _compilationDirectory.Create();
            }

            _compileExtension = compileExtension;
            _combine = combine;
            _compiler = compiler;
        }

        public Task SetPileAsSource(IPile pile, ICompilerConfiguration compilerManager)
        {
            if (pile == null) { throw new ArgumentNullException("pile"); }
            if (compilerManager == null) { throw new ArgumentNullException("compilerManager"); }

            if(_pile != null)
            {
                throw new InvalidOperationException("Cannot change the pile once set - use another Source Manager!");
            }

            _compilerManager = compilerManager;
            _pile = pile;

            // Make sure compilation global directory exists
            foreach (var innerPile in _pile.FindAllPiles())
            {
                var dirPath = Path.Combine(_compilationDirectory.FullName, innerPile);
                if (!Directory.Exists(dirPath))
                {
                    Directory.CreateDirectory(dirPath);
                }   
            }

            if (_pile.IsWatchingFiles)
            {
                _pile.FileCreated += PileOnFileUpdated;
                _pile.FileDeleted += PileOnFileUpdated;
                _pile.FileUpdated += PileOnFileUpdated;
            }

            return CompileAll();
        }

        private IPile _dest;
        public IPile FindDestinationPile()
        {
            if (_pile == null)
            {
                throw new InvalidOperationException("You must first set the pile source via SetPileAsSource");
            }

            if (_dest == null)
            {
                var newPile = new Pile(_pile.IsWatchingFiles);

                foreach (var pile in _pile.FindAllPiles())
                {
                    // Get Urls (just copy over)
                    foreach (var uri in _pile.FindUrls(pile))
                    {
                        newPile.AddUrl(pile, uri.ToString());
                    }

                    // Get New Files
                    if (_combine)
                    {
                        var filePath = Path.Combine(_compilationDirectory.FullName, pile + _compileExtension);
                        newPile.AddFile(pile, filePath);
                    }
                    else
                    {
                        foreach (var file in _pile.FindFiles(pile))
                        {
                            var dirPath = Path.Combine(_compilationDirectory.FullName, pile);
                            var filePath = Path.Combine(dirPath, Path.GetFileNameWithoutExtension(file.Name) + _compileExtension);

                            newPile.AddFile(pile, filePath);
                        }
                    }
                }

                _dest = newPile;
            }

            return _dest;
        }

        private void PileOnFileUpdated(object sender, FileChangedEvent fileChangedEvent)
        {
            if (_combine)
            {
                CompilePile(fileChangedEvent.Pile);
            }
            else
            {
                CompileFile(fileChangedEvent.Pile, fileChangedEvent.File);
            }
        }

        private Task CompileAll()
        {
            var tasks = new List<Task>();

            foreach (var pile in _pile.FindAllPiles())
            {
                if (_combine)
                {
                    tasks.Add(CompilePile(pile));
                }
                else
                {
                    foreach (var file in _pile.FindFiles(pile))
                    {
                        tasks.Add(CompileFile(pile, file));
                    }
                }
            }

            // We just create a task as a hook so that you can do things when a compile finishes
            if (tasks.Any())
            {
                return Task.Factory.ContinueWhenAll(tasks.ToArray(), (taskList) => { });
            }
            else
            {
                return Task.Factory.StartNew(() => { });
            }
        }

        private Task CompileFile(string pile, FileInfo file)
        {
            // Here we are keeping the files seperate... so just compile the single file
            // However the file will live in a subDirectory
            var dirPath = Path.Combine(_compilationDirectory.FullName, pile);
            var filePath = Path.Combine(dirPath, Path.GetFileNameWithoutExtension(file.Name) + _compileExtension);

            // Compile/minimise and write to file
            return _compiler.CompileFile(file, _compilerManager).ContinueWith(task => AttemptWrite(filePath, task.Result));
        }

        private Task CompilePile(string pile)
        {
            // Here we are combining all files
            // If one file in a pile changes... all the rest have to too
            var filePath = Path.Combine(_compilationDirectory.FullName, pile + _compileExtension);

            var tasks = _pile.FindFiles(pile).Select(file => _compiler.CompileFile(file, _compilerManager)).ToArray();

            if (tasks.Any())
            {
                return Task.Factory.ContinueWhenAll(tasks, t =>
                {
                    var builder = new StringBuilder();

                    foreach (var task in t)
                    {
                        builder.Append(task.Result);
                    }

                    AttemptWrite(filePath, builder.ToString());
                });
            }
            else
            {
                return Task.Factory.StartNew(() => { });
            }
        }

        private void AttemptWrite(string path, string text)
        {
            var numTries = 0;

            // This is crap but apparently the only consistent way to wait for a lock on a file
            while (numTries < 10)
            {
                try
                {
                    File.WriteAllText(path, text);
                    numTries = 11;
                }
                catch (IOException)
                {
                    Thread.Sleep(300);
                    numTries++;
                }
            }
        }

        public void Dispose()
        {
            if(_pile != null && _pile.IsWatchingFiles)
            {
                _pile.FileCreated -= PileOnFileUpdated;
                _pile.FileDeleted -= PileOnFileUpdated;
                _pile.FileUpdated -= PileOnFileUpdated;
                _pile = null;
            }
        }
    }
}
