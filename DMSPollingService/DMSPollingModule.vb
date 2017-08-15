Imports System.ServiceProcess
Imports System.Configuration.Install
Imports System.Diagnostics
Imports System.Configuration
Imports NationalInstruments.DAQmx
Imports System.Threading
Imports System.IO

Public Class NewService1
    Inherits System.ServiceProcess.ServiceBase
    Friend WithEvents Timer1 As System.Timers.Timer
    Private newtask As New Task
    Private newdigreader As DigitalSingleChannelReader
    Private newanalogreader As AnalogSingleChannelReader

    Private HttpReaders As New System.Collections.Specialized.ListDictionary()

    Public arrDAQ(10, 17) As Object
    Public cycle As Integer
    Public Sub New()
        MyBase.New()
        InitializeComponents()
        ' TODO: Add any further initialization code
    End Sub

    Private Sub InitializeComponents()
        Me.ServiceName = "DMSPollingService"
        Me.AutoLog = True
        Me.CanStop = True
        Me.Timer1 = New System.Timers.Timer()
        Me.Timer1.Interval = 5000
        Me.Timer1.Enabled = True

    End Sub
    ' This method starts the service.
    <MTAThread()> Shared Sub Main()
        Try

            ' To run more than one service you have to add them to the array
            System.ServiceProcess.ServiceBase.Run(New System.ServiceProcess.ServiceBase() _
            {New NewService1})

        Catch ex As Exception
            Dim sw As StreamWriter
            sw = File.AppendText("c:/DAQServiceErrors.txt")
            sw.WriteLine(Now() & "--MainInit--" & ex.Message)
            sw.Close()
        End Try
    End Sub
    ' Clean up any resources being used.
    Protected Overloads Overrides Sub Dispose(ByVal disposing As Boolean)
        MyBase.Dispose(disposing)
        ' TODO: Add cleanup code here (if required)
    End Sub
    Protected Overrides Sub OnStart(ByVal args() As String)
        ' TODO: Add start code here (if required)
        ' to start your service.
        Call DataLoad()

        Try

            Dim MyLog As New EventLog() ' create a new event log 
            ' Check if the the Event Log Exists 

            'If Not MyLog.SourceExists("DMSPollingService") Then
            'MyLog.CreateEventSource("DMSPollingService", "DMSPollingService1 Log") ' Create Log 
            'End If
            If Not EventLog.SourceExists("DMSPollingService") Then
                EventLog.CreateEventSource("DMSPollingService", "DMSPollingService1 Log") ' Create Log 
            End If

            MyLog.Source = "DMSPollingService"
            'MyLog.WriteEntry("DMSPollingService1 Log", "It is running v2", EventLogEntryType.Information)
            EventLog.WriteEntry("DMSPollingService1 Log", "It is running v2", EventLogEntryType.Information)

        Catch ex As Exception

            ErrorWriter("Error handling init error", ex)

        End Try

        Me.Timer1.Enabled = True

    End Sub
    Protected Overrides Sub OnStop()
        ' TODO: Add tear-down code here (if required) 
        ' to stop your service.
        Me.Timer1.Enabled = False
    End Sub
    Private Sub InitializeComponent()
        'LogWriter("initializecomponent")

        Me.Timer1 = New System.Timers.Timer
        CType(Me.Timer1, System.ComponentModel.ISupportInitialize).BeginInit()

        Me.Timer1.Enabled = True
        CType(Me.Timer1, System.ComponentModel.ISupportInitialize).EndInit()

    End Sub
    Sub DataLoad()
        'LogWriter("data load")

        Try

            Dim adapdevice As New DeviceTableAdapters.tblEventDeviceTableAdapter
            Dim dtdevice As New Device.tblEventDeviceDataTable

            dtdevice = adapdevice.GetData()

            Dim n As Integer

            ' TODO: use constant names instead of numbers
            ' will be easier to read and write the code
            ' declare C_DEVICE_PATH as 1
            ' and use arrDAQ(n, C_DEVICE_PATH) instead of arrDAQ(n, 1)

            ' TODO: give brief description for each ite, like 'row' or 'DeviceType'

            '1 path
            '2 digital reader
            '3 value
            '4 locid
            '5 row in downtime db that its written to
            '6 low - also used for location of httpstring for http device
            '7 lowtol
            '8 high
            '9 hightol
            '10 highorlow
            '11 DeviceID
            '12 Samples read
            '13 Total for samples read
            '14 Device Type (analog or digital or counter)
            '15 Thread Running for this device boolean
            '16 Counter Value
            '17 Connection Up? True = Good Connection, False = no connection




            For Each row As Device.tblEventDeviceRow In dtdevice
                'LogWriter(">> " & row.DeviceLocID)
                n = n + 1
                arrDAQ(n, 1) = row.DevicePath
                arrDAQ(n, 4) = row.DeviceLocID
                arrDAQ(n, 5) = row.DeviceEventID
                arrDAQ(n, 6) = row.DeviceLowVal
                arrDAQ(n, 7) = row.DeviceLowTol
                arrDAQ(n, 8) = row.DeviceHighVal
                arrDAQ(n, 9) = row.DeviceHighTol
                arrDAQ(n, 10) = row.DeviceStatus
                arrDAQ(n, 11) = row.EventDeviceID
                arrDAQ(n, 12) = 0
                arrDAQ(n, 13) = 0
                arrDAQ(n, 14) = row.DeviceType
                arrDAQ(n, 15) = False
                arrDAQ(n, 16) = 0
                arrDAQ(n, 17) = False


            Next

            For i = 1 To 10

                If Not arrDAQ(i, 1) = Nothing Then

                    If arrDAQ(i, 14) = 5 Then

                        Dim url As String = arrDAQ(i, 1)
                        Dim httpreader As httpPull2
                        If HttpReaders.Contains(url) Then
                            httpreader = HttpReaders(url)
                        Else
                            httpreader = New httpPull2(url)
                            HttpReaders.Add(url, httpreader)
                        End If

                        arrDAQ(i, 2) = httpreader
                        arrDAQ(i, 17) = True
                        Continue For

                    End If


                    'create new task for the DAQ card
                    newtask = New Task()

                    If arrDAQ(i, 14) = 1 Then
                        Try
                            'set the task to read a channel on the DAQ card
                            newtask.AIChannels.CreateVoltageChannel(arrDAQ(i, 1), "Analog", AITerminalConfiguration.Differential, 0, 5, AIVoltageUnits.Volts)
                            'set up the reader to use the task for reading the DAQ card
                            newanalogreader = New AnalogSingleChannelReader(newtask.Stream)
                            'write the reader to the matrix
                            arrDAQ(i, 2) = newanalogreader
                            arrDAQ(i, 17) = True

                        Catch ex As Exception
                            ErrorWriter("Analog Initialisation Error", ex)
                        End Try

                    ElseIf arrDAQ(i, 14) = 2 Then
                        'configure a digital reader
                        Try
                            newtask.DIChannels.CreateChannel(arrDAQ(i, 1), "Digi", ChannelLineGrouping.OneChannelForEachLine)
                            newdigreader = New DigitalSingleChannelReader(newtask.Stream)
                            arrDAQ(i, 2) = newdigreader
                            arrDAQ(i, 17) = True
                        Catch ex As Exception
                            ErrorWriter("Digital Initialisation error:", ex)
                        End Try

                    ElseIf arrDAQ(i, 14) = 3 Then
                        'configure a counter reader
                        startCounterDevice(i)
                    End If

                End If

            Next i

            Timer1.Start()

        Catch ex As Exception

            ErrorWriter("General Initialisation Error", ex)
        End Try
    End Sub

    Sub startCounterDevice(ByVal i As Integer)

        Try

            Dim ctTask As New Task
            'get current count for device
            Dim adapQuery As New DeviceTableAdapters.QueriesTableAdapter
            Dim currentcount As Integer = adapQuery.GetCurrentCount(arrDAQ(i, 11))

            ctTask.CIChannels.CreateCountEdgesChannel(arrDAQ(i, 1), "Counter", CICountEdgesActiveEdge.Rising, currentcount, CICountEdgesCountDirection.Up)
            ctTask.CIChannels(0).CountEdgesDigitalFilterEnable = True
            ctTask.CIChannels(0).CountEdgesDigitalFilterMinimumPulseWidth = 0.0007
            ctTask.Start()

            arrDAQ(i, 2) = ctTask
            arrDAQ(i, 17) = True

        Catch ex As Exception
            arrDAQ(i, 17) = False
            ErrorWriter("Counter Initialisation error:", ex)
        End Try

    End Sub

    Private Sub Timer1_Elapsed(ByVal sender As System.Object, ByVal e As System.Timers.ElapsedEventArgs) Handles Timer1.Elapsed
        startread()
    End Sub

    Sub startread()

        Dim workthreads As Integer
        Dim portthreads As Integer

        Dim Threadex As Exception = New Exception("ThreadStartException")

        Try
            For i = 1 To 10

                'if there is something in the first line of the matrix 
                If Not arrDAQ(i, 1) = Nothing Then

                    'if the thread is running still dont start a new one
                    If Not arrDAQ(i, 15) Then

                        If ThreadPool.QueueUserWorkItem(New WaitCallback(AddressOf DoRead), i) Then
                            ThreadPool.GetAvailableThreads(workthreads, portthreads)
                        Else
                            ErrorWriter("ThreadStartFailed", Threadex)
                        End If

                    End If
                End If
                Thread.Sleep(100)
            Next i

            'update end time of any rows which have a status of -1 (started but not ended)
            Dim AdapEndtime As New EventsTableAdapters.QueriesTableAdapter
            AdapEndtime.UpdateEventWithStatusNeg1()
            AdapEndtime.UpdateServerHandshake()

        Catch ex As Exception
            ErrorWriter("StartRead ", ex)
        End Try
    End Sub

    Private Sub DoRead(ByVal i As Integer)
        Try

            arrDAQ(i, 15) = True

            If arrDAQ(i, 14) = 5 Then 'if tis a http device like the rabbit devices

                Try
                    'LogWriter("pre getting data from " & arrDAQ(i, 4) & " res: " & arrDAQ(i, 3))

                    Dim httpreader As httpPull2 = arrDAQ(i, 2)
                    arrDAQ(i, 3) = httpreader.getCachedData(arrDAQ(i, 6))

                    If System.Configuration.ConfigurationManager.AppSettings("logOutput").ToString() = "True" Then
                        LogWriter("getting data from " & arrDAQ(i, 4) & " res: " & arrDAQ(i, 3) & " location: " & arrDAQ(i, 6))
                    End If

                    If arrDAQ(i, 3) = 1 Then

                        endevent(i)

                    ElseIf arrDAQ(i, 3) = 0 Then

                        startevent(i)

                    End If


                Catch ex As Net.WebException
                    ErrorWriter("couldn't receive data from server: " & arrDAQ(i, 1) & ": ", ex)

                Catch ex As IndexOutOfRangeException
                    ErrorWriter("returned data from " & arrDAQ(i, 1) & " is invalid", ex)

                Catch ex As Exception
                    ErrorWriter("cannot obtain information from: " & arrDAQ(i, 11) & "@" & arrDAQ(i, 4), ex)

                End Try

            End If

            If arrDAQ(i, 14) = 2 Then  'if its a digital read

                Try

                    arrDAQ(i, 3) = CInt(arrDAQ(i, 2).readsinglesamplesingleline())

                    If arrDAQ(i, 3) = -1 Then

                        endevent(i)

                    ElseIf arrDAQ(i, 3) = 0 Then

                        startevent(i)

                    End If

                Catch exG As Exception

                    If arrDAQ(i, 6) = 999.0 Then  '999.000 in the devicelowval field indicates that the device is a counter device being used as a digital start stop device
                        'hence you have to try to reconnect to it if it has lost its connection.
                        'if its a counter device being used just for stop start and the comms have failed try to reconnect
                        Try
                            newtask.DIChannels.CreateChannel(arrDAQ(i, 1), "Digi", ChannelLineGrouping.OneChannelForEachLine)
                            newdigreader = New DigitalSingleChannelReader(newtask.Stream)
                            arrDAQ(i, 2) = newdigreader
                            arrDAQ(i, 17) = True
                        Catch ex As Exception
                            ErrorWriter("Reconnect Digital Initialisation error:", ex)
                        End Try

                    End If


                End Try

            End If

            If arrDAQ(i, 14) = 3 Then ' if its a counter

                Dim oldCount As Integer = arrDAQ(i, 16)

                Try

                    'if device has lost communication then try to restart the device at each read
                    If arrDAQ(i, 17) = False Then
                        Call startCounterDevice(i)
                    Else

                        'read the value of the counter
                        arrDAQ(i, 16) = CInt(arrDAQ(i, 2).CIChannels(0).Count)

                        'if the counter is the same as last value the line is stopped
                        If arrDAQ(i, 16) <= oldCount Then
                            startevent(i)

                            'if the counter has incremented since the last value
                        ElseIf arrDAQ(i, 16) > oldCount Then
                            endevent(i)
                        End If

                        'update the asset table with the current count
                        Dim adapEvent As New EventsTableAdapters.QueriesTableAdapter
                        adapEvent.UpdateEventDeviceCounter(arrDAQ(i, 16), arrDAQ(i, 11))

                    End If
                Catch ex As Exception

                    Dim str As String = ex.Message
                    ErrorWriter("Counter Count Error", ex)

                    If str.Contains("-50405") Or str.Contains("-200088") Then
                        arrDAQ(i, 17) = False
                    End If

                End Try


            End If



            If arrDAQ(i, 14) = 4 Then ' if its a counter-only device (ie: digital device does start and stop)

                Try
                    Dim sw As StreamWriter
                    sw = File.AppendText("c:/DAQServiceErrors.txt")


                    'if device has lost communication then try to restart the device at each read
                    If arrDAQ(i, 17) = False Then
                        Call startCounterDevice(i)
                    Else

                        'read the value of the counter
                        arrDAQ(i, 16) = CInt(arrDAQ(i, 2).CIChannels(0).Count)

                        'update the asset table with the current count
                        Dim adapEvent As New EventsTableAdapters.QueriesTableAdapter
                        adapEvent.UpdateEventDeviceCounter(arrDAQ(i, 16), arrDAQ(i, 11))

                        sw.WriteLine(arrDAQ(i, 17), "--", arrDAQ(i, 2), arrDAQ(i, 16))


                    End If
                    sw.Close()
                Catch ex As Exception

                    Dim str As String = ex.Message
                    ErrorWriter("Counter-only Count Error", ex)

                    If str.Contains("-50405") Or str.Contains("-200088") Then
                        arrDAQ(i, 17) = False
                    End If

                End Try


            End If


            arrDAQ(i, 15) = False

        Catch ex As Exception

            ErrorWriter("DoRead: " & arrDAQ(i, 1), ex)
            arrDAQ(i, 15) = False

        End Try

    End Sub

    Sub startevent(ByVal i As Integer)
        Dim newrow As Integer
        Dim AdapEvent As New EventsTableAdapters.QueriesTableAdapter

        If arrDAQ(i, 10) = False Then
            AdapEvent.InsertStartEvent(Now(), arrDAQ(i, 4), arrDAQ(i, 11), 0, -1, arrDAQ(i, 16), newrow)
            AdapEvent.udpateTblDevice(True, newrow, arrDAQ(i, 11))
            arrDAQ(i, 10) = True
            arrDAQ(i, 5) = newrow
        End If

    End Sub

    Sub endevent(ByVal i As Integer)

        If arrDAQ(i, 10) = True Then
            Dim AdapEvent As New EventsTableAdapters.QueriesTableAdapter
            AdapEvent.InsertEndEvent(Now(), 0, arrDAQ(i, 16), arrDAQ(i, 5))
            AdapEvent.udpateTblDevice(False, 0, arrDAQ(i, 11))
            arrDAQ(i, 10) = False
            arrDAQ(i, 5) = 0
        End If

    End Sub
    Shared Sub ErrorWriter(ByVal pretext As String, ByVal ex As Exception)

        'INSERT A CATCH PHRASE HERE TO SEE IF IT ELIMINATES THE IO ERROR BEING GENERATED BY THIS SERVICE JD 100809
        Try
            Dim sw As StreamWriter
            sw = File.AppendText("c:/DAQServiceErrors.txt")
            sw.WriteLine(Now() & "--" & pretext & "--" & ex.Message)
            sw.Close()

        Catch exc As Exception

        End Try
    End Sub

    Shared Sub LogWriter(ByVal pretext As String)

        'log some debug messages
        Try
            Dim sw As StreamWriter
            sw = File.AppendText("c:/DAQServiceLogs.txt")
            sw.WriteLine(Now() & "--" & pretext & "--")
            sw.Close()

        Catch exc As Exception

        End Try
    End Sub

End Class


Public Class httpPull2
    Inherits httpPull

    Public Sub New(ByVal str As String)
        MyBase.New(str)
    End Sub

    ' overrides logger so the information can be saved in files
    Public Overrides Sub logerr(ByVal pretext As String, ByVal ex As Exception)
        NewService1.ErrorWriter(pretext, ex)
    End Sub

    Public Overrides Sub log(ByVal pretext As String)
        NewService1.LogWriter(pretext)
    End Sub

End Class
