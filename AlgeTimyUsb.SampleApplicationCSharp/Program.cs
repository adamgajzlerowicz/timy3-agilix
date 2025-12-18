using System;
using System.Windows.Forms;

namespace AlgeTimyUsb.SampleApplication
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Handle x86/x64 assembly loading
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;

            Application.Run(new Form1());
        }

        static System.Reflection.Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            if (args.Name.ToLower().Contains("algetimyusb"))
            {
                string filename = "AlgeTimyUsb." + (IntPtr.Size == 8 ? "x64" : "x86") + ".dll";
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assembly.Location), filename);

                if (System.IO.File.Exists(filename))
                {
                    try
                    {
                        return System.Reflection.Assembly.LoadFile(filename);
                    }
                    catch { }
                }
            }
            return null;
        }
    }
}
