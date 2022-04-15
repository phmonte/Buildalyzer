Imports System.IO
Imports Newtonsoft.Json
Imports Org.BouncyCastle.Security

Module Module1

    Sub Main()
        Console.WriteLine("Hello World")

        Dim fileInput = Console.ReadLine()
        Dim jsonFile
        If fileInput IsNot "" Then
            jsonFile = fileInput
        Else
            jsonFile = "..\..\..\config.json"
        End If

        Dim fileReader As New StreamReader(CStr(jsonFile))
        Dim fileText As String = fileReader.ReadToEnd()
        Dim config As Configuration = JsonConvert.DeserializeObject(Of Configuration)(fileText)
        Console.WriteLine(config.Description)

        Dim random = New SecureRandom()
        Console.WriteLine(random.Next())
        Console.ReadKey()
    End Sub

    Function SimpleFunction() As String
        Return "This is a function"
    End Function

End Module
