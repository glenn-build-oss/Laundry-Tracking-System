Option Strict On

Imports System.Collections.Generic
Imports System.Drawing.Drawing2D
Imports System.Drawing.Text
Imports System.Linq

' =====================================================================
'  Modern Professional Blue Theme
'  Colours, fonts, icons and the custom-drawn controls used by the app.
' =====================================================================

Public Enum ButtonKind
    Primary
    Secondary
    Success
    Danger
End Enum

Public Module Theme
    ' ---------- Colours ----------
    Public ReadOnly Navy As Color = ColorTranslator.FromHtml("#0B1F3A")
    Public ReadOnly NavHover As Color = ColorTranslator.FromHtml("#16325C")
    Public ReadOnly Blue As Color = ColorTranslator.FromHtml("#0875E1")
    Public ReadOnly BlueHover As Color = ColorTranslator.FromHtml("#0A63BD")
    Public ReadOnly BlueLight As Color = ColorTranslator.FromHtml("#DCEBFF")
    Public ReadOnly Background As Color = ColorTranslator.FromHtml("#F4F7FC")
    Public ReadOnly Green As Color = ColorTranslator.FromHtml("#16A765")
    Public ReadOnly GreenHover As Color = ColorTranslator.FromHtml("#128A53")
    Public ReadOnly Amber As Color = ColorTranslator.FromHtml("#FFB000")
    Public ReadOnly Red As Color = ColorTranslator.FromHtml("#E53935")
    Public ReadOnly RedHover As Color = ColorTranslator.FromHtml("#C62F2B")
    Public ReadOnly Purple As Color = ColorTranslator.FromHtml("#7C5CFC")
    Public ReadOnly Teal As Color = ColorTranslator.FromHtml("#0EA5B7")
    Public ReadOnly Orange As Color = ColorTranslator.FromHtml("#F97316")
    Public ReadOnly Slate As Color = ColorTranslator.FromHtml("#64748B")
    Public ReadOnly TextDark As Color = ColorTranslator.FromHtml("#14213D")
    Public ReadOnly Muted As Color = ColorTranslator.FromHtml("#5B6577")
    Public ReadOnly Border As Color = ColorTranslator.FromHtml("#DDE3EE")
    Public ReadOnly RowAlt As Color = ColorTranslator.FromHtml("#F7F9FD")
    Public ReadOnly SoftRed As Color = ColorTranslator.FromHtml("#FF8A85")

    ' ---------- Icons (Segoe MDL2 Assets / Segoe Fluent Icons, built into Windows 10/11) ----------
    Public ReadOnly IconFontName As String = PickIconFont()
    Public ReadOnly GlyphHome As String = ChrW(&HE80F)
    Public ReadOnly GlyphAdd As String = ChrW(&HE710)
    Public ReadOnly GlyphSync As String = ChrW(&HE895)
    Public ReadOnly GlyphList As String = ChrW(&HE8FD)
    Public ReadOnly GlyphPeople As String = ChrW(&HE716)
    Public ReadOnly GlyphPayment As String = ChrW(&HE8C7)
    Public ReadOnly GlyphChart As String = ChrW(&HE9D2)
    Public ReadOnly GlyphContact As String = ChrW(&HE77B)
    Public ReadOnly GlyphSettings As String = ChrW(&HE713)
    Public ReadOnly GlyphSignOut As String = ChrW(&HF3B1)
    Public ReadOnly GlyphCart As String = ChrW(&HE7BF)
    Public ReadOnly GlyphClock As String = ChrW(&HE823)
    Public ReadOnly GlyphCheck As String = ChrW(&HE73E)
    Public ReadOnly GlyphWarning As String = ChrW(&HE7BA)
    Public ReadOnly GlyphPrint As String = ChrW(&HE749)
    Public ReadOnly GlyphSave As String = ChrW(&HE74E)
    Public ReadOnly GlyphDelete As String = ChrW(&HE74D)
    Public ReadOnly GlyphMessage As String = ChrW(&HE8BD)
    Public ReadOnly GlyphDownload As String = ChrW(&HE896)
    Public ReadOnly GlyphForward As String = ChrW(&HE72A)
    Public ReadOnly GlyphLock As String = ChrW(&HE72E)
    Public ReadOnly GlyphFolder As String = ChrW(&HE8B7)
    Public ReadOnly GlyphRefresh As String = ChrW(&HE72C)

    Private Function PickIconFont() As String
        For Each name In New String() {"Segoe Fluent Icons", "Segoe MDL2 Assets"}
            Using f As New Font(name, 10.0F)
                If String.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase) Then Return name
            End Using
        Next
        Return "Segoe UI Symbol"
    End Function

    ' ---------- Fonts ----------
    Public Function UiFont(size As Single, Optional style As FontStyle = FontStyle.Regular) As Font
        Return New Font("Segoe UI", size, style)
    End Function

    Public Function SemiFont(size As Single) As Font
        Return New Font("Segoe UI Semibold", size)
    End Function

    ' ---------- Drawing helpers ----------
    Public Function RoundedRect(r As Rectangle, radius As Integer) As GraphicsPath
        Dim path As New GraphicsPath()
        Dim d = Math.Max(2, Math.Min(radius * 2, Math.Min(r.Width, r.Height)))
        path.AddArc(r.X, r.Y, d, d, 180, 90)
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        path.CloseFigure()
        Return path
    End Function

    Public Function GlyphBitmap(glyph As String, color As Color, px As Integer) As Bitmap
        Dim bmp As New Bitmap(px, px)
        Using g = Graphics.FromImage(bmp),
              f As New Font(IconFontName, px * 0.7F, GraphicsUnit.Pixel),
              b As New SolidBrush(color),
              sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
            g.DrawString(glyph, f, b, New RectangleF(0, 0, px, px), sf)
        End Using
        Return bmp
    End Function

    ' Background and text colours for status / payment pills.
    Public Function PillColors(text As String) As Tuple(Of Color, Color)
        Select Case text
            Case OrderStatus.Pending, "Part paid"
                Return Tuple.Create(ColorTranslator.FromHtml("#FFF3D6"), ColorTranslator.FromHtml("#8A5A00"))
            Case OrderStatus.Washing, OrderStatus.Ironing, "Administrator"
                Return Tuple.Create(ColorTranslator.FromHtml("#E2EEFC"), ColorTranslator.FromHtml("#0757A8"))
            Case OrderStatus.Ready, "Paid", "Active"
                Return Tuple.Create(ColorTranslator.FromHtml("#E3F6EC"), ColorTranslator.FromHtml("#0E6B41"))
            Case "Unpaid", "Overdue"
                Return Tuple.Create(ColorTranslator.FromHtml("#FDE7E6"), ColorTranslator.FromHtml("#B3261E"))
            Case Else
                Return Tuple.Create(ColorTranslator.FromHtml("#EEF0F3"), ColorTranslator.FromHtml("#4B5563"))
        End Select
    End Function

    ' ---------- Buttons ----------
    Public Function MakeButton(text As String, kind As ButtonKind, Optional glyph As String = "") As Button
        Dim b As New Button With {
            .Text = text,
            .FlatStyle = FlatStyle.Flat,
            .Font = SemiFont(9.75F),
            .Height = 40,
            .Cursor = Cursors.Hand,
            .TextImageRelation = TextImageRelation.ImageBeforeText,
            .UseVisualStyleBackColor = False
        }
        Dim fore As Color = Color.White
        Select Case kind
            Case ButtonKind.Primary
                b.BackColor = Blue
                b.FlatAppearance.MouseOverBackColor = BlueHover
            Case ButtonKind.Success
                b.BackColor = Green
                b.FlatAppearance.MouseOverBackColor = GreenHover
            Case ButtonKind.Danger
                b.BackColor = Red
                b.FlatAppearance.MouseOverBackColor = RedHover
            Case Else
                b.BackColor = Color.White
                fore = Blue
                b.FlatAppearance.MouseOverBackColor = BlueLight
        End Select
        b.ForeColor = fore
        b.FlatAppearance.BorderSize = If(kind = ButtonKind.Secondary, 1, 0)
        b.FlatAppearance.BorderColor = Blue
        If glyph <> "" Then b.Image = GlyphBitmap(glyph, fore, 18)
        b.Width = TextRenderer.MeasureText(text, b.Font).Width + If(glyph <> "", 54, 36)
        Return b
    End Function

    ' ---------- Tables ----------
    Public Function MakeGrid() As DataGridView
        Dim g As New DataGridView With {
            .AllowUserToAddRows = False,
            .AllowUserToDeleteRows = False,
            .AllowUserToResizeRows = False,
            .ReadOnly = True,
            .MultiSelect = False,
            .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            .BackgroundColor = Color.White,
            .BorderStyle = BorderStyle.None,
            .CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            .GridColor = Border,
            .RowHeadersVisible = False,
            .EnableHeadersVisualStyles = False,
            .ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
            .ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
            .ColumnHeadersHeight = 40
        }
        g.ColumnHeadersDefaultCellStyle.BackColor = Navy
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = Navy
        g.ColumnHeadersDefaultCellStyle.Font = SemiFont(9.5F)
        g.ColumnHeadersDefaultCellStyle.Padding = New Padding(6, 0, 0, 0)
        g.DefaultCellStyle.BackColor = Color.White
        g.DefaultCellStyle.ForeColor = TextDark
        g.DefaultCellStyle.Font = UiFont(9.75F)
        g.DefaultCellStyle.SelectionBackColor = BlueLight
        g.DefaultCellStyle.SelectionForeColor = TextDark
        g.DefaultCellStyle.Padding = New Padding(6, 0, 0, 0)
        g.DefaultCellStyle.WrapMode = DataGridViewTriState.False
        g.AlternatingRowsDefaultCellStyle.BackColor = RowAlt
        g.RowTemplate.Height = 38
        Return g
    End Function

    ' Draws the named columns as rounded coloured pills (Pending, Ready, Paid ...).
    Public Sub AttachPills(grid As DataGridView, ParamArray columnNames As String())
        Dim names = New HashSet(Of String)(columnNames)
        AddHandler grid.CellPainting,
            Sub(sender As Object, e As DataGridViewCellPaintingEventArgs)
                If e.RowIndex < 0 OrElse e.ColumnIndex < 0 Then Return
                If Not names.Contains(grid.Columns(e.ColumnIndex).Name) Then Return
                e.PaintBackground(e.CellBounds, True)
                Dim text = If(e.Value Is Nothing, "", e.Value.ToString())
                If text <> "" Then
                    Dim colors = PillColors(text)
                    e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
                    Using f = SemiFont(8.5F)
                        Dim w = Math.Min(TextRenderer.MeasureText(text, f).Width + 16, e.CellBounds.Width - 10)
                        Dim r As New Rectangle(e.CellBounds.X + 6, e.CellBounds.Y + (e.CellBounds.Height - 24) \ 2, w, 24)
                        Using path = RoundedRect(r, 12), brush As New SolidBrush(colors.Item1)
                            e.Graphics.FillPath(brush, path)
                        End Using
                        TextRenderer.DrawText(e.Graphics, text, f, r, colors.Item2, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.EndEllipsis)
                    End Using
                End If
                e.Handled = True
            End Sub
    End Sub
End Module

' =====================================================================
'  A white card with rounded corners and a soft border.
' =====================================================================
Public Class RoundedPanel
    Inherits Panel

    Public Property Radius As Integer = 14
    Public Property FillColor As Color = Color.White
    Public Property BorderColor As Color = Theme.Border

    Public Sub New()
        DoubleBuffered = True
        ResizeRedraw = True
        BackColor = Color.White
    End Sub

    Protected Overrides Sub OnPaintBackground(e As PaintEventArgs)
        Dim outside = If(Parent IsNot Nothing, Parent.BackColor, Theme.Background)
        e.Graphics.Clear(outside)
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        Using path = Theme.RoundedRect(r, Radius), brush As New SolidBrush(FillColor), pen As New Pen(BorderColor)
            e.Graphics.FillPath(brush, path)
            e.Graphics.DrawPath(pen, path)
        End Using
    End Sub
End Class

' =====================================================================
'  A coloured square or circle with a white icon (or initials) inside.
' =====================================================================
Public Class IconTile
    Inherits Control

    Public Property Glyph As String = ""
    Public Property TileColor As Color = Theme.Blue
    Public Property GlyphColor As Color = Color.White
    Public Property Radius As Integer = 10
    Public Property Round As Boolean
    Public Property TextMode As Boolean   ' True = draw Glyph as normal text (initials)

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        Size = New Size(48, 48)
    End Sub

    Public Sub Redraw()
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit
        Dim r As New Rectangle(0, 0, Width - 1, Height - 1)
        Using brush As New SolidBrush(TileColor)
            If Round Then
                g.FillEllipse(brush, r)
            Else
                Using path = Theme.RoundedRect(r, Radius)
                    g.FillPath(brush, path)
                End Using
            End If
        End Using
        Dim fontName = If(TextMode, "Segoe UI Semibold", Theme.IconFontName)
        Dim size = CSng(Math.Max(7.0, Height * If(TextMode, 0.34, 0.42)))
        Using f As New Font(fontName, size, FontStyle.Regular, GraphicsUnit.Pixel),
              tb As New SolidBrush(GlyphColor),
              sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            g.DrawString(Glyph, f, tb, New RectangleF(0, 0, Width, Height), sf)
        End Using
    End Sub
End Class

' =====================================================================
'  Sidebar menu item: coloured icon tile + label, blue when selected.
' =====================================================================
Public Class NavButton
    Inherits Control

    Private selectedValue As Boolean
    Private hovering As Boolean
    Private ReadOnly normalFont As Font = Theme.UiFont(10.5F)
    Private ReadOnly boldFont As Font = Theme.UiFont(10.5F, FontStyle.Bold)

    Public Property Glyph As String = ""
    Public Property IconColor As Color = Theme.Blue
    Public Property IsDanger As Boolean

    Public Property Selected As Boolean
        Get
            Return selectedValue
        End Get
        Set(value As Boolean)
            selectedValue = value
            Invalidate()
        End Set
    End Property

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        Cursor = Cursors.Hand
        Size = New Size(208, 44)
    End Sub

    Protected Overrides Sub OnMouseEnter(e As EventArgs)
        hovering = True
        Invalidate()
        MyBase.OnMouseEnter(e)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        hovering = False
        Invalidate()
        MyBase.OnMouseLeave(e)
    End Sub

    Protected Overrides Sub OnTextChanged(e As EventArgs)
        Invalidate()
        MyBase.OnTextChanged(e)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit

        Dim fill As Color = BackColor
        If selectedValue Then
            fill = Theme.Blue
        ElseIf hovering Then
            fill = If(IsDanger, Color.FromArgb(60, Theme.Red), Theme.NavHover)
        End If
        If fill <> BackColor Then
            Using path = Theme.RoundedRect(New Rectangle(0, 0, Width - 1, Height - 1), 10), b As New SolidBrush(fill)
                g.FillPath(b, path)
            End Using
        End If

        Dim tileRect As New Rectangle(10, (Height - 30) \ 2, 30, 30)
        Dim tileColor = If(selectedValue, Color.FromArgb(55, 255, 255, 255), If(IsDanger, Theme.Red, IconColor))
        Using path = Theme.RoundedRect(tileRect, 8), b As New SolidBrush(tileColor)
            g.FillPath(b, path)
        End Using
        Using f As New Font(Theme.IconFontName, 13.0F, FontStyle.Regular, GraphicsUnit.Pixel),
              sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center}
            g.DrawString(Glyph, f, Brushes.White, New RectangleF(tileRect.X, tileRect.Y, tileRect.Width, tileRect.Height), sf)
        End Using

        Dim textColor = If(IsDanger AndAlso Not selectedValue, Theme.SoftRed, Color.White)
        TextRenderer.DrawText(g, Text, If(selectedValue, boldFont, normalFont), New Rectangle(52, 0, Width - 56, Height), textColor,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.Left Or TextFormatFlags.EndEllipsis)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            normalFont.Dispose()
            boldFont.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class

' =====================================================================
'  Simple rounded bar chart (money received per day / month).
' =====================================================================
Public Class BarChart
    Inherits Control

    Private labels As New List(Of String)()
    Private values As New List(Of Decimal)()
    Private highlight As Integer = -1

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.UserPaint Or ControlStyles.ResizeRedraw, True)
        Font = Theme.UiFont(8.5F)
    End Sub

    Public Sub SetData(newLabels As IEnumerable(Of String), newValues As IEnumerable(Of Decimal), highlightIndex As Integer)
        labels = newLabels.ToList()
        values = newValues.ToList()
        highlight = highlightIndex
        Invalidate()
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        If values.Count = 0 Then Return

        Dim maxValue = values.Max()
        If maxValue <= 0D Then maxValue = 1D
        Const topPad As Integer = 22
        Const bottomPad As Integer = 26
        Dim chartHeight = Math.Max(10, Height - topPad - bottomPad)
        Dim slot = Width / values.Count
        Dim barWidth = CInt(Math.Max(4.0, Math.Min(46.0, slot * 0.6)))
        Dim showValues = values.Count <= 12
        Dim labelStep = CInt(Math.Ceiling(values.Count / 12.0))
        Dim pale = ColorTranslator.FromHtml("#BBD6F7")

        Using baseline As New Pen(Theme.Border)
            g.DrawLine(baseline, 0, topPad + chartHeight, Width, topPad + chartHeight)
        End Using

        For i = 0 To values.Count - 1
            Dim h = CInt(CDbl(values(i)) / CDbl(maxValue) * chartHeight)
            Dim x = CInt(slot * i + (slot - barWidth) / 2)
            Dim y = topPad + chartHeight - h
            If h > 0 Then
                Using b As New SolidBrush(If(i = highlight, Theme.Blue, pale))
                    If h >= 8 Then
                        Using path = Theme.RoundedRect(New Rectangle(x, y, barWidth, h), Math.Min(6, barWidth \ 2))
                            g.FillPath(b, path)
                        End Using
                    Else
                        g.FillRectangle(b, x, y, barWidth, h)
                    End If
                End Using
            End If
            Dim slotRect As New Rectangle(CInt(slot * i), 0, CInt(Math.Ceiling(slot)), 0)
            If showValues Then
                TextRenderer.DrawText(g, "₵" & values(i).ToString("N0"), Font, New Rectangle(slotRect.X, y - 20, slotRect.Width, 18), Theme.Muted, TextFormatFlags.HorizontalCenter)
            End If
            If i < labels.Count AndAlso (i Mod labelStep = 0 OrElse i = highlight) Then
                TextRenderer.DrawText(g, labels(i), Font, New Rectangle(slotRect.X, topPad + chartHeight + 6, slotRect.Width, 18), If(i = highlight, Theme.TextDark, Theme.Muted), TextFormatFlags.HorizontalCenter)
            End If
        Next
    End Sub
End Class
