Option Strict On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

' =====================================================================
'  Creates real Excel workbooks (.xlsx) with no extra packages.
'  Header rows are navy and bold, money cells are numbers formatted
'  as GH₵ so Excel can add them up.
' =====================================================================

' Wrap a value in ExcelMoney to show it as GH₵ in Excel.
Public Class ExcelMoney
    Public ReadOnly Property Value As Decimal

    Public Sub New(value As Decimal)
        Me.Value = value
    End Sub
End Class

Public Class ExcelSheet
    Public Property Name As String
    Public Property Header As String()
    Public ReadOnly Property Rows As New List(Of Object())()

    Public Sub New(name As String, ParamArray header As String())
        Me.Name = name
        Me.Header = header
    End Sub

    Public Sub AddRow(ParamArray values As Object())
        Rows.Add(values)
    End Sub
End Class

Public Module ExcelWriter
    Public Sub Save(path As String, sheets As IList(Of ExcelSheet))
        Dim names = UniqueSheetNames(sheets)
        Dim entries As New List(Of KeyValuePair(Of String, Byte()))()

        Dim types As New StringBuilder()
        types.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        types.Append("<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">")
        types.Append("<Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>")
        types.Append("<Default Extension=""xml"" ContentType=""application/xml""/>")
        types.Append("<Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>")
        types.Append("<Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>")
        For i = 1 To sheets.Count
            types.Append("<Override PartName=""/xl/worksheets/sheet" & i & ".xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>")
        Next
        types.Append("</Types>")
        AddEntry(entries, "[Content_Types].xml", types.ToString())

        AddEntry(entries, "_rels/.rels",
                 "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
                 "<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">" &
                 "<Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>" &
                 "</Relationships>")

        Dim workbook As New StringBuilder()
        workbook.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        workbook.Append("<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships""><sheets>")
        Dim rels As New StringBuilder()
        rels.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        rels.Append("<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">")
        For i = 1 To sheets.Count
            workbook.Append("<sheet name=""" & Escape(names(i - 1)) & """ sheetId=""" & i & """ r:id=""rId" & i & """/>")
            rels.Append("<Relationship Id=""rId" & i & """ Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet" & i & ".xml""/>")
        Next
        workbook.Append("</sheets></workbook>")
        rels.Append("<Relationship Id=""rId" & (sheets.Count + 1) & """ Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>")
        rels.Append("</Relationships>")
        AddEntry(entries, "xl/workbook.xml", workbook.ToString())
        AddEntry(entries, "xl/_rels/workbook.xml.rels", rels.ToString())
        AddEntry(entries, "xl/styles.xml", StylesXml())

        For i = 1 To sheets.Count
            AddEntry(entries, "xl/worksheets/sheet" & i & ".xml", SheetXml(sheets(i - 1)))
        Next

        WriteZip(path, entries)
    End Sub

    Private Sub AddEntry(entries As List(Of KeyValuePair(Of String, Byte())), name As String, content As String)
        entries.Add(New KeyValuePair(Of String, Byte())(name, New UTF8Encoding(False).GetBytes(content)))
    End Sub

    Private Function StylesXml() As String
        Return "<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>" &
               "<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">" &
               "<numFmts count=""1""><numFmt numFmtId=""164"" formatCode=""&quot;GH₵&quot; #,##0.00""/></numFmts>" &
               "<fonts count=""2""><font><sz val=""11""/><name val=""Calibri""/></font>" &
               "<font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font></fonts>" &
               "<fills count=""3""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill>" &
               "<fill><patternFill patternType=""solid""><fgColor rgb=""FF0B1F3A""/><bgColor indexed=""64""/></patternFill></fill></fills>" &
               "<borders count=""1""><border><left/><right/><top/><bottom/><diagonal/></border></borders>" &
               "<cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>" &
               "<cellXfs count=""3""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>" &
               "<xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""0"" xfId=""0"" applyFont=""1"" applyFill=""1""/>" &
               "<xf numFmtId=""164"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/></cellXfs>" &
               "<cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>" &
               "</styleSheet>"
    End Function

    Private Function SheetXml(sheet As ExcelSheet) As String
        Dim columnCount = Math.Max(sheet.Header.Length, If(sheet.Rows.Count = 0, 0, sheet.Rows.Max(Function(r) r.Length)))
        Dim widths(Math.Max(0, columnCount - 1)) As Integer
        For c = 0 To columnCount - 1
            widths(c) = If(c < sheet.Header.Length, sheet.Header(c).Length, 0)
        Next
        For Each row In sheet.Rows
            For c = 0 To row.Length - 1
                widths(c) = Math.Max(widths(c), DisplayText(row(c)).Length)
            Next
        Next

        Dim sb As New StringBuilder()
        sb.Append("<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>")
        sb.Append("<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">")
        sb.Append("<sheetViews><sheetView workbookViewId=""0""><pane ySplit=""1"" topLeftCell=""A2"" activePane=""bottomLeft"" state=""frozen""/></sheetView></sheetViews>")
        If columnCount > 0 Then
            sb.Append("<cols>")
            For c = 0 To columnCount - 1
                Dim w = Math.Min(60, Math.Max(10, widths(c) + 3))
                sb.Append("<col min=""" & (c + 1) & """ max=""" & (c + 1) & """ width=""" & w & """ customWidth=""1""/>")
            Next
            sb.Append("</cols>")
        End If
        sb.Append("<sheetData>")
        sb.Append("<row r=""1"">")
        For c = 0 To sheet.Header.Length - 1
            sb.Append(TextCell(ColumnName(c) & "1", sheet.Header(c), 1))
        Next
        sb.Append("</row>")
        For r = 0 To sheet.Rows.Count - 1
            Dim rowNumber = r + 2
            sb.Append("<row r=""" & rowNumber & """>")
            Dim values = sheet.Rows(r)
            For c = 0 To values.Length - 1
                Dim reference = ColumnName(c) & rowNumber
                Dim v = values(c)
                If v Is Nothing Then Continue For
                If TypeOf v Is ExcelMoney Then
                    sb.Append("<c r=""" & reference & """ s=""2""><v>" & DirectCast(v, ExcelMoney).Value.ToString(CultureInfo.InvariantCulture) & "</v></c>")
                ElseIf TypeOf v Is Integer OrElse TypeOf v Is Long OrElse TypeOf v Is Decimal OrElse TypeOf v Is Double OrElse TypeOf v Is Single Then
                    sb.Append("<c r=""" & reference & """><v>" & Convert.ToString(v, CultureInfo.InvariantCulture) & "</v></c>")
                Else
                    sb.Append(TextCell(reference, Convert.ToString(v, CultureInfo.InvariantCulture), 0))
                End If
            Next
            sb.Append("</row>")
        Next
        sb.Append("</sheetData></worksheet>")
        Return sb.ToString()
    End Function

    Private Function TextCell(reference As String, text As String, style As Integer) As String
        Return "<c r=""" & reference & """ t=""inlineStr""" & If(style > 0, " s=""" & style & """", "") & "><is><t xml:space=""preserve"">" & Escape(text) & "</t></is></c>"
    End Function

    Private Function DisplayText(v As Object) As String
        If v Is Nothing Then Return ""
        If TypeOf v Is ExcelMoney Then Return "GH₵ " & DirectCast(v, ExcelMoney).Value.ToString("N2", CultureInfo.InvariantCulture)
        Return Convert.ToString(v, CultureInfo.InvariantCulture)
    End Function

    Private Function ColumnName(index As Integer) As String
        Dim name = ""
        Dim n = index + 1
        While n > 0
            Dim remainder = (n - 1) Mod 26
            name = ChrW(65 + remainder) & name
            n = (n - 1) \ 26
        End While
        Return name
    End Function

    Private Function Escape(text As String) As String
        If text Is Nothing Then Return ""
        Dim clean As New StringBuilder(text.Length)
        For Each ch In text
            Dim code = AscW(ch)
            If code = 9 OrElse code = 10 OrElse code = 13 OrElse code >= 32 Then clean.Append(ch)
        Next
        Return clean.ToString().Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("""", "&quot;")
    End Function

    Private Function UniqueSheetNames(sheets As IList(Of ExcelSheet)) As List(Of String)
        Dim result As New List(Of String)()
        For Each sheet In sheets
            Dim name = If(sheet.Name, "Sheet")
            For Each bad In "[]:*?/\".ToCharArray()
                name = name.Replace(bad, " "c)
            Next
            name = name.Trim()
            If name = "" Then name = "Sheet"
            If name.Length > 28 Then name = name.Substring(0, 28)
            Dim candidate = name
            Dim n = 2
            While result.Any(Function(x) String.Equals(x, candidate, StringComparison.OrdinalIgnoreCase))
                candidate = name & " " & n
                n += 1
            End While
            result.Add(candidate)
        Next
        Return result
    End Function

    ' ---------- Minimal ZIP writer (stored entries, which Excel reads fine) ----------
    Private ReadOnly CrcTable As UInteger() = BuildCrcTable()

    Private Function BuildCrcTable() As UInteger()
        Dim table(255) As UInteger
        For n = 0 To 255
            Dim c = CUInt(n)
            For k = 0 To 7
                If (c And 1UI) <> 0UI Then
                    c = &HEDB88320UI Xor (c >> 1)
                Else
                    c = c >> 1
                End If
            Next
            table(n) = c
        Next
        Return table
    End Function

    Private Function Crc32(data As Byte()) As UInteger
        Dim c = &HFFFFFFFFUI
        For Each b In data
            c = CrcTable(CInt((c Xor b) And &HFFUI)) Xor (c >> 8)
        Next
        Return c Xor &HFFFFFFFFUI
    End Function

    Private Sub WriteZip(path As String, entries As List(Of KeyValuePair(Of String, Byte())))
        Using fs = File.Create(path), bw As New BinaryWriter(fs)
            Dim offsets As New List(Of UInteger)()
            Dim crcs As New List(Of UInteger)()
            For Each entry In entries
                Dim nameBytes = Encoding.UTF8.GetBytes(entry.Key)
                Dim crc = Crc32(entry.Value)
                offsets.Add(CUInt(fs.Position))
                crcs.Add(crc)
                bw.Write(&H4034B50UI)
                bw.Write(CUShort(20))
                bw.Write(CUShort(&H800))
                bw.Write(CUShort(0))
                bw.Write(CUShort(0))
                bw.Write(CUShort(&H21))
                bw.Write(crc)
                bw.Write(CUInt(entry.Value.Length))
                bw.Write(CUInt(entry.Value.Length))
                bw.Write(CUShort(nameBytes.Length))
                bw.Write(CUShort(0))
                bw.Write(nameBytes)
                bw.Write(entry.Value)
            Next
            Dim directoryStart = CUInt(fs.Position)
            For i = 0 To entries.Count - 1
                Dim nameBytes = Encoding.UTF8.GetBytes(entries(i).Key)
                bw.Write(&H2014B50UI)
                bw.Write(CUShort(20))
                bw.Write(CUShort(20))
                bw.Write(CUShort(&H800))
                bw.Write(CUShort(0))
                bw.Write(CUShort(0))
                bw.Write(CUShort(&H21))
                bw.Write(crcs(i))
                bw.Write(CUInt(entries(i).Value.Length))
                bw.Write(CUInt(entries(i).Value.Length))
                bw.Write(CUShort(nameBytes.Length))
                bw.Write(CUShort(0))
                bw.Write(CUShort(0))
                bw.Write(CUShort(0))
                bw.Write(CUShort(0))
                bw.Write(0UI)
                bw.Write(offsets(i))
                bw.Write(nameBytes)
            Next
            Dim directorySize = CUInt(fs.Position) - directoryStart
            bw.Write(&H6054B50UI)
            bw.Write(CUShort(0))
            bw.Write(CUShort(0))
            bw.Write(CUShort(entries.Count))
            bw.Write(CUShort(entries.Count))
            bw.Write(directorySize)
            bw.Write(directoryStart)
            bw.Write(CUShort(0))
        End Using
    End Sub
End Module
