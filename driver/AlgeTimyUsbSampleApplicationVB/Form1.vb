Public Class Form1

    Private timyUsb As Alge.TimyUsb

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        timyUsb = New Alge.TimyUsb(Me)
        timyUsb.Start()

        AddHandler timyUsb.LineReceived, AddressOf timyUsb_LineReceived
    End Sub

    Private Sub timyUsb_LineReceived(sender As Object, e As Alge.DataReceivedEventArgs)
        ListBox1.Items.Insert(0, e.Data)
    End Sub

End Class
