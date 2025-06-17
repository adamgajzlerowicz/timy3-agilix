// WARNING: The assembly AlgeTimyUsb.dummy.dll may NEVER be located in the bin/debug/release folder or when installing an application


// Attach an event handler for missing assemblies in Program.Main() or App.OnStartup() as follows:
AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

#region x86/x64 compatibility

public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
{
    // Check for if Alge Timy assembly needs to be resolved
    if (args.Name.Contains("AlgeTimyUsb"))
    {
        // Detach event handler s it is not needed anymore
        AppDomain.CurrentDomain.AssemblyResolve -= new ResolveEventHandler(CurrentDomain_AssemblyResolve);

        // Construct correct filename depending on the platform
        String filename = "AlgeTimyUsb." + (IsX64Process ? "x64" : "x86") + ".dll";
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assembly.Location), filename);

        // Check for file exists
        if (System.IO.File.Exists(filename))
        {
            try
            {
                // Try to load assembly
                return System.Reflection.Assembly.LoadFile(filename);
            }
            catch (Exception ex)
            {
                // Error on loading assembly - Timy Usb will not work and application will crash
                System.Windows.Forms.MessageBox.Show("Error on loading the existing file '" + System.IO.Path.GetFileName(filename) + "'. This application cannot be executed without it.\nPlease make sure that you have Microsoft Visual C++ 2012 Runtime installed.\n\n" + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

            }
        }
        else
        {
            // Correct assembly for platform does not exist - Timy Usb will not work and application will crash
            System.Windows.Forms.MessageBox.Show("Unable to find " + System.IO.Path.GetFileName(filename) + ". This application cannot be executed without it.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);
        }
        return null;
    }
    return null;
}

/// <summary>
/// Determines whether the process is running as x64 or x86
/// </summary>
public static bool IsX64Process
{
    get { return IntPtr.Size == 8; }
}

#endregion