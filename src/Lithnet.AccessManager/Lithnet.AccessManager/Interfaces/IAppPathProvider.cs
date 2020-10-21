﻿namespace Lithnet.AccessManager
{
    public interface IAppPathProvider
    {
        string AppPath { get; } 

        string TemplatesPath { get; } 

        string ScriptsPath { get; } 

        string WwwRootPath { get; } 

        string ImagesPath { get; } 

        string LogoPath { get; }

        string ConfigFile { get; }

        string HostingConfigFile { get; }

        string GetRelativePath(string file, string basePath);

        string GetFullPath(string file, string basePath);
    }
}