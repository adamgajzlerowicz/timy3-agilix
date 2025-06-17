Namespace My

    ' The following events are available for MyApplication:
    ' 
    ' Startup: Raised when the application starts, before the startup form is created.
    ' Shutdown: Raised after all application forms are closed.  This event is not raised if the application terminates abnormally.
    ' UnhandledException: Raised if the application encounters an unhandled exception.
    ' StartupNextInstance: Raised when launching a single-instance application and the application is already active. 
    ' NetworkAvailabilityChanged: Raised when the network connection is connected or disconnected.
    Partial Friend Class MyApplication

        Protected Overrides Function OnStartup(eventArgs As ApplicationServices.StartupEventArgs) As Boolean

            AddHandler AppDomain.CurrentDomain.AssemblyResolve, AddressOf CurrentDomain_AssemblyResolve

            Return MyBase.OnStartup(eventArgs)
        End Function


        Private Function CurrentDomain_AssemblyResolve(sender As Object, args As System.ResolveEventArgs) As Object
            ' Check for if Alge Timy assembly needs to be resolved
            If args.Name.Contains("AlgeTimyUsb") Then
                ' Construct correct filename depending on the platform
                Dim filename As String = "AlgeTimyUsb." + If(IsX64Process(), "x64", "x86") + ".dll"
                Dim assembly As System.Reflection.Assembly = System.Reflection.Assembly.GetExecutingAssembly()
                filename = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assembly.Location), filename)

                ' Check for file exists
                If System.IO.File.Exists(filename) Then
                    Try
                        'Try to load assembly
                        Return System.Reflection.Assembly.LoadFile(filename)
                    Catch ex As Exception
                        'Error on loading assembly - Timy Usb will not work and application will crash
                        System.Windows.Forms.MessageBox.Show("Error on loading the existing file '" + System.IO.Path.GetFileName(filename) + "'. This application cannot be executed without it.\nPlease make sure that you have Microsoft Visual C++ 2022 Runtime installed.\n\n" + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning)

                    End Try
                Else
                    ' Correct assembly for platform does not exist - Timy Usb will not work and application will crash
                    System.Windows.Forms.MessageBox.Show("Unable to find " + System.IO.Path.GetFileName(filename) + ". This application cannot be executed without it.", "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Warning)
                End If
                Return Nothing
            End If
            Return Nothing
        End Function


        Private Function IsX64Process() As Boolean
            Return IntPtr.Size = 8
        End Function

    End Class


End Namespace

