using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace AlgeTimyUsb.SampleApplication
{
    static class Program
    {
        // Flag to indicate if the assembly was successfully loaded
        public static bool TimyAssemblyLoaded { get; private set; } = false;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Attach an event handler for missing assemblies
            AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);
            
            // Preload the Timy USB assembly to ensure proper device detection at startup
            try
            {
                // This will trigger the assembly resolution mechanism
                var dummy = typeof(Alge.TimyUsb);
            }
            catch
            {
                // Assembly resolution will be handled by the AssemblyResolve event
            }

            Application.Run(new Form1());
        }

        #region x86/x64 compatibility

        public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Check for if Alge Timy assembly needs to be resolved
            if (args.Name.ToLower().Contains("algetimyusb.dummy"))
            {
                // Detach event handler as it is not needed anymore
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
                        var a = System.Reflection.Assembly.LoadFile(filename);
                        TimyAssemblyLoaded = true; // Set flag indicating successful load
                        return a;
                    }
                    catch (Exception ex)
                    {
                        // Error on loading assembly - Timy Usb will not work and application will crash
                        System.Windows.Forms.MessageBox.Show("Error on loading the existing file '" + System.IO.Path.GetFileName(filename) + "'. This application cannot be executed without it.\nPlease make sure that you have Microsoft Visual C++ 2022 Runtime installed.\n\n" + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning);

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

    }
}
