using System;
using System.IO;
using System.Reflection;

class CheckDlls
{
    static void Main()
    {
        Console.WriteLine("DLL Check Utility");
        Console.WriteLine("----------------");
        
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        Console.WriteLine($"Current directory: {baseDir}");
        
        // Check for DLLs
        string[] dllsToCheck = {
            "AlgeTimyUsb.Dummy.dll",
            "AlgeTimyUsb.x86.dll",
            "AlgeTimyUsb.x64.dll"
        };
        
        foreach (string dll in dllsToCheck)
        {
            string dllPath = Path.Combine(baseDir, dll);
            bool exists = File.Exists(dllPath);
            Console.WriteLine($"{dll}: {(exists ? "Found" : "Not found")} at {dllPath}");
            
            if (exists)
            {
                try
                {
                    var assembly = Assembly.LoadFile(dllPath);
                    Console.WriteLine($"  - Successfully loaded: {assembly.FullName}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"  - Failed to load: {ex.Message}");
                }
            }
        }
        
        Console.WriteLine("\nPress Enter to exit...");
        Console.ReadLine();
    }
} 