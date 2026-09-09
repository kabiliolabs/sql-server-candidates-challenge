using System;

namespace SyncAgent.Host
{
    /// <summary>
    /// Entry point of the sync agent. The full console/Windows Service host is wired up in a
    /// later step; for now this only proves the .NET Framework 4.8 build chain is sound.
    /// </summary>
    internal static class Program
    {
        private static int Main()
        {
            Console.WriteLine("Kabilio SQL Server Sync Agent");
            return 0;
        }
    }
}
