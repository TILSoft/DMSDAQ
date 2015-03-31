Imports System.Threading
Imports System.IO

Public Class httpPull

    ' url from which data is being pulled of
    Private Property _url As String
    ' array holding parsed reply from server
    Private Property _result As List(Of Integer)
    Private Property _cached = False
    Private Property _semaphore As Semaphore = New Semaphore(0, 1)

    Public Sub New(ByVal url As String)

        _url = url
        _result = New List(Of Integer)
        _semaphore.Release(1)

    End Sub

    ' connects to a server and returns response
    Private Function sendRequest(ByVal address As String)

        Dim webClient As New System.Net.WebClient()
        Return webClient.DownloadString(address)

    End Function

#Region "Obsolete Code"



    ' transforms input: 00 01 01 into array of integers 0,1,1
    'Private Function parseResult(ByVal input As String)
    '    Try


    '        ' Dim result As List(Of Integer) = New List(Of Integer)
    '        'Console.WriteLine("input: {0} length: {1}", input, input.Length())
    '        'log("input: " & input & " length: " & input.Length())
    '        Dim idx As Integer = 0

    '        Dim strResult() As String = input.Split(" ")
    '        Dim intResult() = Array.ConvertAll(strResult, Function(str) Int32.Parse(str))

    '        Dim result = intResult.ToList()

    '        log("response " & input.ToString & "- Length" & input.Length & "-" & result(0) & "-" & result(1))

    '        ' get the number of triplets from the lenfth of response 
    '        ' the first block's length is 2, so it needs to be incremented
    '        'Dim dbl_blocks As Double = (input.Length() + 1) / 3
    '        'Dim int_blocks As Integer = (input.Length() + 1) / 3

    '        '' compare result of flowting point and integer division
    '        '' to find out if the input is valid
    '        'If Not dbl_blocks = int_blocks Then
    '        '    log("invalid length of http response. this was the response " & input.ToString & "- Length" & input.Length)
    '        '    Return result
    '        'End If


    '        'For index As Integer = 1 To input.Length() Step 3
    '        '    'Console.WriteLine("id[{0}] res: {1} ", index, input.ElementAt(index))
    '        '    result.Add(Val(input.ElementAt(index)))
    '        'Next

    '        ' other idea, maybe even better
    '        ' split by ' ' and then make sure each has length of 2 bytes
    '        'Dim data As List(Of String) = input.Split(" ")

    '        Return result
    '    Catch ex As Exception
    '        ErrorWriter("Parse Error", ex)
    '    End Try

    'End Function
#End Region

    ' transforms input: 00 01 01 into array of integers 0,1,1
    Private Function parseResult(ByVal input As String)
        Try
            'trim whitespace off string, also lots of CRLFs at end of string
            input = input.Trim()
            'split string at spaces
            Dim strResult() As String = input.Split(" ")
            'take only the right hand value of the pair
            Dim strResultTrim() = Array.ConvertAll(strResult, Function(str) Right(Left(str, 2), 1))
            'convert array to integer array from string
            Dim intResult() = Array.ConvertAll(strResultTrim, Function(str) Int32.Parse(str))
            'convert array to list type
            Dim result = intResult.ToList()

            Return result

        Catch ex As Exception
            ErrorWriter("Parse Error", ex)

        End Try

    End Function

    ' gets information about line status from cache
    ' unles it is out of date or needs to be initialised, then it refreshes cache
    Public Function getCachedData(ByVal index As Integer)

        ' obtain exclusive lock to avoid race condition situation
        'log("obtaining lock")
        _semaphore.WaitOne()
        'log("got lock")
        Dim result As Integer = -1

        'because the the code here is executing in critical section 
        'all exceptions have to be cought, semafor then has to be released 
        'and exception re-thrown
        Try
            ' if data is cached get result from it
            ' can throw index error exception
            If _cached Then
                result = _result(index)
            End If

            ' the data is out of date or the data was never cached refresh it
            ' can throw any network exception
            If result = -1 Or Not _cached Then
                _result = updateCache()
                'get result again
                'can throw index error
                result = _result(index)
            End If

            ' make sure next time we get data refreshed
            _result(index) = -1

            'catch exception, release semaphore and re-throw exception
        Catch ex As Exception
            _semaphore.Release(1)
            Throw ex
        End Try

        _cached = True

        ' leave critical section
        _semaphore.Release(1)
        'log("released lock")
        Return result

    End Function

    ' updates cached data
    Private Function updateCache()

        Dim httpresult As String

        ' get data from a server
        httpresult = sendRequest(_url)
        'Console.WriteLine("http result: " & httpresult)

        'Commented out as used for testing only
        'Dim ex As New Exception
        'ErrorWriter("HttpString: " & httpresult, ex)

        ' parse it and return result
        Return parseResult(httpresult)

    End Function

    ' two loggers, for logging information, and errors

    Public Overridable Sub log(ByVal message As String)

        Console.WriteLine("info: " & message)

    End Sub

    Public Overridable Sub logerr(ByVal message As String, ByVal ex As Exception)

        Console.WriteLine(message & ": " & ex.Message)

    End Sub

    ' small unit test
    Public Sub UTparse(ByVal input As String)

        Console.WriteLine("input: '{0}'", input)
        Dim result As List(Of Integer) = parseResult(input)

        Dim idx As Integer = 0
        For Each x As Integer In result
            Console.WriteLine("res[{0}]: {1}", idx, x)
            idx += 1
        Next
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

End Class
