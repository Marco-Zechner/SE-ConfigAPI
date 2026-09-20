using System;
using System.IO;

namespace Sandbox.ModAPI
{
    internal interface IConfigApiTestUtilities
    {
        bool FileExistsInLocalStorage(string file, Type scope);
        TextReader ReadFileInLocalStorage(string file, Type scope);
        TextWriter WriteFileInLocalStorage(string file, Type scope);

        bool FileExistsInGlobalStorage(string file);
        TextReader ReadFileInGlobalStorage(string file);
        TextWriter WriteFileInGlobalStorage(string file);

        bool FileExistsInWorldStorage(string file, Type scope);
        TextReader ReadFileInWorldStorage(string file, Type scope);
        TextWriter WriteFileInWorldStorage(string file, Type scope);
    }

    internal static class MyAPIGateway
    {
        public static IConfigApiTestUtilities Utilities { get; set; }
    }
}
