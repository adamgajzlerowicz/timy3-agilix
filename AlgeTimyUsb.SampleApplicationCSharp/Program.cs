using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using System.Threading;

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
            try
            {
                // Set up global exception handlers for better stability
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
                AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Attach an event handler for missing assemblies
                AppDomain.CurrentDomain.AssemblyResolve += new ResolveEventHandler(CurrentDomain_AssemblyResolve);

                // Preload the Timy USB assembly to ensure proper device detection at startup
                try
                {
                    // This will trigger the assembly resolution mechanism
                    var dummy = typeof(Alge.TimyUsb);
                    TimyAssemblyLoaded = true;
                }
                catch (Exception ex)
                {
                    System.Windows.Forms.MessageBox.Show(
                        "Warning: Timy assembly could not be preloaded. It will be loaded on first use.\n\n" + ex.Message,
                        "Startup Warning",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    // Assembly resolution will be handled by the AssemblyResolve event
                }

                Application.Run(new Form1());
            }
            catch (Exception ex)
            {
                LogFatalError(ex);
                System.Windows.Forms.MessageBox.Show(
                    "Application startup failed: " + ex.Message + "\n\nPlease check the error log for details.",
                    "Fatal Error",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handle thread exceptions (Windows Forms threads)
        /// </summary>
        static void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                LogError(e.Exception, "Thread Exception");

                // Show a user-friendly message
                DialogResult result = MessageBox.Show(
                    "An unexpected error occurred. The application will attempt to continue.\n\n" +
                    "Error: " + e.Exception.Message + "\n\n" +
                    "Do you want to continue?",
                    "Application Error",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Error);

                if (result == DialogResult.No)
                {
                    Application.Exit();
                }
            }
            catch
            {
                // If we can't even show the error dialog, just try to exit gracefully
                try
                {
                    Application.Exit();
                }
                catch
                {
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// Handle unhandled exceptions (non-Windows Forms threads)
        /// </summary>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception ex = e.ExceptionObject as Exception;
                if (ex != null)
                {
                    LogError(ex, "Unhandled Exception");
                }

                if (e.IsTerminating)
                {
                    MessageBox.Show(
                        "A critical error occurred and the application must close.\n\n" +
                        (ex != null ? "Error: " + ex.Message : "Unknown error"),
                        "Fatal Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Stop);
                }
            }
            catch
            {
                // Last resort - just exit
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Log errors to a file for debugging
        /// </summary>
        static void LogError(Exception ex, string errorType)
        {
            try
            {
                string logPath = System.IO.Path.Combine(
                    System.IO.Path.GetDirectoryName(Application.ExecutablePath),
                    "error.log");

                using (var writer = new System.IO.StreamWriter(logPath, true))
                {
                    writer.WriteLine("===========================================");
                    writer.WriteLine($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                    writer.WriteLine($"Error Type: {errorType}");
                    writer.WriteLine($"Message: {ex.Message}");
                    writer.WriteLine($"Source: {ex.Source}");
                    writer.WriteLine($"Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        writer.WriteLine($"Inner Exception: {ex.InnerException.Message}");
                        writer.WriteLine($"Inner Stack Trace: {ex.InnerException.StackTrace}");
                    }
                    writer.WriteLine("===========================================");
                    writer.WriteLine();
                }
            }
            catch
            {
                // If we can't write to the log, don't crash the error handler
            }
        }

        /// <summary>
        /// Log fatal errors
        /// </summary>
        static void LogFatalError(Exception ex)
        {
            LogError(ex, "Fatal Error");
        }

        #region x86/x64 compatibility

        public static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            // Check for if Alge Timy assembly needs to be resolved
            if (args.Name.ToLower().Contains("algetimyusb"))
            {
                // Don't detach the event handler immediately - it might be needed for other assemblies

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
                        // Error on loading assembly - log it but try to continue
                        LogError(ex, "Assembly Load Error");

                        System.Windows.Forms.MessageBox.Show(
                            "Error loading '" + System.IO.Path.GetFileName(filename) + "'.\n\n" +
                            "Please ensure Microsoft Visual C++ 2022 Runtime is installed.\n\n" +
                            "The application will attempt to continue but Timy USB functionality may not work.\n\n" +
                            "Error: " + ex.Message,
                            "Assembly Load Error",
                            System.Windows.Forms.MessageBoxButtons.OK,
                            System.Windows.Forms.MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    // Correct assembly for platform does not exist - log and notify
                    var notFoundEx = new System.IO.FileNotFoundException(
                        "Unable to find " + System.IO.Path.GetFileName(filename),
                        filename);
                    LogError(notFoundEx, "Assembly Not Found");

                    System.Windows.Forms.MessageBox.Show(
                        "Unable to find " + System.IO.Path.GetFileName(filename) + ".\n\n" +
                        "The application will attempt to continue but Timy USB functionality will not work.",
                        "Missing Assembly",
                        System.Windows.Forms.MessageBoxButtons.OK,
                        System.Windows.Forms.MessageBoxIcon.Warning);
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
