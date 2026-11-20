Option Strict On

Imports System.Collections.Generic
Imports System.Drawing.Printing
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text

' =====================================================================
'  Laundry Tracking System - main window
'  Login screen, navy sidebar, header with live clock, and 9 pages.
' =====================================================================
Public Class Form1
    Private Const ContentWidth As Integer = 1040

    ' Page order (must match the order the pages are built in BuildShell)
    Private Const PageDashboard As Integer = 0
    Private Const PageNewOrder As Integer = 1
    Private Const PageStatus As Integer = 2
    Private Const PageOrders As Integer = 3
    Private Const PageCustomers As Integer = 4
    Private Const PagePayments As Integer = 5
    Private Const PageReports As Integer = 6
    Private Const PageWorkers As Integer = 7
    Private Const PageSettings As Integer = 8

    Private Const TopLeft As AnchorStyles = AnchorStyles.Top Or AnchorStyles.Left
    Private Const TopRight As AnchorStyles = AnchorStyles.Top Or AnchorStyles.Right
    Private Const TopLeftRight As AnchorStyles = AnchorStyles.Top Or AnchorStyles.Left Or AnchorStyles.Right
    Private Const AllSides As AnchorStyles = AnchorStyles.Top Or AnchorStyles.Bottom Or AnchorStyles.Left Or AnchorStyles.Right

    Private currentUser As StaffUser
    Private logoImage As Image
    Private ReadOnly pages As New List(Of Panel)()
    Private ReadOnly pageTitles As New List(Of String)()
    Private ReadOnly pageRefreshers As New List(Of Action)()
    Private ReadOnly navButtons As New List(Of NavButton)()
    Private contentHost As Panel
    Private lblPageTitle As Label
    Private lblGreeting As Label
    Private lblClockTime As Label
    Private lblClockDate As Label
    Private clockTimer As System.Windows.Forms.Timer
    Private currentPage As Integer = -1
    Private suppressEvents As Boolean

    ' Dashboard
    Private dashActive, dashReady, dashProgress, dashOverdue As Label
    Private dashSumReceived, dashSumCollected, dashSumMoney, dashSumOutstanding, dashWeekTotal As Label
    Private dashRecentGrid, dashReadyGrid As DataGridView
    Private dashChart As BarChart

    ' New order
    Private noPhone, noName, noItems, noNotes, noPaid As TextBox
    Private noService, noMethod As ComboBox
    Private noWeight As NumericUpDown
    Private noDue As DateTimePicker
    Private noHint, noSumService, noSumWeight, noSumBand, noSumTotal, noSumBalance As Label

    ' Update status
    Private usSearch As TextBox
    Private usShowCollected As CheckBox
    Private usGrid As DataGridView
    Private usOrderLabel, usStatusLabel, usCustomerLabel, usDetailsLabel, usDueLabel, usBalanceLabel As Label
    Private usNextButton, usUpdateButton As Button
    Private usStatusCombo As ComboBox
    Private usHistory As ListBox
    Private usSelectedId As String = ""
    Private pendingStatusOrderId As String = ""

    ' Order list
    Private olSearch As TextBox
    Private olStatus As ComboBox
    Private olFrom, olTo As DateTimePicker
    Private olGrid As DataGridView
    Private olCount As Label
    Private olLastList As New List(Of LaundryOrder)()

    ' Customers
    Private cuSearch, cuName, cuPhone, cuNotes As TextBox
    Private cuGrid, cuOrdersGrid As DataGridView
    Private cuTitle As Label
    Private cuSelectedId As Integer

    ' Payments
    Private pyOrder, pyMethod As ComboBox
    Private pyAmount As TextBox
    Private pyBalanceLabel As Label
    Private pyOutGrid, pyHistGrid As DataGridView

    ' Reports
    Private rpRange As ComboBox
    Private rpOrders, rpValue, rpReceived, rpOutstanding, rpChartTitle As Label
    Private rpChart As BarChart
    Private rpServiceGrid, rpMethodGrid As DataGridView

    ' Workers
    Private wkName, wkPhone, wkPassword As TextBox
    Private wkRole As ComboBox
    Private wkGrid As DataGridView

    ' Settings
    Private stName, stAddress, stPhone, stTagline, stCurrentPw, stNewPw, stConfirmPw As TextBox
    Private stServicesGrid As DataGridView
    Private stDataLabel As Label
    Private stCollectionDays As NumericUpDown
    Private stBusinessCard, stServicesCard, stDataCard As RoundedPanel

    Private ReadOnly Property Db As AppData
        Get
            Return DataStore.Data
        End Get
    End Property

    ' =================================================================
    '  Start-up and closing
    ' =================================================================
    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        DataStore.LoadData()
        DataStore.AutoBackup()
        BuildLoginScreen()
    End Sub

    Private Sub Form1_FormClosing(sender As Object, e As FormClosingEventArgs) Handles MyBase.FormClosing
        DataStore.SaveData()
    End Sub

    ' =================================================================
    '  Logo  (Images\logo.png next to the .exe)
    ' =================================================================
    Private Function GetLogo() As Image
        If logoImage IsNot Nothing Then Return logoImage
        Dim logoPath = Path.Combine(Application.StartupPath, "Images", "logo.png")
        If Not File.Exists(logoPath) Then Return Nothing
        Try
            Using stream As New MemoryStream(File.ReadAllBytes(logoPath))
                Using temp = Image.FromStream(stream)
                    logoImage = New Bitmap(temp)
                End Using
            End Using
        Catch
            logoImage = Nothing
        End Try
        Return logoImage
    End Function

    Private Function CreateLogo(location As Point, size As Size, fallbackFontSize As Single) As Control
        Dim logo = GetLogo()
        If logo IsNot Nothing Then
            Return New PictureBox With {.Image = logo, .SizeMode = PictureBoxSizeMode.Zoom, .Location = location, .Size = size}
        End If
        Return New Label With {.Text = "Laundry" & Environment.NewLine & "Tracking System", .Font = UiFont(fallbackFontSize, FontStyle.Bold),
                               .ForeColor = Theme.Navy, .Location = location, .Size = size, .TextAlign = ContentAlignment.MiddleCenter}
    End Function

    ' =================================================================
    '  Login screen
    ' =================================================================
    Private Sub BuildLoginScreen()
        If clockTimer IsNot Nothing Then clockTimer.Stop()
        SuspendLayout()
        Controls.Clear()
        pages.Clear()
        pageTitles.Clear()
        pageRefreshers.Clear()
        navButtons.Clear()
        currentUser = Nothing
        currentPage = -1
        Text = "Laundry Tracking System - Login"
        WindowState = FormWindowState.Normal
        MinimumSize = Size.Empty
        FormBorderStyle = FormBorderStyle.FixedSingle
        MaximizeBox = False
        ClientSize = New Size(940, 560)
        BackColor = Color.White
        Font = UiFont(9.75F)
        DoubleBuffered = True

        Dim formSide As New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.Navy}
        Dim brandSide As New Panel With {.Dock = DockStyle.Left, .Width = 480, .BackColor = Color.White}
        Controls.Add(formSide)
        Controls.Add(brandSide)
        formSide.BringToFront()

        ' ----- Left: logo and three feature tiles -----
        brandSide.Controls.Add(CreateLogo(New Point(40, 50), New Size(400, 230), 26.0F))
        Dim features = {Tuple.Create("Log every order", Theme.GlyphCart, Theme.Blue),
                        Tuple.Create("Track each stage", Theme.GlyphClock, Theme.Amber),
                        Tuple.Create("Hand over on time", Theme.GlyphCheck, Theme.Green)}
        For i = 0 To features.Length - 1
            Dim tile As New RoundedPanel With {.Location = New Point(40 + i * 136, 320), .Size = New Size(128, 112), .FillColor = Theme.Background, .BorderColor = Theme.Background, .BackColor = Theme.Background}
            tile.Controls.Add(New IconTile With {.Glyph = features(i).Item2, .TileColor = features(i).Item3, .Size = New Size(42, 42), .Location = New Point(43, 16)})
            tile.Controls.Add(New Label With {.Text = features(i).Item1, .Font = SemiFont(9.0F), .ForeColor = Theme.TextDark, .AutoSize = False, .Size = New Size(120, 36), .Location = New Point(4, 66), .TextAlign = ContentAlignment.MiddleCenter})
            brandSide.Controls.Add(tile)
        Next
        brandSide.Controls.Add(New Label With {.Text = Db.Business.Tagline, .Font = UiFont(10.0F, FontStyle.Italic), .ForeColor = Theme.Muted, .AutoSize = False, .Size = New Size(400, 24), .Location = New Point(40, 470), .TextAlign = ContentAlignment.MiddleCenter})

        ' ----- Right: login form -----
        formSide.Controls.Add(New Label With {.Text = "Welcome back", .Font = UiFont(22.0F, FontStyle.Bold), .ForeColor = Color.White, .AutoSize = True, .Location = New Point(56, 60)})
        formSide.Controls.Add(New Label With {.Text = "Choose your profile and enter your password.", .Font = UiFont(10.0F), .ForeColor = ColorTranslator.FromHtml("#AFC3E0"), .AutoSize = True, .Location = New Point(59, 108)})

        formSide.Controls.Add(New Label With {.Text = "Staff profile", .Font = SemiFont(9.75F), .ForeColor = Color.White, .AutoSize = True, .Location = New Point(58, 160)})
        Dim profileBox As New RoundedPanel With {.Location = New Point(58, 184), .Size = New Size(340, 52), .Radius = 12, .BorderColor = Color.White}
        Dim avatar As New IconTile With {.Round = True, .TextMode = True, .TileColor = Theme.Green, .Size = New Size(34, 34), .Location = New Point(10, 9)}
        Dim cmbProfile As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat, .Font = SemiFont(10.5F), .ForeColor = Theme.TextDark, .Location = New Point(52, 13), .Width = 278}
        For Each user In Db.Users.Where(Function(u) u.Active).OrderBy(Function(u) u.FullName)
            cmbProfile.Items.Add(New ComboItem(user.FullName & "  ·  " & user.Role, user.FullName))
        Next
        AddHandler cmbProfile.SelectedIndexChanged,
            Sub()
                Dim item = TryCast(cmbProfile.SelectedItem, ComboItem)
                avatar.Glyph = If(item Is Nothing, "?", Helpers.MakeInitials(item.Key))
                avatar.Redraw()
            End Sub
        If cmbProfile.Items.Count > 0 Then cmbProfile.SelectedIndex = 0
        profileBox.Controls.Add(avatar)
        profileBox.Controls.Add(cmbProfile)
        formSide.Controls.Add(profileBox)

        formSide.Controls.Add(New Label With {.Text = "Password", .Font = SemiFont(9.75F), .ForeColor = Color.White, .AutoSize = True, .Location = New Point(58, 256)})
        Dim passwordBox As New RoundedPanel With {.Location = New Point(58, 280), .Size = New Size(340, 52), .Radius = 12, .BorderColor = Color.White}
        passwordBox.Controls.Add(New IconTile With {.Glyph = Theme.GlyphLock, .TileColor = Theme.Blue, .Size = New Size(34, 34), .Location = New Point(10, 9), .Radius = 8})
        Dim txtPassword As New TextBox With {.BorderStyle = BorderStyle.None, .Font = UiFont(12.0F), .ForeColor = Theme.TextDark, .UseSystemPasswordChar = True, .Location = New Point(54, 15), .Width = 196}
        Dim btnShow = MakeButton("Show", ButtonKind.Secondary)
        btnShow.Size = New Size(72, 36)
        btnShow.Location = New Point(260, 8)
        btnShow.FlatAppearance.BorderSize = 0
        btnShow.BackColor = Theme.Background
        AddHandler btnShow.Click,
            Sub()
                txtPassword.UseSystemPasswordChar = Not txtPassword.UseSystemPasswordChar
                btnShow.Text = If(txtPassword.UseSystemPasswordChar, "Show", "Hide")
                txtPassword.Focus()
            End Sub
        passwordBox.Controls.Add(txtPassword)
        passwordBox.Controls.Add(btnShow)
        formSide.Controls.Add(passwordBox)

        Dim lblError As New Label With {.Font = SemiFont(9.5F), .ForeColor = Theme.SoftRed, .AutoSize = False, .Size = New Size(340, 22), .Location = New Point(58, 344), .Visible = False}
        formSide.Controls.Add(lblError)

        Dim btnLogin = MakeButton("Log in", ButtonKind.Primary, Theme.GlyphForward)
        btnLogin.Font = SemiFont(11.0F)
        btnLogin.Location = New Point(58, 376)
        btnLogin.Size = New Size(340, 52)
        AddHandler btnLogin.Click,
            Sub()
                Dim item = TryCast(cmbProfile.SelectedItem, ComboItem)
                If item Is Nothing Then Return
                Dim user = Db.Users.FirstOrDefault(Function(u) u.FullName = item.Key AndAlso u.Active)
                If user IsNot Nothing AndAlso DataStore.CheckPassword(user, txtPassword.Text) Then
                    currentUser = user
                    BuildShell()
                Else
                    lblError.Text = "That password doesn't match this profile. Try again."
                    lblError.Visible = True
                    txtPassword.Clear()
                    txtPassword.Focus()
                End If
            End Sub
        AddHandler txtPassword.TextChanged, Sub() lblError.Visible = False
        formSide.Controls.Add(btnLogin)

        formSide.Controls.Add(New Label With {.Text = Db.Business.Name, .Font = UiFont(8.5F), .ForeColor = ColorTranslator.FromHtml("#7F93B5"), .AutoSize = True, .Location = New Point(58, 500)})

        AcceptButton = btnLogin
        ResumeLayout(True)
        CenterToScreen()
        ActiveControl = txtPassword
    End Sub

    ' =================================================================
    '  Main window: sidebar + header + pages
    ' =================================================================
    Private Sub BuildShell()
        SuspendLayout()
        Controls.Clear()
        AcceptButton = Nothing
        Text = "Laundry Tracking System"
        FormBorderStyle = FormBorderStyle.Sizable
        MaximizeBox = True
        MinimumSize = New Size(1150, 720)
        ClientSize = New Size(1345, 860)
        BackColor = Theme.Background
        Font = UiFont(9.75F)

        Dim mainArea As New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.Background}
        Dim sidebar As New Panel With {.Dock = DockStyle.Left, .Width = 240, .BackColor = Theme.Navy}
        Controls.Add(mainArea)
        Controls.Add(sidebar)
        mainArea.BringToFront()

        contentHost = New Panel With {.Dock = DockStyle.Fill, .BackColor = Theme.Background, .Padding = New Padding(24, 20, 24, 20)}
        Dim header = BuildHeader()
        mainArea.Controls.Add(contentHost)
        mainArea.Controls.Add(header)
        contentHost.BringToFront()
        ResumeLayout(True)
        PerformLayout()

        BuildDashboardPage()
        BuildNewOrderPage()
        BuildStatusPage()
        BuildOrdersPage()
        BuildCustomersPage()
        BuildPaymentsPage()
        BuildReportsPage()
        BuildWorkersPage()
        BuildSettingsPage()
        BuildSidebar(sidebar)

        StartClock()
        CenterToScreen()
        WindowState = FormWindowState.Maximized
        ShowPage(PageDashboard)
    End Sub

    Private Function BuildHeader() As Panel
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 92, .BackColor = Color.White, .Width = Math.Max(800, ClientSize.Width - 240)}
        AddHandler header.Paint,
            Sub(s As Object, pe As PaintEventArgs)
                Using borderPen As New Pen(Theme.Border)
                    pe.Graphics.DrawLine(borderPen, 0, header.Height - 1, header.Width, header.Height - 1)
                End Using
            End Sub
        AddHandler header.Resize, Sub() header.Invalidate()

        header.Controls.Add(CreateLogo(New Point(24, 12), New Size(190, 68), 12.0F))
        lblPageTitle = New Label With {.AutoSize = True, .Font = UiFont(17.0F, FontStyle.Bold), .ForeColor = Theme.TextDark, .Location = New Point(236, 14)}
        lblGreeting = New Label With {.AutoSize = True, .Font = UiFont(10.0F), .ForeColor = Theme.Muted, .Location = New Point(238, 52)}
        header.Controls.Add(lblPageTitle)
        header.Controls.Add(lblGreeting)

        Dim clockBox As New RoundedPanel With {.Size = New Size(250, 64), .Location = New Point(header.Width - 274, 14), .Anchor = TopRight,
                                               .FillColor = Theme.Navy, .BorderColor = Theme.Navy, .BackColor = Theme.Navy, .Radius = 12}
        clockBox.Controls.Add(New IconTile With {.Glyph = Theme.GlyphClock, .TileColor = Theme.Blue, .Size = New Size(40, 40), .Location = New Point(12, 12)})
        lblClockTime = New Label With {.AutoSize = True, .Font = UiFont(16.0F, FontStyle.Bold), .ForeColor = Color.White, .Location = New Point(60, 6)}
        lblClockDate = New Label With {.AutoSize = True, .Font = UiFont(9.5F), .ForeColor = ColorTranslator.FromHtml("#C9D6EA"), .Location = New Point(62, 38)}
        clockBox.Controls.Add(lblClockTime)
        clockBox.Controls.Add(lblClockDate)
        header.Controls.Add(clockBox)
        Return header
    End Function

    Private Sub BuildSidebar(sidebar As Panel)
        sidebar.Controls.Add(New IconTile With {.Glyph = "L", .TextMode = True, .TileColor = Theme.Blue, .Size = New Size(44, 44), .Location = New Point(20, 22), .Radius = 12})
        sidebar.Controls.Add(New Label With {.Text = "Laundry", .Font = UiFont(16.0F, FontStyle.Bold), .ForeColor = Color.White, .AutoSize = True, .Location = New Point(72, 18)})
        sidebar.Controls.Add(New Label With {.Text = "Tracking System", .Font = SemiFont(9.0F), .ForeColor = ColorTranslator.FromHtml("#9CC4FF"), .AutoSize = True, .Location = New Point(74, 48)})
        sidebar.Controls.Add(New Panel With {.Location = New Point(16, 84), .Size = New Size(208, 1), .BackColor = ColorTranslator.FromHtml("#1E3A66")})

        Dim glyphs = {Theme.GlyphHome, Theme.GlyphAdd, Theme.GlyphSync, Theme.GlyphList, Theme.GlyphPeople, Theme.GlyphPayment, Theme.GlyphChart, Theme.GlyphContact, Theme.GlyphSettings}
        Dim colors = {Theme.Blue, Theme.Green, Theme.Amber, Theme.Purple, Theme.Teal, Theme.Green, Theme.Red, Theme.Orange, Theme.Slate}
        Dim y = 98
        For i = 0 To pages.Count - 1
            Dim pageIndex = i
            Dim nav As New NavButton With {.Text = pageTitles(i), .Glyph = glyphs(i), .IconColor = colors(i), .Location = New Point(16, y), .Size = New Size(208, 44)}
            AddHandler nav.Click, Sub() ShowPage(pageIndex)
            Dim allowed = currentUser.IsAdmin OrElse i <> PageWorkers
            nav.Visible = allowed
            If allowed Then y += 48
            sidebar.Controls.Add(nav)
            navButtons.Add(nav)
        Next

        Dim h = sidebar.Height
        sidebar.Controls.Add(New Panel With {.Location = New Point(16, h - 136), .Size = New Size(208, 1), .BackColor = ColorTranslator.FromHtml("#1E3A66"), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
        sidebar.Controls.Add(New IconTile With {.Glyph = currentUser.Initials, .TextMode = True, .Round = True, .TileColor = Theme.Green, .Size = New Size(40, 40), .Location = New Point(22, h - 120), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
        sidebar.Controls.Add(New Label With {.Text = currentUser.FullName, .Font = SemiFont(10.0F), .ForeColor = Color.White, .AutoSize = True, .Location = New Point(72, h - 120), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})
        sidebar.Controls.Add(New Label With {.Text = currentUser.Role, .Font = UiFont(9.0F), .ForeColor = ColorTranslator.FromHtml("#9FB3D1"), .AutoSize = True, .Location = New Point(73, h - 98), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left})

        Dim logout As New NavButton With {.Text = "Log out", .Glyph = Theme.GlyphSignOut, .IsDanger = True, .Location = New Point(16, h - 64), .Size = New Size(208, 44), .Anchor = AnchorStyles.Bottom Or AnchorStyles.Left}
        AddHandler logout.Click,
            Sub()
                If MessageBox.Show("Log out of the Laundry Tracking System?", "Log out", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes Then
                    DataStore.SaveData()
                    BuildLoginScreen()
                End If
            End Sub
        sidebar.Controls.Add(logout)
    End Sub

    Private Sub ShowPage(index As Integer)
        If index < 0 OrElse index >= pages.Count Then Return
        If index = PageWorkers AndAlso Not currentUser.IsAdmin Then Return
        For i = 0 To pages.Count - 1
            pages(i).Visible = (i = index)
            If i < navButtons.Count Then navButtons(i).Selected = (i = index)
        Next
        pages(index).BringToFront()
        lblPageTitle.Text = pageTitles(index)
        currentPage = index
        pageRefreshers(index).Invoke()
    End Sub

    Private Sub StartClock()
        If clockTimer Is Nothing Then
            clockTimer = New System.Windows.Forms.Timer With {.Interval = 1000}
            AddHandler clockTimer.Tick, Sub() UpdateClock()
        End If
        UpdateClock()
        clockTimer.Start()
    End Sub

    Private Sub UpdateClock()
        If lblClockTime Is Nothing OrElse currentUser Is Nothing Then Return
        Dim currentTime = DateTime.Now
        lblClockTime.Text = currentTime.ToString("hh:mm:ss tt", CultureInfo.InvariantCulture)
        lblClockDate.Text = currentTime.ToString("ddd, MMM dd, yyyy", CultureInfo.InvariantCulture)
        Dim greeting = If(currentTime.Hour < 12, "Good morning", If(currentTime.Hour < 17, "Good afternoon", "Good evening"))
        lblGreeting.Text = greeting & ", " & Helpers.FirstName(currentUser.FullName) & ".  " & Db.Business.Tagline
    End Sub

    ' =================================================================
    '  Small builders used by every page
    ' =================================================================
    Private Function NewPage(title As String, minHeight As Integer) As Panel
        Dim page As New Panel With {.Dock = DockStyle.Fill, .AutoScroll = True, .BackColor = Theme.Background}
        contentHost.Controls.Add(page)
        page.AutoScrollMinSize = New Size(ContentWidth, minHeight)
        pages.Add(page)
        pageTitles.Add(title)
        Return page
    End Function

    Private Function AddCard(parent As Control, x As Integer, y As Integer, w As Integer, h As Integer, title As String, anchor As AnchorStyles) As RoundedPanel
        Dim card As New RoundedPanel With {.Location = New Point(x, y), .Size = New Size(w, h), .anchor = anchor}
        parent.Controls.Add(card)
        If title <> "" Then
            card.Controls.Add(New Label With {.Text = title, .Font = UiFont(12.5F, FontStyle.Bold), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(20, 16)})
        End If
        Return card
    End Function

    Private Function AddCaption(parent As Control, text As String, x As Integer, y As Integer) As Label
        Dim l As New Label With {.Text = text, .Font = SemiFont(9.0F), .ForeColor = Theme.Muted, .AutoSize = True, .Location = New Point(x, y)}
        parent.Controls.Add(l)
        Return l
    End Function

    Private Function AddTextBox(parent As Control, caption As String, x As Integer, y As Integer, w As Integer, Optional multilineHeight As Integer = 0) As TextBox
        AddCaption(parent, caption, x, y)
        Dim tb As New TextBox With {.Location = New Point(x, y + 22), .Width = w, .BorderStyle = BorderStyle.FixedSingle, .Font = UiFont(10.5F), .ForeColor = Theme.TextDark}
        If multilineHeight > 0 Then
            tb.Multiline = True
            tb.Height = multilineHeight
            tb.ScrollBars = ScrollBars.Vertical
        End If
        parent.Controls.Add(tb)
        Return tb
    End Function

    Private Function AddCombo(parent As Control, caption As String, x As Integer, y As Integer, w As Integer) As ComboBox
        AddCaption(parent, caption, x, y)
        Dim cb As New ComboBox With {.Location = New Point(x, y + 22), .Width = w, .DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat, .Font = UiFont(10.5F), .ForeColor = Theme.TextDark}
        parent.Controls.Add(cb)
        Return cb
    End Function

    Private Function AddDatePicker(parent As Control, caption As String, x As Integer, y As Integer, w As Integer) As DateTimePicker
        AddCaption(parent, caption, x, y)
        Dim dp As New DateTimePicker With {.Location = New Point(x, y + 22), .Width = w, .Format = DateTimePickerFormat.Custom, .CustomFormat = "dd MMM yyyy", .Font = UiFont(10.5F)}
        parent.Controls.Add(dp)
        Return dp
    End Function

    Private Function AddGrid(parent As Control, x As Integer, y As Integer, w As Integer, h As Integer, anchor As AnchorStyles, ParamArray columns As String()) As DataGridView
        Dim grid = MakeGrid()
        grid.Location = New Point(x, y)
        grid.Size = New Size(w, h)
        grid.Anchor = anchor
        For Each c In columns
            Dim index = grid.Columns.Add(c, c)
            If c = "Status" Then grid.Columns(index).FillWeight = 160
            If c = "Order #" Then grid.Columns(index).FillWeight = 90
        Next
        parent.Controls.Add(grid)
        Return grid
    End Function

    Private Function AddButton(parent As Control, text As String, kind As ButtonKind, glyph As String, x As Integer, y As Integer, Optional width As Integer = 0) As Button
        Dim b = MakeButton(text, kind, glyph)
        b.Location = New Point(x, y)
        If width > 0 Then b.Width = width
        parent.Controls.Add(b)
        Return b
    End Function

    ' Places buttons right-to-left from the right edge of a card's header.
    Private Sub AddHeaderButtons(card As RoundedPanel, ParamArray buttons As Button())
        Dim right = card.Width - 20
        For Each b In buttons
            b.Location = New Point(right - b.Width, 12)
            b.Anchor = TopRight
            card.Controls.Add(b)
            right = b.Left - 10
        Next
    End Sub

    Private Function AddValueLabel(parent As Control, x As Integer, y As Integer, w As Integer, Optional bold As Boolean = False) As Label
        Dim l As New Label With {.AutoSize = False, .Size = New Size(w, 26), .Location = New Point(x, y), .TextAlign = ContentAlignment.MiddleRight,
                                 .Font = If(bold, SemiFont(11.0F), UiFont(10.5F)), .ForeColor = Theme.TextDark}
        parent.Controls.Add(l)
        Return l
    End Function

    Private Function MakeStatRow(page As Panel, y As Integer) As TableLayoutPanel
        Dim row As New TableLayoutPanel With {.Location = New Point(0, y), .Size = New Size(ContentWidth, 112), .ColumnCount = 4, .RowCount = 1, .BackColor = Theme.Background, .Anchor = TopLeftRight, .Margin = Padding.Empty}
        For i = 0 To 3
            row.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 25.0F))
        Next
        row.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        page.Controls.Add(row)
        Return row
    End Function

    Private Function AddStatCard(row As TableLayoutPanel, column As Integer, caption As String, glyph As String, color As Color) As Label
        Dim card As New RoundedPanel With {.Dock = DockStyle.Fill, .Margin = New Padding(If(column = 0, 0, 8), 0, If(column = 3, 0, 8), 0)}
        card.Controls.Add(New IconTile With {.glyph = glyph, .TileColor = color, .Size = New Size(54, 54), .Location = New Point(20, 28), .Radius = 14})
        Dim value As New Label With {.Text = "0", .Font = UiFont(22.0F, FontStyle.Bold), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(88, 20)}
        card.Controls.Add(value)
        card.Controls.Add(New Label With {.Text = caption, .Font = UiFont(10.0F), .ForeColor = Theme.Muted, .AutoSize = True, .Location = New Point(91, 62)})
        row.Controls.Add(card, column, 0)
        Return value
    End Function

    Private Function AddSummaryRow(card As RoundedPanel, y As Integer, caption As String, glyph As String, color As Color) As Label
        card.Controls.Add(New IconTile With {.glyph = glyph, .TileColor = color, .Size = New Size(34, 34), .Location = New Point(20, y), .Radius = 9})
        card.Controls.Add(New Label With {.Text = caption, .Font = UiFont(10.5F), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(64, y + 7)})
        Return AddValueLabel(card, 196, y + 4, 144, True)
    End Function

    Private Shared Function CellText(grid As DataGridView, rowIndex As Integer, column As String) As String
        If rowIndex < 0 OrElse rowIndex >= grid.Rows.Count Then Return ""
        Return Convert.ToString(grid.Rows(rowIndex).Cells(column).Value)
    End Function

    Private Shared Function CurrentText(grid As DataGridView, column As String) As String
        If grid.CurrentRow Is Nothing Then Return ""
        Return Convert.ToString(grid.CurrentRow.Cells(column).Value)
    End Function

    Private Sub SelectRow(grid As DataGridView, column As String, value As String)
        If String.IsNullOrEmpty(value) Then Return
        For Each row As DataGridViewRow In grid.Rows
            If Convert.ToString(row.Cells(column).Value) = value Then
                grid.ClearSelection()
                row.Selected = True
                grid.CurrentCell = row.Cells(0)
                Return
            End If
        Next
    End Sub

    Private Sub Warn(message As String)
        MessageBox.Show(message, "Laundry Tracking System", MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Private Sub Info(message As String)
        MessageBox.Show(message, "Laundry Tracking System", MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Private Function Confirm(message As String, title As String) As Boolean
        Return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
    End Function

    Private Shared Function MatchesSearch(o As LaundryOrder, term As String) As Boolean
        If term = "" Then Return True
        Return o.Id.ToLowerInvariant().Contains(term) OrElse o.CustomerName.ToLowerInvariant().Contains(term) OrElse
               Helpers.DigitsOnly(o.Phone).Contains(Helpers.DigitsOnly(term)) AndAlso Helpers.DigitsOnly(term) <> ""
    End Function

    Private Sub OpenOrderInStatusPage(id As String)
        Dim o = DataStore.FindOrder(id)
        If o Is Nothing Then Return
        pendingStatusOrderId = id
        If o.Status = OrderStatus.Collected AndAlso Not usShowCollected.Checked Then
            suppressEvents = True
            usShowCollected.Checked = True
            suppressEvents = False
        End If
        ShowPage(PageStatus)
    End Sub

    Private Function ReadyMessage(o As LaundryOrder) As String
        Dim text = "Hello " & Helpers.FirstName(o.CustomerName) & ", your laundry (" & o.Id & ") is ready for collection at " & Db.Business.Name & "."
        If o.Balance > 0D Then text &= " Balance to pay: " & Money(o.Balance) & "."
        Return text & " Thank you!"
    End Function

    Private Sub CopyReadyMessage(o As LaundryOrder)
        If o Is Nothing Then
            Warn("Select a customer in the list first.")
            Return
        End If
        Clipboard.SetText(ReadyMessage(o))
        Info("Message copied. Paste it into your SMS or WhatsApp app and send it to " & o.Phone & "." & Environment.NewLine & Environment.NewLine & ReadyMessage(o))
    End Sub

    ' =================================================================
    '  PAGE 0: Dashboard
    ' =================================================================
    Private Sub BuildDashboardPage()
        Dim page = NewPage("Dashboard", 830)
        Dim stats = MakeStatRow(page, 0)
        dashActive = AddStatCard(stats, 0, "Active orders", Theme.GlyphCart, Theme.Blue)
        dashReady = AddStatCard(stats, 1, "Ready for collection", Theme.GlyphCheck, Theme.Green)
        dashProgress = AddStatCard(stats, 2, "In progress", Theme.GlyphClock, Theme.Amber)
        dashOverdue = AddStatCard(stats, 3, "Overdue", Theme.GlyphWarning, Theme.Red)

        Dim recent = AddCard(page, 0, 132, 660, 360, "Recent orders", TopLeftRight)
        Dim viewAll As New LinkLabel With {.Text = "View all orders", .AutoSize = True, .Font = SemiFont(9.5F), .LinkColor = Theme.Blue, .ActiveLinkColor = Theme.BlueHover,
                                           .LinkBehavior = LinkBehavior.HoverUnderline, .Location = New Point(540, 20), .Anchor = TopRight}
        AddHandler viewAll.LinkClicked, Sub() ShowPage(PageOrders)
        recent.Controls.Add(viewAll)
        dashRecentGrid = AddGrid(recent, 16, 56, 628, 288, AllSides, "Order #", "Customer", "Service", "Status", "Payment", "Amount", "Time")
        AttachPills(dashRecentGrid, "Status", "Payment")
        AddHandler dashRecentGrid.CellDoubleClick, Sub(s As Object, e As DataGridViewCellEventArgs) OpenOrderInStatusPage(CellText(dashRecentGrid, e.RowIndex, "Order #"))

        Dim summary = AddCard(page, 680, 132, 360, 360, "Today's summary", TopRight)
        dashSumReceived = AddSummaryRow(summary, 60, "Orders received", Theme.GlyphCart, Theme.Blue)
        dashSumCollected = AddSummaryRow(summary, 112, "Orders collected", Theme.GlyphCheck, Theme.Green)
        dashSumMoney = AddSummaryRow(summary, 164, "Money received", Theme.GlyphPayment, Theme.Amber)
        dashSumOutstanding = AddSummaryRow(summary, 216, "Total outstanding", Theme.GlyphWarning, Theme.Red)
        Dim bNew = AddButton(summary, "New order", ButtonKind.Primary, Theme.GlyphAdd, 20, 296, 154)
        Dim bUpdate = AddButton(summary, "Update status", ButtonKind.Secondary, Theme.GlyphSync, 186, 296, 154)
        AddHandler bNew.Click, Sub() ShowPage(PageNewOrder)
        AddHandler bUpdate.Click, Sub() ShowPage(PageStatus)

        Dim chartCard = AddCard(page, 0, 512, 660, 300, "Money received this week", TopLeftRight)
        dashWeekTotal = AddValueLabel(chartCard, 380, 16, 260, True)
        dashWeekTotal.Anchor = TopRight
        dashChart = New BarChart With {.Location = New Point(16, 60), .Size = New Size(628, 224), .Anchor = AllSides}
        chartCard.Controls.Add(dashChart)

        Dim readyCard = AddCard(page, 680, 512, 360, 300, "Ready for collection", TopRight)
        dashReadyGrid = AddGrid(readyCard, 16, 56, 328, 176, AllSides, "Customer", "Phone", "Order #")
        Dim bSms = AddButton(readyCard, "Copy ready message", ButtonKind.Secondary, Theme.GlyphMessage, 16, 244, 328)
        AddHandler bSms.Click, Sub() CopyReadyMessage(DataStore.FindOrder(CurrentText(dashReadyGrid, "Order #")))

        pageRefreshers.Add(AddressOf RefreshDashboard)
    End Sub

    Private Sub RefreshDashboard()
        Dim today = DateTime.Today
        Dim active = Db.Orders.Where(Function(o) o.Status <> OrderStatus.Collected).ToList()
        dashActive.Text = active.Count.ToString()
        dashReady.Text = active.Where(Function(o) o.Status = OrderStatus.Ready).Count().ToString()
        dashProgress.Text = active.Where(Function(o) o.Status <> OrderStatus.Ready).Count().ToString()
        dashOverdue.Text = Db.Orders.Where(Function(o) o.IsOverdue).Count().ToString()

        dashRecentGrid.Rows.Clear()
        For Each o In Db.Orders.OrderByDescending(Function(x) x.OrderDate).Take(12)
            dashRecentGrid.Rows.Add(o.Id, o.CustomerName, o.Service, o.Status, o.PaymentStatus, Money(o.Amount),
                                    If(o.OrderDate.Date = today, o.OrderDate.ToString("h:mm tt", CultureInfo.InvariantCulture), o.OrderDate.ToString("dd MMM", CultureInfo.InvariantCulture)))
        Next

        Dim payments = DataStore.AllPayments()
        dashSumReceived.Text = Db.Orders.Where(Function(o) o.OrderDate.Date = today).Count().ToString()
        dashSumCollected.Text = Db.Orders.Where(Function(o) o.Status = OrderStatus.Collected AndAlso o.StatusChangedAt.Date = today).Count().ToString()
        dashSumMoney.Text = Money(payments.Where(Function(p) p.PaidAt.Date = today).Sum(Function(p) p.Amount))
        dashSumOutstanding.Text = Money(Db.Orders.Sum(Function(o) o.Balance))

        Dim days = Enumerable.Range(0, 7).Select(Function(i) today.AddDays(i - 6)).ToList()
        Dim values = days.Select(Function(d) payments.Where(Function(p) p.PaidAt.Date = d).Sum(Function(p) p.Amount)).ToList()
        dashChart.SetData(days.Select(Function(d) d.ToString("ddd", CultureInfo.InvariantCulture)), values, 6)
        dashWeekTotal.Text = "Total " & Money(values.Sum())

        dashReadyGrid.Rows.Clear()
        For Each o In active.Where(Function(x) x.Status = OrderStatus.Ready).OrderBy(Function(x) x.StatusChangedAt)
            dashReadyGrid.Rows.Add(o.CustomerName, o.Phone, o.Id)
        Next
    End Sub

    ' =================================================================
    '  PAGE 1: New order
    ' =================================================================
    Private Sub BuildNewOrderPage()
        Dim page = NewPage("New Order", 600)

        Dim customerCard = AddCard(page, 0, 0, 500, 190, "Customer", TopLeft)
        noPhone = AddTextBox(customerCard, "Phone number", 20, 52, 220)
        noName = AddTextBox(customerCard, "Customer name", 260, 52, 220)
        noHint = New Label With {.AutoSize = False, .Size = New Size(460, 44), .Location = New Point(20, 124), .Font = UiFont(9.5F), .ForeColor = Theme.Muted,
                                 .Text = "Type the phone number first. If the customer has been here before, their name fills in."}
        customerCard.Controls.Add(noHint)

        Dim orderCard = AddCard(page, 0, 210, 500, 370, "Order details", TopLeft)
        noService = AddCombo(orderCard, "Service", 20, 52, 300)
        AddCaption(orderCard, "Weight (kg)", 340, 52)
        noWeight = New NumericUpDown With {.DecimalPlaces = 1, .Increment = 0.5D, .Minimum = 0D, .Maximum = 1000D, .Value = 0D, .Location = New Point(340, 74), .Width = 140, .Font = UiFont(10.5F), .ThousandsSeparator = False}
        orderCard.Controls.Add(noWeight)
        noDue = AddDatePicker(orderCard, "Collection date", 20, 116, 200)
        noItems = AddTextBox(orderCard, "Items received (for example: 3 shirts, 2 trousers, 1 bedsheet)", 20, 180, 460, 64)
        noNotes = AddTextBox(orderCard, "Notes (stains, special care)", 20, 270, 460)

        Dim summaryCard = AddCard(page, 520, 0, 520, 580, "Order summary", TopLeftRight)
        Dim rowsY = {64, 100, 136}
        Dim rowCaptions = {"Service", "Weight", "Price band"}
        Dim valueLabels As New List(Of Label)()
        For i = 0 To 2
            AddCaption(summaryCard, rowCaptions(i), 24, rowsY(i) + 4).Font = UiFont(10.5F)
            Dim v = AddValueLabel(summaryCard, 256, rowsY(i), 240)
            v.Anchor = TopRight
            valueLabels.Add(v)
        Next
        noSumService = valueLabels(0)
        noSumWeight = valueLabels(1)
        noSumBand = valueLabels(2)
        noSumBand.Left = 136
        noSumBand.Width = 360
        noSumBand.Font = UiFont(9.5F)
        summaryCard.Controls.Add(New Panel With {.Location = New Point(24, 176), .Size = New Size(472, 1), .BackColor = Theme.Border, .Anchor = TopLeftRight})
        Dim totalCaption = AddCaption(summaryCard, "Total", 24, 196)
        totalCaption.Font = UiFont(12.0F, FontStyle.Bold)
        totalCaption.ForeColor = Theme.TextDark
        noSumTotal = New Label With {.AutoSize = False, .Size = New Size(300, 44), .Location = New Point(196, 186), .TextAlign = ContentAlignment.MiddleRight,
                                     .Font = UiFont(22.0F, FontStyle.Bold), .ForeColor = Theme.Blue, .Anchor = TopRight}
        summaryCard.Controls.Add(noSumTotal)

        Dim payTitle = AddCaption(summaryCard, "Payment now (optional)", 24, 250)
        payTitle.Font = UiFont(11.0F, FontStyle.Bold)
        payTitle.ForeColor = Theme.TextDark
        noPaid = AddTextBox(summaryCard, "Amount paid now (GH₵)", 24, 282, 200)
        noMethod = AddCombo(summaryCard, "Payment method", 244, 282, 200)
        noMethod.Items.AddRange(New Object() {"Cash", "Mobile Money", "Card", "Bank transfer"})
        noMethod.SelectedIndex = 0
        AddCaption(summaryCard, "Balance after payment", 24, 356).Font = UiFont(10.5F)
        noSumBalance = AddValueLabel(summaryCard, 256, 352, 240, True)
        noSumBalance.Anchor = TopRight

        Dim bSave = AddButton(summaryCard, "Save order", ButtonKind.Primary, Theme.GlyphSave, 24, 420, 200)
        Dim bSavePrint = AddButton(summaryCard, "Save and print receipt", ButtonKind.Success, Theme.GlyphPrint, 236, 420, 260)
        Dim bClear = AddButton(summaryCard, "Clear form", ButtonKind.Secondary, Theme.GlyphRefresh, 24, 476, 200)
        AddHandler bSave.Click, Sub() SaveNewOrder(False)
        AddHandler bSavePrint.Click, Sub() SaveNewOrder(True)
        AddHandler bClear.Click, Sub() ClearNewOrder()

        AddHandler noService.SelectedIndexChanged, Sub() UpdateNewOrderSummary()
        AddHandler noWeight.ValueChanged, Sub() UpdateNewOrderSummary()
        AddHandler noWeight.KeyUp, Sub() UpdateNewOrderSummary()
        AddHandler noPaid.TextChanged, Sub() UpdateNewOrderSummary()
        AddHandler noPhone.TextChanged,
            Sub()
                Dim found = DataStore.FindCustomerByPhone(noPhone.Text)
                If found IsNot Nothing Then
                    noName.Text = found.Name
                    Dim count = Db.Orders.Where(Function(o) o.CustomerId = found.Id).Count()
                    noHint.Text = "Returning customer: " & found.Name & ", " & count & " previous order" & If(count = 1, "", "s") & "."
                    noHint.ForeColor = ColorTranslator.FromHtml("#0E6B41")
                Else
                    noHint.Text = "Type the phone number first. If the customer has been here before, their name fills in."
                    noHint.ForeColor = Theme.Muted
                End If
            End Sub

        pageRefreshers.Add(AddressOf RefreshNewOrder)
        ClearNewOrder()
    End Sub

    Private Function SelectedServiceName() As String
        Return Convert.ToString(noService.SelectedItem)
    End Function

    ' Reads the weight as it is being typed, not only after the box loses focus.
    Private Function CurrentWeight() As Decimal
        Dim typed As Decimal
        If Decimal.TryParse(noWeight.Text, NumberStyles.Number, CultureInfo.CurrentCulture, typed) Then Return Math.Max(0D, Math.Min(1000D, typed))
        Return noWeight.Value
    End Function

    Private Sub RefreshNewOrder()
        Dim previous = SelectedServiceName()
        noService.Items.Clear()
        For Each serviceItem In Pricing.ServiceNames(Db.Services)
            noService.Items.Add(serviceItem)
        Next
        If noService.Items.Count > 0 Then
            noService.SelectedIndex = 0
            If previous <> "" AndAlso noService.Items.Contains(previous) Then noService.SelectedItem = previous
        End If
        UpdateNewOrderSummary()
        noPhone.Focus()
    End Sub

    Private Sub UpdateNewOrderSummary()
        If noSumTotal Is Nothing Then Return
        Dim serviceName = SelectedServiceName()
        Dim weight = CurrentWeight()
        Dim quote = Pricing.Quote(Db.Services, serviceName, weight)
        noSumService.Text = If(serviceName = "", "—", serviceName)
        noSumWeight.Text = If(weight > 0D, Helpers.Kg(weight), "Weigh the laundry")
        noSumBand.Text = If(quote Is Nothing, If(weight > 0D, "No price set for this service", "—"), quote.Note)
        Dim total = If(quote Is Nothing, 0D, quote.Price)
        noSumTotal.Text = Money(total)
        Dim paid As Decimal = 0D
        If noPaid.Text.Trim() <> "" AndAlso Not TryParseMoney(noPaid.Text, paid) Then paid = 0D
        Dim balance = Math.Max(0D, total - paid)
        noSumBalance.Text = Money(balance)
        noSumBalance.ForeColor = If(balance > 0D, Theme.Red, ColorTranslator.FromHtml("#0E6B41"))
    End Sub

    Private Sub ClearNewOrder()
        noPhone.Clear()
        noName.Clear()
        noItems.Clear()
        noNotes.Clear()
        noPaid.Clear()
        noWeight.Value = 0D
        noWeight.Text = "0.0"
        noDue.Value = DateTime.Today.AddDays(Math.Max(0, Db.Business.DefaultCollectionDays))
        noMethod.SelectedIndex = 0
        If noService.Items.Count > 0 Then noService.SelectedIndex = 0
        UpdateNewOrderSummary()
        noPhone.Focus()
    End Sub

    Private Sub SaveNewOrder(printAfterSaving As Boolean)
        Dim phone = noPhone.Text.Trim()
        Dim name = noName.Text.Trim()
        If phone = "" OrElse name = "" Then
            Warn("Enter the customer's phone number and name.")
            Return
        End If
        Dim serviceName = SelectedServiceName()
        If serviceName = "" Then
            Warn("Choose a service. Administrators can add services and prices in Settings.")
            Return
        End If
        Dim weight = CurrentWeight()
        If weight <= 0D Then
            Warn("Weigh the laundry and enter the weight in kg.")
            Return
        End If
        Dim quote = Pricing.Quote(Db.Services, serviceName, weight)
        If quote Is Nothing Then
            Warn("There is no price for " & serviceName & " yet. An administrator can add one in Settings.")
            Return
        End If
        Dim total = quote.Price
        Dim paid As Decimal = 0D
        If noPaid.Text.Trim() <> "" AndAlso Not TryParseMoney(noPaid.Text, paid) Then
            Warn("Enter the amount paid as a number, for example 20 or 20.50.")
            Return
        End If
        If paid < 0D OrElse paid > total Then
            Warn("The amount paid can't be more than the order total of " & Money(total) & ".")
            Return
        End If
        If noDue.Value.Date < DateTime.Today Then
            Warn("The collection date can't be in the past.")
            Return
        End If

        Dim cust = DataStore.FindCustomerByPhone(phone)
        If cust Is Nothing Then
            cust = New Customer With {.Id = DataStore.NextCustomerId(), .name = name, .phone = phone}
            Db.Customers.Add(cust)
        ElseIf cust.Name <> name Then
            cust.Name = name
        End If

        Dim order As New LaundryOrder With {
            .Id = DataStore.NextOrderId(), .CustomerId = cust.Id, .CustomerName = cust.Name, .phone = cust.Phone,
            .Service = serviceName, .ItemCount = 0, .WeightKg = weight, .PricingNote = quote.Note, .ItemsDescription = noItems.Text.Trim(), .UnitPrice = total, .Amount = total,
            .Notes = noNotes.Text.Trim(), .OrderDate = DateTime.Now, .DueDate = noDue.Value.Date, .Status = OrderStatus.Pending,
            .StatusChangedAt = DateTime.Now, .CreatedBy = currentUser.FullName}
        order.History.Add(New StatusChange With {.Status = OrderStatus.Pending, .ChangedAt = DateTime.Now, .ChangedBy = currentUser.FullName})

        Dim payment As Payment = Nothing
        If paid > 0D Then
            payment = New Payment With {.ReceiptNo = DataStore.NextReceiptNo(), .OrderId = order.Id, .Amount = paid,
                                        .Method = Convert.ToString(noMethod.SelectedItem), .PaidAt = DateTime.Now, .ReceivedBy = currentUser.FullName}
            order.Payments.Add(payment)
        End If

        Db.Orders.Add(order)
        DataStore.SaveData()
        ClearNewOrder()
        If printAfterSaving Then
            PrintReceipt(order, payment)
        Else
            Info("Order " & order.Id & " saved for " & order.CustomerName & ". Total " & Money(order.Amount) & ", balance " & Money(order.Balance) & ".")
        End If
    End Sub

    ' =================================================================
    '  PAGE 2: Update status
    ' =================================================================
    Private Sub BuildStatusPage()
        Dim page = NewPage("Update Status", 620)

        Dim listCard = AddCard(page, 0, 0, 620, 600, "Orders in progress", TopLeftRight)
        usSearch = AddTextBox(listCard, "Search by order #, name or phone", 20, 52, 360)
        usShowCollected = New CheckBox With {.Text = "Include collected orders", .AutoSize = True, .Font = UiFont(9.75F), .ForeColor = Theme.TextDark, .Location = New Point(400, 78)}
        listCard.Controls.Add(usShowCollected)
        usGrid = AddGrid(listCard, 16, 120, 588, 464, AllSides, "Order #", "Customer", "Service", "Status", "Due")
        AttachPills(usGrid, "Status")

        Dim detailCard = AddCard(page, 640, 0, 400, 600, "Order details", TopRight)
        usOrderLabel = New Label With {.AutoSize = True, .Font = UiFont(18.0F, FontStyle.Bold), .ForeColor = Theme.TextDark, .Location = New Point(18, 50)}
        usStatusLabel = New Label With {.AutoSize = True, .Font = SemiFont(9.5F), .Padding = New Padding(10, 4, 10, 4), .Location = New Point(20, 92)}
        usCustomerLabel = New Label With {.AutoSize = False, .Size = New Size(360, 24), .Font = SemiFont(10.5F), .ForeColor = Theme.TextDark, .Location = New Point(20, 128)}
        usDetailsLabel = New Label With {.AutoSize = False, .Size = New Size(360, 44), .Font = UiFont(9.75F), .ForeColor = Theme.Muted, .Location = New Point(20, 154)}
        usDueLabel = New Label With {.AutoSize = False, .Size = New Size(360, 22), .Font = UiFont(9.75F), .ForeColor = Theme.TextDark, .Location = New Point(20, 200)}
        usBalanceLabel = New Label With {.AutoSize = False, .Size = New Size(360, 22), .Font = SemiFont(9.75F), .Location = New Point(20, 224)}
        detailCard.Controls.AddRange(New Control() {usOrderLabel, usStatusLabel, usCustomerLabel, usDetailsLabel, usDueLabel, usBalanceLabel})

        usNextButton = AddButton(detailCard, "Move to next stage", ButtonKind.Success, Theme.GlyphForward, 20, 262, 360)
        usStatusCombo = AddCombo(detailCard, "Or choose a status", 20, 316, 220)
        usStatusCombo.Items.AddRange(OrderStatus.All)
        usUpdateButton = AddButton(detailCard, "Update", ButtonKind.Primary, Theme.GlyphSync, 252, 334, 128)
        AddCaption(detailCard, "Status history", 20, 392)
        usHistory = New ListBox With {.Location = New Point(20, 414), .Size = New Size(360, 166), .BorderStyle = BorderStyle.FixedSingle, .Font = UiFont(9.5F), .ForeColor = Theme.TextDark, .IntegralHeight = False}
        detailCard.Controls.Add(usHistory)

        AddHandler usSearch.TextChanged, Sub() RefreshStatusPage()
        AddHandler usShowCollected.CheckedChanged,
            Sub()
                If Not suppressEvents Then RefreshStatusPage()
            End Sub
        AddHandler usGrid.SelectionChanged,
            Sub()
                If suppressEvents Then Return
                usSelectedId = CurrentText(usGrid, "Order #")
                ShowStatusDetails(DataStore.FindOrder(usSelectedId))
            End Sub
        AddHandler usNextButton.Click,
            Sub()
                Dim o = DataStore.FindOrder(usSelectedId)
                If o IsNot Nothing Then ChangeStatus(o, OrderStatus.NextOf(o.Status))
            End Sub
        AddHandler usUpdateButton.Click,
            Sub()
                Dim o = DataStore.FindOrder(usSelectedId)
                If o IsNot Nothing Then ChangeStatus(o, Convert.ToString(usStatusCombo.SelectedItem))
            End Sub

        pageRefreshers.Add(AddressOf RefreshStatusPage)
    End Sub

    Private Sub RefreshStatusPage()
        suppressEvents = True
        Dim term = usSearch.Text.Trim().ToLowerInvariant()
        Dim showCollected = usShowCollected.Checked
        Dim list = Db.Orders.Where(Function(o) (showCollected OrElse o.Status <> OrderStatus.Collected) AndAlso MatchesSearch(o, term)).OrderBy(Function(o) o.DueDate).ThenBy(Function(o) o.Id).ToList()
        usGrid.Rows.Clear()
        For Each o In list
            Dim r = usGrid.Rows.Add(o.Id, o.CustomerName, o.Service, o.Status, o.DueDate.ToString("dd MMM", CultureInfo.InvariantCulture) & If(o.IsOverdue, "  late", ""))
            If o.IsOverdue Then usGrid.Rows(r).Cells("Due").Style.ForeColor = Theme.Red
        Next
        Dim wanted = If(pendingStatusOrderId <> "", pendingStatusOrderId, usSelectedId)
        pendingStatusOrderId = ""
        usGrid.ClearSelection()
        SelectRow(usGrid, "Order #", wanted)
        If usGrid.SelectedRows.Count = 0 AndAlso usGrid.Rows.Count > 0 Then
            usGrid.Rows(0).Selected = True
            usGrid.CurrentCell = usGrid.Rows(0).Cells(0)
        End If
        suppressEvents = False
        usSelectedId = If(usGrid.SelectedRows.Count > 0, CurrentText(usGrid, "Order #"), "")
        ShowStatusDetails(DataStore.FindOrder(usSelectedId))
    End Sub

    Private Sub ShowStatusDetails(o As LaundryOrder)
        Dim hasOrder = o IsNot Nothing
        usNextButton.Enabled = hasOrder
        usUpdateButton.Enabled = hasOrder
        usStatusCombo.Enabled = hasOrder
        usHistory.Items.Clear()
        If Not hasOrder Then
            usOrderLabel.Text = "No order selected"
            usStatusLabel.Visible = False
            usCustomerLabel.Text = "Select an order from the list to update it."
            usDetailsLabel.Text = ""
            usDueLabel.Text = ""
            usBalanceLabel.Text = ""
            usNextButton.Text = "Move to next stage"
            Return
        End If
        usOrderLabel.Text = o.Id
        Dim colors = PillColors(o.Status)
        usStatusLabel.Text = o.Status
        usStatusLabel.BackColor = colors.Item1
        usStatusLabel.ForeColor = colors.Item2
        usStatusLabel.Visible = True
        usCustomerLabel.Text = o.CustomerName & "   ·   " & o.Phone
        usDetailsLabel.Text = o.Service & ", " & o.SizeText & If(o.ItemsDescription <> "", ": " & o.ItemsDescription, "")
        usDueLabel.Text = "Collection date: " & o.DueDate.ToString("ddd, dd MMM yyyy", CultureInfo.InvariantCulture) & If(o.IsOverdue, "  (overdue)", "")
        usDueLabel.ForeColor = If(o.IsOverdue, Theme.Red, Theme.TextDark)
        usBalanceLabel.Text = If(o.Balance > 0D, "Balance to pay: " & Money(o.Balance), "Fully paid (" & Money(o.Amount) & ")")
        usBalanceLabel.ForeColor = If(o.Balance > 0D, Theme.Red, ColorTranslator.FromHtml("#0E6B41"))
        Dim nextStatus = OrderStatus.NextOf(o.Status)
        usNextButton.Text = If(nextStatus = "", "Order completed", "Move to " & nextStatus)
        usNextButton.Enabled = nextStatus <> ""
        usStatusCombo.SelectedItem = o.Status
        For Each h In o.History.OrderByDescending(Function(x) x.ChangedAt)
            usHistory.Items.Add(h.ChangedAt.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture) & "   " & h.Status & "   (" & h.ChangedBy & ")")
        Next
    End Sub

    Private Sub ChangeStatus(o As LaundryOrder, newStatus As String)
        If o Is Nothing OrElse newStatus = "" OrElse newStatus = o.Status Then Return
        If newStatus = OrderStatus.Collected AndAlso o.Balance > 0D Then
            If Not Confirm(o.Id & " still has " & Money(o.Balance) & " to pay." & Environment.NewLine & "Mark it as collected anyway?", "Balance outstanding") Then Return
        End If
        o.Status = newStatus
        o.StatusChangedAt = DateTime.Now
        o.History.Add(New StatusChange With {.Status = newStatus, .ChangedAt = DateTime.Now, .ChangedBy = currentUser.FullName})
        DataStore.SaveData()
        usSelectedId = o.Id
        RefreshStatusPage()
        If newStatus = OrderStatus.Ready AndAlso Confirm(o.Id & " is ready. Copy a text message to send to " & o.CustomerName & "?", "Tell the customer") Then
            CopyReadyMessage(o)
        End If
    End Sub

    ' =================================================================
    '  PAGE 3: Order list
    ' =================================================================
    Private Sub BuildOrdersPage()
        Dim page = NewPage("Order List", 660)

        Dim filterCard = AddCard(page, 0, 0, 1040, 96, "", TopLeftRight)
        olSearch = AddTextBox(filterCard, "Search", 20, 14, 260)
        olStatus = AddCombo(filterCard, "Show", 300, 14, 190)
        olStatus.Items.Add("All orders")
        olStatus.Items.AddRange(OrderStatus.All)
        olStatus.Items.AddRange(New Object() {"Overdue", "Not fully paid"})
        olStatus.SelectedIndex = 0
        olFrom = AddDatePicker(filterCard, "From", 510, 14, 150)
        olTo = AddDatePicker(filterCard, "To", 680, 14, 150)
        Dim reset As New LinkLabel With {.Text = "Reset filters", .AutoSize = True, .Font = SemiFont(9.5F), .LinkColor = Theme.Blue, .LinkBehavior = LinkBehavior.HoverUnderline, .Location = New Point(852, 42)}
        filterCard.Controls.Add(reset)
        ResetOrderFilters()

        Dim gridCard = AddCard(page, 0, 116, 1040, 520, "All orders", TopLeftRight)
        olCount = New Label With {.AutoSize = True, .Font = UiFont(9.5F), .ForeColor = Theme.Muted, .Location = New Point(22, 46)}
        gridCard.Controls.Add(olCount)
        Dim bExport = MakeButton("Export to Excel", ButtonKind.Primary, Theme.GlyphDownload)
        Dim bPrint = MakeButton("Print receipt", ButtonKind.Secondary, Theme.GlyphPrint)
        Dim bOpen = MakeButton("Update status", ButtonKind.Secondary, Theme.GlyphSync)
        Dim bDelete = MakeButton("Delete", ButtonKind.Danger, Theme.GlyphDelete)
        Dim bEdit = MakeButton("Edit order", ButtonKind.Secondary, Theme.GlyphRefresh)
        If currentUser.IsAdmin Then
            AddHeaderButtons(gridCard, bExport, bPrint, bOpen, bEdit, bDelete)
        Else
            AddHeaderButtons(gridCard, bExport, bPrint, bOpen)
        End If
        olGrid = AddGrid(gridCard, 16, 74, 1008, 430, AllSides, "Order #", "Date", "Customer", "Phone", "Service", "Weight", "Amount", "Paid", "Balance", "Payment", "Status", "Due")
        AttachPills(olGrid, "Payment", "Status")

        AddHandler olSearch.TextChanged, Sub() RefreshOrderList()
        AddHandler olStatus.SelectedIndexChanged, Sub() RefreshOrderList()
        AddHandler olFrom.ValueChanged, Sub() RefreshOrderList()
        AddHandler olTo.ValueChanged, Sub() RefreshOrderList()
        AddHandler reset.LinkClicked,
            Sub()
                ResetOrderFilters()
                RefreshOrderList()
            End Sub
        AddHandler olGrid.CellDoubleClick, Sub(s As Object, e As DataGridViewCellEventArgs) OpenOrderInStatusPage(CellText(olGrid, e.RowIndex, "Order #"))
        AddHandler bOpen.Click, Sub() OpenOrderInStatusPage(CurrentText(olGrid, "Order #"))
        AddHandler bExport.Click, Sub() SaveWorkbook("orders-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx", OrdersSheet("Orders", olLastList))
        AddHandler bEdit.Click,
            Sub()
                EditOrder(DataStore.FindOrder(CurrentText(olGrid, "Order #")))
                RefreshOrderList()
            End Sub
        AddHandler bPrint.Click,
            Sub()
                Dim o = DataStore.FindOrder(CurrentText(olGrid, "Order #"))
                If o Is Nothing Then
                    Warn("Select an order first.")
                Else
                    PrintReceipt(o, Nothing)
                End If
            End Sub
        AddHandler bDelete.Click,
            Sub()
                Dim o = DataStore.FindOrder(CurrentText(olGrid, "Order #"))
                If o Is Nothing Then
                    Warn("Select an order first.")
                    Return
                End If
                If Confirm("Delete order " & o.Id & " for " & o.CustomerName & "?" & Environment.NewLine & "This also removes its payments and can't be undone.", "Delete order") Then
                    Db.Orders.Remove(o)
                    DataStore.SaveData()
                    RefreshOrderList()
                End If
            End Sub

        pageRefreshers.Add(AddressOf RefreshOrderList)
    End Sub

    Private Sub ResetOrderFilters()
        suppressEvents = True
        olSearch.Clear()
        olStatus.SelectedIndex = 0
        Dim first = If(Db.Orders.Count > 0, Db.Orders.Min(Function(o) o.OrderDate).Date, DateTime.Today)
        olFrom.Value = If(first < DateTime.Today, first, DateTime.Today)
        olTo.Value = DateTime.Today
        suppressEvents = False
    End Sub

    Private Sub RefreshOrderList()
        If suppressEvents Then Return
        Dim term = olSearch.Text.Trim().ToLowerInvariant()
        Dim filter = Convert.ToString(olStatus.SelectedItem)
        Dim fromDate = olFrom.Value.Date
        Dim toDate = olTo.Value.Date
        Dim list = Db.Orders.Where(Function(o) MatchesSearch(o, term) AndAlso o.OrderDate.Date >= fromDate AndAlso o.OrderDate.Date <= toDate).ToList()
        Select Case filter
            Case "All orders"
            Case "Overdue"
                list = list.Where(Function(o) o.IsOverdue).ToList()
            Case "Not fully paid"
                list = list.Where(Function(o) o.Balance > 0D).ToList()
            Case Else
                list = list.Where(Function(o) o.Status = filter).ToList()
        End Select
        olGrid.Rows.Clear()
        olLastList = list.OrderByDescending(Function(x) x.OrderDate).ToList()
        For Each o In olLastList
            Dim r = olGrid.Rows.Add(o.Id, o.OrderDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), o.CustomerName, o.Phone, o.Service, o.SizeText,
                                    Money(o.Amount), Money(o.PaidAmount), Money(o.Balance), o.PaymentStatus, o.Status,
                                    o.DueDate.ToString("dd MMM", CultureInfo.InvariantCulture) & If(o.IsOverdue, "  late", ""))
            If o.IsOverdue Then olGrid.Rows(r).Cells("Due").Style.ForeColor = Theme.Red
        Next
        olCount.Text = list.Count & " order" & If(list.Count = 1, "", "s") & ",  " & Money(list.Sum(Function(o) o.Amount)) & " in total"
    End Sub

    ' =================================================================
    '  PAGE 4: Customers
    ' =================================================================
    Private Sub BuildCustomersPage()
        Dim page = NewPage("Customers", 620)

        Dim listCard = AddCard(page, 0, 0, 560, 600, "Customers", TopLeftRight)
        cuSearch = AddTextBox(listCard, "Search by name or phone", 20, 52, 300)
        cuGrid = AddGrid(listCard, 16, 118, 528, 466, AllSides, "Id", "Name", "Phone", "Orders", "Total spent", "Balance")
        cuGrid.Columns("Id").Visible = False

        Dim detailCard = AddCard(page, 580, 0, 460, 600, "", TopRight)
        cuTitle = New Label With {.Text = "Customer details", .Font = UiFont(12.5F, FontStyle.Bold), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(20, 16)}
        detailCard.Controls.Add(cuTitle)
        cuName = AddTextBox(detailCard, "Name", 20, 52, 420)
        cuPhone = AddTextBox(detailCard, "Phone number", 20, 112, 420)
        cuNotes = AddTextBox(detailCard, "Notes", 20, 172, 420, 56)
        Dim bSave = AddButton(detailCard, "Save customer", ButtonKind.Primary, Theme.GlyphSave, 20, 262, 200)
        Dim bNew = AddButton(detailCard, "New customer", ButtonKind.Secondary, Theme.GlyphAdd, 232, 262, 208)
        AddCaption(detailCard, "Order history", 20, 322)
        cuOrdersGrid = AddGrid(detailCard, 16, 346, 428, 238, AllSides, "Order #", "Date", "Service", "Amount", "Status")
        AttachPills(cuOrdersGrid, "Status")

        AddHandler cuSearch.TextChanged, Sub() RefreshCustomers()
        AddHandler cuGrid.SelectionChanged,
            Sub()
                If suppressEvents Then Return
                Dim idText = CurrentText(cuGrid, "Id")
                Dim id As Integer
                If Integer.TryParse(idText, id) Then LoadCustomer(DataStore.FindCustomer(id))
            End Sub
        AddHandler cuOrdersGrid.CellDoubleClick, Sub(s As Object, e As DataGridViewCellEventArgs) OpenOrderInStatusPage(CellText(cuOrdersGrid, e.RowIndex, "Order #"))
        AddHandler bNew.Click,
            Sub()
                suppressEvents = True
                cuGrid.ClearSelection()
                suppressEvents = False
                LoadCustomer(Nothing)
                cuName.Focus()
            End Sub
        AddHandler bSave.Click, Sub() SaveCustomer()

        pageRefreshers.Add(AddressOf RefreshCustomers)
    End Sub

    Private Sub RefreshCustomers()
        suppressEvents = True
        Dim term = cuSearch.Text.Trim().ToLowerInvariant()
        Dim termDigits = Helpers.DigitsOnly(term)
        cuGrid.Rows.Clear()
        For Each c In Db.Customers.OrderBy(Function(x) x.Name)
            If term <> "" AndAlso Not c.Name.ToLowerInvariant().Contains(term) AndAlso Not (termDigits <> "" AndAlso Helpers.DigitsOnly(c.Phone).Contains(termDigits)) Then Continue For
            Dim custId = c.Id
            Dim custOrders = Db.Orders.Where(Function(o) o.CustomerId = custId).ToList()
            cuGrid.Rows.Add(c.Id, c.Name, c.Phone, custOrders.Count, Money(custOrders.Sum(Function(o) o.PaidAmount)), Money(custOrders.Sum(Function(o) o.Balance)))
        Next
        cuGrid.ClearSelection()
        SelectRow(cuGrid, "Id", If(cuSelectedId > 0, cuSelectedId.ToString(), ""))
        suppressEvents = False
        LoadCustomer(DataStore.FindCustomer(cuSelectedId))
    End Sub

    Private Sub LoadCustomer(c As Customer)
        cuOrdersGrid.Rows.Clear()
        If c Is Nothing Then
            cuSelectedId = 0
            cuTitle.Text = "New customer"
            cuName.Clear()
            cuPhone.Clear()
            cuNotes.Clear()
            Return
        End If
        cuSelectedId = c.Id
        cuTitle.Text = "Customer details"
        cuName.Text = c.Name
        cuPhone.Text = c.Phone
        cuNotes.Text = c.Notes
        For Each o In Db.Orders.Where(Function(x) x.CustomerId = c.Id).OrderByDescending(Function(x) x.OrderDate)
            cuOrdersGrid.Rows.Add(o.Id, o.OrderDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), o.Service, Money(o.Amount), o.Status)
        Next
    End Sub

    Private Sub SaveCustomer()
        Dim name = cuName.Text.Trim()
        Dim phone = cuPhone.Text.Trim()
        If name = "" OrElse phone = "" Then
            Warn("Enter the customer's name and phone number.")
            Return
        End If
        Dim samePhone = DataStore.FindCustomerByPhone(phone)
        If samePhone IsNot Nothing AndAlso samePhone.Id <> cuSelectedId Then
            Warn("Another customer (" & samePhone.Name & ") already uses this phone number.")
            Return
        End If
        Dim c = DataStore.FindCustomer(cuSelectedId)
        If c Is Nothing Then
            c = New Customer With {.Id = DataStore.NextCustomerId()}
            Db.Customers.Add(c)
        End If
        c.Name = name
        c.Phone = phone
        c.Notes = cuNotes.Text.Trim()
        For Each o In Db.Orders.Where(Function(x) x.CustomerId = c.Id)
            o.CustomerName = c.Name
            o.Phone = c.Phone
        Next
        DataStore.SaveData()
        cuSelectedId = c.Id
        RefreshCustomers()
        Info("Customer saved.")
    End Sub

    ' =================================================================
    '  PAGE 5: Payments
    ' =================================================================
    Private Sub BuildPaymentsPage()
        Dim page = NewPage("Payments", 760)

        Dim recordCard = AddCard(page, 0, 0, 1040, 170, "Record a payment", TopLeftRight)
        pyOrder = AddCombo(recordCard, "Order", 20, 52, 420)
        pyAmount = AddTextBox(recordCard, "Amount (GH₵)", 460, 52, 150)
        pyMethod = AddCombo(recordCard, "Method", 630, 52, 170)
        pyMethod.Items.AddRange(New Object() {"Cash", "Mobile Money", "Card", "Bank transfer"})
        pyMethod.SelectedIndex = 0
        Dim bRecord = AddButton(recordCard, "Record payment", ButtonKind.Primary, Theme.GlyphPayment, 820, 72, 200)
        pyBalanceLabel = New Label With {.AutoSize = True, .Font = SemiFont(10.0F), .ForeColor = Theme.Muted, .Location = New Point(20, 122)}
        recordCard.Controls.Add(pyBalanceLabel)

        Dim outCard = AddCard(page, 0, 190, 1040, 260, "Orders with a balance", TopLeftRight)
        pyOutGrid = AddGrid(outCard, 16, 56, 1008, 188, AllSides, "Order #", "Customer", "Phone", "Amount", "Paid", "Balance", "Payment")
        AttachPills(pyOutGrid, "Payment")

        Dim histCard = AddCard(page, 0, 470, 1040, 270, "Payment history", TopLeftRight)
        Dim bReceipt = MakeButton("Print receipt", ButtonKind.Secondary, Theme.GlyphPrint)
        Dim bExport = MakeButton("Export to Excel", ButtonKind.Secondary, Theme.GlyphDownload)
        Dim bDeletePayment = MakeButton("Delete payment", ButtonKind.Danger, Theme.GlyphDelete)
        If currentUser.IsAdmin Then
            AddHeaderButtons(histCard, bExport, bReceipt, bDeletePayment)
        Else
            AddHeaderButtons(histCard, bExport, bReceipt)
        End If
        pyHistGrid = AddGrid(histCard, 16, 64, 1008, 190, AllSides, "Receipt #", "Date", "Order #", "Customer", "Amount", "Method", "Received by")

        AddHandler pyOrder.SelectedIndexChanged,
            Sub()
                Dim o = SelectedPaymentOrder()
                If o Is Nothing Then
                    pyBalanceLabel.Text = ""
                Else
                    pyBalanceLabel.Text = o.CustomerName & ":  total " & Money(o.Amount) & ",  paid " & Money(o.PaidAmount) & ",  balance " & Money(o.Balance)
                    pyAmount.Text = o.Balance.ToString("0.00", CultureInfo.InvariantCulture)
                End If
            End Sub
        AddHandler pyOutGrid.CellClick,
            Sub(s As Object, e As DataGridViewCellEventArgs)
                Dim id = CellText(pyOutGrid, e.RowIndex, "Order #")
                For i = 0 To pyOrder.Items.Count - 1
                    If DirectCast(pyOrder.Items(i), ComboItem).Key = id Then pyOrder.SelectedIndex = i
                Next
            End Sub
        AddHandler bRecord.Click, Sub() RecordPayment()
        AddHandler bReceipt.Click,
            Sub()
                Dim receiptNo = CurrentText(pyHistGrid, "Receipt #")
                Dim p = DataStore.AllPayments().FirstOrDefault(Function(x) x.ReceiptNo = receiptNo)
                If p Is Nothing Then
                    Warn("Select a payment first.")
                Else
                    PrintReceipt(DataStore.FindOrder(p.OrderId), p)
                End If
            End Sub
        AddHandler bExport.Click, Sub() SaveWorkbook("payments-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx", PaymentsSheet("Payments", DataStore.AllPayments()))
        AddHandler bDeletePayment.Click,
            Sub()
                Dim receiptNo = CurrentText(pyHistGrid, "Receipt #")
                Dim p = DataStore.AllPayments().FirstOrDefault(Function(x) x.ReceiptNo = receiptNo)
                If p Is Nothing Then
                    Warn("Select a payment first.")
                    Return
                End If
                Dim o = DataStore.FindOrder(p.OrderId)
                If o Is Nothing Then Return
                If Confirm("Delete payment " & p.ReceiptNo & " of " & Money(p.Amount) & " for " & o.Id & "?" & Environment.NewLine & "Use this only to correct a mistake. The order's balance will go back up.", "Delete payment") Then
                    o.Payments.Remove(p)
                    o.History.Add(New StatusChange With {.Status = "Payment " & p.ReceiptNo & " (" & Money(p.Amount) & ") deleted", .ChangedAt = DateTime.Now, .ChangedBy = currentUser.FullName})
                    DataStore.SaveData()
                    RefreshPayments()
                End If
            End Sub

        pageRefreshers.Add(AddressOf RefreshPayments)
    End Sub

    Private Function SelectedPaymentOrder() As LaundryOrder
        Dim item = TryCast(pyOrder.SelectedItem, ComboItem)
        Return If(item Is Nothing, Nothing, DataStore.FindOrder(item.Key))
    End Function

    Private Sub RefreshPayments()
        Dim previous = TryCast(pyOrder.SelectedItem, ComboItem)
        pyOrder.Items.Clear()
        Dim owing = Db.Orders.Where(Function(o) o.Balance > 0D).OrderBy(Function(o) o.Id).ToList()
        For Each o In owing
            pyOrder.Items.Add(New ComboItem(o.Id & "  —  " & o.CustomerName & "  —  balance " & Money(o.Balance), o.Id))
        Next
        If previous IsNot Nothing Then
            For i = 0 To pyOrder.Items.Count - 1
                If DirectCast(pyOrder.Items(i), ComboItem).Key = previous.Key Then pyOrder.SelectedIndex = i
            Next
        End If
        If pyOrder.SelectedIndex < 0 Then
            pyAmount.Clear()
            pyBalanceLabel.Text = If(owing.Count = 0, "Every order is fully paid.", "Choose an order, or click one in the list below.")
        End If

        pyOutGrid.Rows.Clear()
        For Each o In owing
            pyOutGrid.Rows.Add(o.Id, o.CustomerName, o.Phone, Money(o.Amount), Money(o.PaidAmount), Money(o.Balance), o.PaymentStatus)
        Next

        pyHistGrid.Rows.Clear()
        For Each p In DataStore.AllPayments().OrderByDescending(Function(x) x.PaidAt)
            Dim o = DataStore.FindOrder(p.OrderId)
            pyHistGrid.Rows.Add(p.ReceiptNo, p.PaidAt.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture), p.OrderId,
                                If(o Is Nothing, "", o.CustomerName), Money(p.Amount), p.Method, p.ReceivedBy)
        Next
    End Sub

    Private Sub RecordPayment()
        Dim o = SelectedPaymentOrder()
        If o Is Nothing Then
            Warn("Choose the order being paid for.")
            Return
        End If
        Dim amount As Decimal
        If Not TryParseMoney(pyAmount.Text, amount) OrElse amount <= 0D Then
            Warn("Enter the amount received as a number, for example 20 or 20.50.")
            Return
        End If
        If amount > o.Balance Then
            Warn("That's more than the balance of " & Money(o.Balance) & " on " & o.Id & ".")
            Return
        End If
        Dim p As New Payment With {.ReceiptNo = DataStore.NextReceiptNo(), .OrderId = o.Id, .amount = amount, .Method = Convert.ToString(pyMethod.SelectedItem),
                                   .PaidAt = DateTime.Now, .ReceivedBy = currentUser.FullName}
        o.Payments.Add(p)
        DataStore.SaveData()
        RefreshPayments()
        If Confirm("Payment of " & Money(amount) & " recorded for " & o.Id & ". Balance is now " & Money(o.Balance) & "." & Environment.NewLine & "Print a receipt?", "Payment saved") Then
            PrintReceipt(o, p)
        End If
    End Sub

    ' =================================================================
    '  PAGE 6: Sales reports
    ' =================================================================
    Private Sub BuildReportsPage()
        Dim page = NewPage("Sales Reports", 810)
        rpRange = New ComboBox With {.Location = New Point(0, 8), .Width = 220, .DropDownStyle = ComboBoxStyle.DropDownList, .FlatStyle = FlatStyle.Flat, .Font = UiFont(11.0F)}
        rpRange.Items.AddRange(New Object() {"Today", "Last 7 days", "This month", "This year", "All time"})
        rpRange.SelectedIndex = 1
        page.Controls.Add(rpRange)
        Dim bExport = MakeButton("Export report", ButtonKind.Primary, Theme.GlyphDownload)
        bExport.Location = New Point(ContentWidth - bExport.Width, 4)
        bExport.Anchor = TopRight
        page.Controls.Add(bExport)

        Dim stats = MakeStatRow(page, 56)
        rpOrders = AddStatCard(stats, 0, "Orders received", Theme.GlyphCart, Theme.Blue)
        rpValue = AddStatCard(stats, 1, "Value of orders", Theme.GlyphList, Theme.Purple)
        rpReceived = AddStatCard(stats, 2, "Money received", Theme.GlyphPayment, Theme.Green)
        rpOutstanding = AddStatCard(stats, 3, "Still to collect", Theme.GlyphWarning, Theme.Red)
        For Each l In {rpValue, rpReceived, rpOutstanding}
            l.Font = UiFont(17.0F, FontStyle.Bold)
            l.Top = 24
        Next

        Dim chartCard = AddCard(page, 0, 188, 1040, 300, "", TopLeftRight)
        rpChartTitle = New Label With {.Font = UiFont(12.5F, FontStyle.Bold), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(20, 16)}
        chartCard.Controls.Add(rpChartTitle)
        rpChart = New BarChart With {.Location = New Point(16, 60), .Size = New Size(1008, 224), .Anchor = AllSides}
        chartCard.Controls.Add(rpChart)

        Dim serviceCard = AddCard(page, 0, 508, 510, 280, "Sales by service", TopLeft)
        rpServiceGrid = AddGrid(serviceCard, 16, 56, 478, 208, AllSides, "Service", "Orders", "Weight (kg)", "Value")
        Dim methodCard = AddCard(page, 530, 508, 510, 280, "Money received by method", TopLeftRight)
        rpMethodGrid = AddGrid(methodCard, 16, 56, 478, 208, AllSides, "Method", "Payments", "Amount")

        AddHandler rpRange.SelectedIndexChanged, Sub() RefreshReports()
        AddHandler bExport.Click, Sub() ExportReport()
        pageRefreshers.Add(AddressOf RefreshReports)
    End Sub

    Private Function ReportStart() As DateTime
        Dim today = DateTime.Today
        Select Case Convert.ToString(rpRange.SelectedItem)
            Case "Today" : Return today
            Case "Last 7 days" : Return today.AddDays(-6)
            Case "This month" : Return New DateTime(today.Year, today.Month, 1)
            Case "This year" : Return New DateTime(today.Year, 1, 1)
            Case Else : Return DateTime.MinValue
        End Select
    End Function

    Private Sub RefreshReports()
        Dim start = ReportStart()
        Dim today = DateTime.Today
        Dim orders = Db.Orders.Where(Function(o) o.OrderDate >= start).ToList()
        Dim payments = DataStore.AllPayments()
        Dim paidInRange = payments.Where(Function(p) p.PaidAt >= start).ToList()

        rpOrders.Text = orders.Count.ToString()
        rpValue.Text = Money(orders.Sum(Function(o) o.Amount))
        rpReceived.Text = Money(paidInRange.Sum(Function(p) p.Amount))
        rpOutstanding.Text = Money(orders.Sum(Function(o) o.Balance))

        Dim labels As New List(Of String)()
        Dim values As New List(Of Decimal)()
        Dim range = Convert.ToString(rpRange.SelectedItem)
        If range = "Today" OrElse range = "Last 7 days" Then
            rpChartTitle.Text = "Money received, last 7 days"
            For i = 6 To 0 Step -1
                Dim d = today.AddDays(-i)
                labels.Add(d.ToString("ddd", CultureInfo.InvariantCulture))
                values.Add(payments.Where(Function(p) p.PaidAt.Date = d).Sum(Function(p) p.Amount))
            Next
        ElseIf range = "This month" Then
            rpChartTitle.Text = "Money received each day, " & today.ToString("MMMM yyyy", CultureInfo.InvariantCulture)
            For dayNumber = 1 To today.Day
                Dim d = New DateTime(today.Year, today.Month, dayNumber)
                labels.Add(dayNumber.ToString())
                values.Add(payments.Where(Function(p) p.PaidAt.Date = d).Sum(Function(p) p.Amount))
            Next
        Else
            Dim months As New List(Of DateTime)()
            If range = "This year" Then
                rpChartTitle.Text = "Money received each month, " & today.Year
                For m = 1 To today.Month
                    months.Add(New DateTime(today.Year, m, 1))
                Next
            Else
                rpChartTitle.Text = "Money received each month, last 12 months"
                For i = 11 To 0 Step -1
                    months.Add(New DateTime(today.Year, today.Month, 1).AddMonths(-i))
                Next
            End If
            For Each m In months
                Dim monthStart = m
                labels.Add(m.ToString("MMM", CultureInfo.InvariantCulture))
                values.Add(payments.Where(Function(p) p.PaidAt.Year = monthStart.Year AndAlso p.PaidAt.Month = monthStart.Month).Sum(Function(p) p.Amount))
            Next
        End If
        rpChart.SetData(labels, values, values.Count - 1)

        rpServiceGrid.Rows.Clear()
        For Each g In orders.GroupBy(Function(o) o.Service).OrderByDescending(Function(x) x.Sum(Function(o) o.Amount))
            rpServiceGrid.Rows.Add(g.Key, g.Count(), g.Sum(Function(o) o.WeightKg).ToString("0.##", CultureInfo.InvariantCulture), Money(g.Sum(Function(o) o.Amount)))
        Next
        rpMethodGrid.Rows.Clear()
        For Each g In paidInRange.GroupBy(Function(p) p.Method).OrderByDescending(Function(x) x.Sum(Function(p) p.Amount))
            rpMethodGrid.Rows.Add(g.Key, g.Count(), Money(g.Sum(Function(p) p.Amount)))
        Next
    End Sub

    Private Sub ExportReport()
        Dim start = ReportStart()
        Dim orders = Db.Orders.Where(Function(o) o.OrderDate >= start).OrderBy(Function(o) o.OrderDate).ToList()
        Dim paid = DataStore.AllPayments().Where(Function(p) p.PaidAt >= start).OrderBy(Function(p) p.PaidAt).ToList()

        Dim summary As New ExcelSheet("Summary", "Item", "Value")
        summary.AddRow("Report", Db.Business.Name & " sales report")
        summary.AddRow("Period", Convert.ToString(rpRange.SelectedItem))
        summary.AddRow("Created", DateTime.Now.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture))
        summary.AddRow("Created by", currentUser.FullName)
        summary.AddRow("Orders received", orders.Count)
        summary.AddRow("Total weight (kg)", orders.Sum(Function(o) o.WeightKg))
        summary.AddRow("Value of orders", New ExcelMoney(orders.Sum(Function(o) o.Amount)))
        summary.AddRow("Money received", New ExcelMoney(paid.Sum(Function(p) p.Amount)))
        summary.AddRow("Still to collect", New ExcelMoney(orders.Sum(Function(o) o.Balance)))

        Dim byService As New ExcelSheet("By service", "Service", "Orders", "Weight (kg)", "Value (GH₵)")
        For Each g In orders.GroupBy(Function(o) o.Service).OrderByDescending(Function(x) x.Sum(Function(o) o.Amount))
            byService.AddRow(g.Key, g.Count(), g.Sum(Function(o) o.WeightKg), New ExcelMoney(g.Sum(Function(o) o.Amount)))
        Next
        Dim byMethod As New ExcelSheet("By payment method", "Method", "Payments", "Amount (GH₵)")
        For Each g In paid.GroupBy(Function(p) p.Method).OrderByDescending(Function(x) x.Sum(Function(p) p.Amount))
            byMethod.AddRow(g.Key, g.Count(), New ExcelMoney(g.Sum(Function(p) p.Amount)))
        Next

        SaveWorkbook("sales-report-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx",
                     summary, byService, byMethod, OrdersSheet("Orders", orders), PaymentsSheet("Payments", paid))
    End Sub

    ' =================================================================
    '  PAGE 7: Workers (administrators only)
    ' =================================================================
    Private Sub BuildWorkersPage()
        Dim page = NewPage("Workers", 620)

        Dim staffCard = AddCard(page, 0, 0, 380, 430, "Add a staff member", TopLeft)
        wkName = AddTextBox(staffCard, "Full name", 20, 52, 340)
        wkPhone = AddTextBox(staffCard, "Phone number", 20, 112, 340)
        wkRole = AddCombo(staffCard, "Role", 20, 172, 340)
        wkRole.Items.AddRange(New Object() {"Staff", "Administrator"})
        wkRole.SelectedIndex = 0
        wkPassword = AddTextBox(staffCard, "Password (at least 4 characters)", 20, 232, 340)
        wkPassword.UseSystemPasswordChar = True
        Dim bAdd = AddButton(staffCard, "Add staff member", ButtonKind.Primary, Theme.GlyphAdd, 20, 300, 340)
        staffCard.Controls.Add(New Label With {.AutoSize = False, .Size = New Size(340, 60), .Location = New Point(20, 356), .Font = UiFont(9.0F), .ForeColor = Theme.Muted,
                                             .Text = "Staff can take orders, update status and record payments. Only administrators can manage staff, delete orders and change settings."})

        Dim listCard = AddCard(page, 400, 0, 640, 600, "Staff", TopLeftRight)
        Dim bReset = MakeButton("Reset password", ButtonKind.Secondary, Theme.GlyphLock)
        Dim bToggle = MakeButton("Activate / deactivate", ButtonKind.Secondary, Theme.GlyphSync)
        AddHeaderButtons(listCard, bReset, bToggle)
        wkGrid = AddGrid(listCard, 16, 68, 608, 516, AllSides, "Name", "Phone", "Role", "Status", "Orders taken")
        AttachPills(wkGrid, "Role", "Status")

        AddHandler bAdd.Click, Sub() AddWorker()
        AddHandler bToggle.Click,
            Sub()
                Dim user = SelectedWorker()
                If user Is Nothing Then Return
                If user Is currentUser Then
                    Warn("You can't deactivate your own account while you're logged in.")
                    Return
                End If
                If user.Active AndAlso user.IsAdmin AndAlso Db.Users.Where(Function(u) u.Active AndAlso u.IsAdmin).Count() <= 1 Then
                    Warn("There must always be at least one active administrator.")
                    Return
                End If
                user.Active = Not user.Active
                DataStore.SaveData()
                RefreshWorkers()
            End Sub
        AddHandler bReset.Click,
            Sub()
                Dim user = SelectedWorker()
                If user Is Nothing Then Return
                Dim newPassword = InputBox("Type a new password for " & user.FullName & " (at least 4 characters).", "Reset password").Trim()
                If newPassword = "" Then Return
                If newPassword.Length < 4 Then
                    Warn("The password must be at least 4 characters.")
                    Return
                End If
                DataStore.SetPassword(user, newPassword)
                DataStore.SaveData()
                Info("Password changed for " & user.FullName & ".")
            End Sub

        pageRefreshers.Add(AddressOf RefreshWorkers)
    End Sub

    Private Function SelectedWorker() As StaffUser
        Dim name = CurrentText(wkGrid, "Name")
        Dim user = Db.Users.FirstOrDefault(Function(u) u.FullName = name)
        If user Is Nothing Then Warn("Select a staff member first.")
        Return user
    End Function

    Private Sub RefreshWorkers()
        Dim previous = CurrentText(wkGrid, "Name")
        wkGrid.Rows.Clear()
        For Each u In Db.Users.OrderBy(Function(x) x.FullName)
            Dim userName = u.FullName
            wkGrid.Rows.Add(u.FullName, u.Phone, u.Role, If(u.Active, "Active", "Inactive"), Db.Orders.Where(Function(o) o.CreatedBy = userName).Count())
        Next
        SelectRow(wkGrid, "Name", previous)
    End Sub

    Private Sub AddWorker()
        Dim name = wkName.Text.Trim()
        If name = "" Then
            Warn("Enter the staff member's full name.")
            Return
        End If
        If Db.Users.Any(Function(u) String.Equals(u.FullName, name, StringComparison.OrdinalIgnoreCase)) Then
            Warn("There is already a staff member called " & name & ".")
            Return
        End If
        If wkPassword.Text.Length < 4 Then
            Warn("The password must be at least 4 characters.")
            Return
        End If
        Dim user As New StaffUser With {.FullName = name, .Phone = wkPhone.Text.Trim(), .Role = Convert.ToString(wkRole.SelectedItem), .Active = True}
        DataStore.SetPassword(user, wkPassword.Text)
        Db.Users.Add(user)
        DataStore.SaveData()
        wkName.Clear()
        wkPhone.Clear()
        wkPassword.Clear()
        wkRole.SelectedIndex = 0
        RefreshWorkers()
        Info(name & " can now log in from the profile list.")
    End Sub

    ' =================================================================
    '  PAGE 8: Settings
    ' =================================================================
    Private Sub BuildSettingsPage()
        Dim page = NewPage("Settings", 640)

        stBusinessCard = AddCard(page, 0, 0, 500, 340, "Business details", TopLeft)
        stName = AddTextBox(stBusinessCard, "Business name", 20, 52, 460)
        stAddress = AddTextBox(stBusinessCard, "Address", 20, 108, 460)
        stPhone = AddTextBox(stBusinessCard, "Phone number", 20, 164, 220)
        AddCaption(stBusinessCard, "Default collection time (days)", 260, 164)
        stCollectionDays = New NumericUpDown With {.Minimum = 0D, .Maximum = 60D, .Value = 2D, .Location = New Point(260, 186), .Width = 120, .Font = UiFont(10.5F)}
        stBusinessCard.Controls.Add(stCollectionDays)
        stTagline = AddTextBox(stBusinessCard, "Tagline (shown on receipts)", 20, 220, 460)
        Dim bSaveBusiness = AddButton(stBusinessCard, "Save details", ButtonKind.Primary, Theme.GlyphSave, 20, 284, 180)

        stServicesCard = AddCard(page, 520, 0, 520, 345, "Services and prices", TopLeftRight)
        stServicesGrid = MakeGrid()
        stServicesGrid.Location = New Point(16, 56)
        stServicesGrid.Size = New Size(488, 214)
        stServicesGrid.Anchor = AllSides
        stServicesGrid.ReadOnly = False
        stServicesGrid.AllowUserToAddRows = True
        stServicesGrid.AllowUserToDeleteRows = True
        stServicesGrid.SelectionMode = DataGridViewSelectionMode.CellSelect
        stServicesGrid.Columns.Add("Service", "Service")
        stServicesGrid.Columns.Add("MaxKg", "Up to (kg)")
        stServicesGrid.Columns.Add("Price", "Price (GH₵)")
        stServicesCard.Controls.Add(stServicesGrid)
        Dim bSavePrices = AddButton(stServicesCard, "Save prices", ButtonKind.Primary, Theme.GlyphSave, 16, 284, 160)
        stServicesCard.Controls.Add(New Label With {.AutoSize = False, .Size = New Size(320, 54), .Location = New Point(186, 280), .Font = UiFont(8.5F), .ForeColor = Theme.Muted, .Anchor = TopLeftRight,
                                                    .Text = "One row per price band, e.g. Washing / 80 / 90. Add a band in the empty last row. To remove one, select it and press Delete."})

        stDataCard = AddCard(page, 0, 360, 500, 260, "Backups and data", TopLeft)
        stDataLabel = New Label With {.AutoSize = False, .Size = New Size(460, 84), .Location = New Point(20, 52), .Font = UiFont(9.5F), .ForeColor = Theme.Muted}
        stDataCard.Controls.Add(stDataLabel)
        Dim bBackup = AddButton(stDataCard, "Back up now", ButtonKind.Primary, Theme.GlyphSave, 20, 150, 224)
        Dim bRestore = AddButton(stDataCard, "Restore a backup", ButtonKind.Secondary, Theme.GlyphRefresh, 256, 150, 224)
        Dim bExportAll = AddButton(stDataCard, "Export all data to Excel", ButtonKind.Success, Theme.GlyphDownload, 20, 202, 224)
        Dim bFolder = AddButton(stDataCard, "Open data folder", ButtonKind.Secondary, Theme.GlyphFolder, 256, 202, 224)

        Dim pwCard = AddCard(page, 520, 360, 520, 240, "Change my password", TopLeftRight)
        stCurrentPw = AddTextBox(pwCard, "Current password", 20, 52, 230)
        stNewPw = AddTextBox(pwCard, "New password", 20, 112, 230)
        stConfirmPw = AddTextBox(pwCard, "Confirm new password", 268, 112, 230)
        For Each tb In {stCurrentPw, stNewPw, stConfirmPw}
            tb.UseSystemPasswordChar = True
        Next
        Dim bChangePw = AddButton(pwCard, "Change password", ButtonKind.Primary, Theme.GlyphLock, 20, 180, 200)

        AddHandler bSaveBusiness.Click,
            Sub()
                If stName.Text.Trim() = "" Then
                    Warn("The business name can't be empty.")
                    Return
                End If
                Db.Business.Name = stName.Text.Trim()
                Db.Business.Address = stAddress.Text.Trim()
                Db.Business.Phone = stPhone.Text.Trim()
                Db.Business.Tagline = stTagline.Text.Trim()
                Db.Business.DefaultCollectionDays = CInt(stCollectionDays.Value)
                DataStore.SaveData()
                UpdateClock()
                Info("Business details saved.")
            End Sub
        AddHandler bSavePrices.Click, Sub() SaveServices()
        AddHandler bBackup.Click,
            Sub()
                Dim target = DataStore.BackupNow()
                RefreshSettings()
                Info("Backup saved:" & Environment.NewLine & target)
            End Sub
        AddHandler bRestore.Click, Sub() RestoreBackup()
        AddHandler bExportAll.Click, Sub() ExportAllData()
        AddHandler bFolder.Click, Sub() Process.Start(New ProcessStartInfo("explorer.exe", """" & DataStore.DataFolder() & """"))
        AddHandler bChangePw.Click, Sub() ChangeMyPassword()

        pageRefreshers.Add(AddressOf RefreshSettings)
    End Sub

    Private Sub RefreshSettings()
        Dim isAdmin = currentUser.IsAdmin
        stBusinessCard.Enabled = isAdmin
        stServicesCard.Enabled = isAdmin
        stDataCard.Enabled = isAdmin
        stName.Text = Db.Business.Name
        stAddress.Text = Db.Business.Address
        stPhone.Text = Db.Business.Phone
        stTagline.Text = Db.Business.Tagline
        stCollectionDays.Value = Math.Max(0, Math.Min(60, Db.Business.DefaultCollectionDays))
        stServicesGrid.Rows.Clear()
        For Each s In Db.Services.OrderBy(Function(x) x.Name).ThenBy(Function(x) x.MaxKg)
            stServicesGrid.Rows.Add(s.Name, s.MaxKg.ToString("0.##", CultureInfo.InvariantCulture), s.Price.ToString("0.00", CultureInfo.InvariantCulture))
        Next
        Dim backupText = If(Db.LastBackup = DateTime.MinValue, "No backup yet.", "Last backup: " & Db.LastBackup.ToString("ddd dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture) & ".")
        stDataLabel.Text = "Everything is saved automatically to:" & Environment.NewLine & DataStore.DataFile() & Environment.NewLine &
                           backupText & " A backup is made automatically each day."
        If Not isAdmin Then stDataLabel.Text &= Environment.NewLine & "Only administrators can change these settings."
    End Sub

    Private Sub SaveServices()
        stServicesGrid.EndEdit()
        Dim list As New List(Of ServicePrice)()
        For Each row As DataGridViewRow In stServicesGrid.Rows
            If row.IsNewRow Then Continue For
            Dim name = Convert.ToString(row.Cells("Service").Value).Trim()
            Dim kgText = Convert.ToString(row.Cells("MaxKg").Value).Trim().Replace("kg", "").Trim()
            Dim priceText = Convert.ToString(row.Cells("Price").Value).Trim()
            If name = "" AndAlso kgText = "" AndAlso priceText = "" Then Continue For
            Dim maxKg As Decimal
            Dim price As Decimal
            If name = "" OrElse Not Decimal.TryParse(kgText, NumberStyles.Number, CultureInfo.InvariantCulture, maxKg) OrElse maxKg <= 0D OrElse
               Not TryParseMoney(priceText, price) OrElse price < 0D Then
                Warn("Check row " & (row.Index + 1) & ": each band needs a service name, a weight such as 80, and a price such as 90 or 90.50.")
                Return
            End If
            Dim kgCheck = maxKg
            If list.Any(Function(x) String.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) AndAlso x.MaxKg = kgCheck) Then
                Warn("'" & name & "' up to " & Helpers.Kg(maxKg) & " is listed twice.")
                Return
            End If
            list.Add(New ServicePrice With {.name = name, .maxKg = maxKg, .price = price})
        Next
        If list.Count = 0 Then
            Warn("Add at least one price band.")
            Return
        End If
        Db.Services = list
        DataStore.SaveData()
        RefreshSettings()
        Info("Prices saved. New orders will use them; existing orders keep the price they were given.")
    End Sub

    Private Sub RestoreBackup()
        Using dlg As New OpenFileDialog With {.Filter = "Laundry backups (*.xml)|*.xml", .InitialDirectory = DataStore.BackupFolder(), .Title = "Choose a backup to restore"}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If Not Confirm("Replace ALL current data with this backup?" & Environment.NewLine & dlg.FileName & Environment.NewLine & Environment.NewLine &
                           "Your current data will be backed up first, so you can undo this.", "Restore backup") Then Return
            If DataStore.RestoreBackup(dlg.FileName) Then
                Info("Backup restored. Please log in again.")
                BuildLoginScreen()
            Else
                Warn("That file isn't a valid Laundry Tracking System backup. Nothing was changed.")
            End If
        End Using
    End Sub

    Private Sub ExportAllData()
        Dim customers As New ExcelSheet("Customers", "Id", "Name", "Phone", "Orders", "Total paid (GH₵)", "Balance (GH₵)", "Notes")
        For Each c In Db.Customers.OrderBy(Function(x) x.Name)
            Dim id = c.Id
            Dim mine = Db.Orders.Where(Function(o) o.CustomerId = id).ToList()
            customers.AddRow(c.Id, c.Name, c.Phone, mine.Count, New ExcelMoney(mine.Sum(Function(o) o.PaidAmount)), New ExcelMoney(mine.Sum(Function(o) o.Balance)), c.Notes)
        Next
        Dim staff As New ExcelSheet("Staff", "Name", "Phone", "Role", "Status", "Orders taken")
        For Each u In Db.Users.OrderBy(Function(x) x.FullName)
            Dim userName = u.FullName
            staff.AddRow(u.FullName, u.Phone, u.Role, If(u.Active, "Active", "Inactive"), Db.Orders.Where(Function(o) o.CreatedBy = userName).Count())
        Next
        Dim prices As New ExcelSheet("Price list", "Service", "Up to (kg)", "Price (GH₵)")
        For Each sp In Db.Services.OrderBy(Function(x) x.Name).ThenBy(Function(x) x.MaxKg)
            prices.AddRow(sp.Name, sp.MaxKg, New ExcelMoney(sp.Price))
        Next
        SaveWorkbook("laundry-all-data-" & DateTime.Today.ToString("yyyy-MM-dd") & ".xlsx",
                     OrdersSheet("Orders", Db.Orders.OrderBy(Function(o) o.OrderDate).ToList()),
                     PaymentsSheet("Payments", DataStore.AllPayments().OrderBy(Function(p) p.PaidAt).ToList()),
                     customers, staff, prices)
    End Sub

    Private Sub ChangeMyPassword()
        If Not DataStore.CheckPassword(currentUser, stCurrentPw.Text) Then
            Warn("Your current password is not correct.")
            Return
        End If
        If stNewPw.Text.Length < 4 Then
            Warn("The new password must be at least 4 characters.")
            Return
        End If
        If stNewPw.Text <> stConfirmPw.Text Then
            Warn("The new passwords don't match.")
            Return
        End If
        DataStore.SetPassword(currentUser, stNewPw.Text)
        DataStore.SaveData()
        stCurrentPw.Clear()
        stNewPw.Clear()
        stConfirmPw.Clear()
        Info("Your password has been changed.")
    End Sub

    ' =================================================================
    '  Export to Excel (.xlsx)
    ' =================================================================
    Private Function OrdersSheet(name As String, orders As IEnumerable(Of LaundryOrder)) As ExcelSheet
        Dim sheet As New ExcelSheet(name, "Order #", "Date", "Customer", "Phone", "Service", "Weight (kg)", "Price band",
                                    "Amount (GH₵)", "Paid (GH₵)", "Balance (GH₵)", "Payment", "Status", "Collection date", "Taken by", "Items received", "Notes")
        For Each o In orders
            sheet.AddRow(o.Id, o.OrderDate.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture), o.CustomerName, o.Phone, o.Service,
                         If(o.WeightKg > 0D, CObj(o.WeightKg), CObj(o.SizeText)), o.PricingNote,
                         New ExcelMoney(o.Amount), New ExcelMoney(o.PaidAmount), New ExcelMoney(o.Balance), o.PaymentStatus, o.Status,
                         o.DueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), o.CreatedBy, o.ItemsDescription, o.Notes)
        Next
        Return sheet
    End Function

    Private Function PaymentsSheet(name As String, payments As IEnumerable(Of Payment)) As ExcelSheet
        Dim sheet As New ExcelSheet(name, "Receipt #", "Date", "Order #", "Customer", "Amount (GH₵)", "Method", "Received by")
        For Each p In payments
            Dim o = DataStore.FindOrder(p.OrderId)
            sheet.AddRow(p.ReceiptNo, p.PaidAt.ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture), p.OrderId,
                         If(o Is Nothing, "", o.CustomerName), New ExcelMoney(p.Amount), p.Method, p.ReceivedBy)
        Next
        Return sheet
    End Function

    Private Sub SaveWorkbook(fileName As String, ParamArray sheets As ExcelSheet())
        Using dlg As New SaveFileDialog With {.Filter = "Excel workbook (*.xlsx)|*.xlsx", .FileName = fileName, .DefaultExt = "xlsx", .AddExtension = True}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                ExcelWriter.Save(dlg.FileName, sheets)
            Catch ex As IOException
                Warn("The file couldn't be saved. If it's open in Excel, close it and try again.")
                Return
            End Try
            If Confirm("Saved to " & dlg.FileName & Environment.NewLine & "Open it in Excel now?", "Export to Excel") Then
                Try
                    Process.Start(New ProcessStartInfo(dlg.FileName) With {.UseShellExecute = True})
                Catch
                    Warn("The file was saved, but Excel couldn't be opened automatically. Open it from the folder you saved it in.")
                End Try
            End If
        End Using
    End Sub

    ' =================================================================
    '  Edit an order (administrators): service, weight, price, date, notes
    ' =================================================================
    Private Sub EditOrder(o As LaundryOrder)
        If o Is Nothing Then
            Warn("Select an order first.")
            Return
        End If
        Using dlg As New Form With {.Text = "Edit order " & o.Id, .FormBorderStyle = FormBorderStyle.FixedDialog, .MaximizeBox = False, .MinimizeBox = False,
                                    .StartPosition = FormStartPosition.CenterParent, .ClientSize = New Size(560, 560), .BackColor = Theme.Background,
                                    .Font = UiFont(9.75F), .ShowInTaskbar = False}
            Dim card As New RoundedPanel With {.Location = New Point(16, 16), .Size = New Size(528, 528)}
            dlg.Controls.Add(card)
            card.Controls.Add(New Label With {.Text = o.Id & "  ·  " & o.CustomerName, .Font = UiFont(12.5F, FontStyle.Bold), .ForeColor = Theme.TextDark, .AutoSize = True, .Location = New Point(20, 16)})

            Dim cmbService = AddCombo(card, "Service", 20, 52, 240)
            For Each serviceItem In Pricing.ServiceNames(Db.Services)
                cmbService.Items.Add(serviceItem)
            Next
            If Not cmbService.Items.Contains(o.Service) Then cmbService.Items.Add(o.Service)
            cmbService.SelectedItem = o.Service

            AddCaption(card, "Weight (kg)", 280, 52)
            Dim numWeight As New NumericUpDown With {.DecimalPlaces = 1, .Increment = 0.5D, .Minimum = 0D, .Maximum = 1000D, .Location = New Point(280, 74), .Width = 140, .Font = UiFont(10.5F)}
            numWeight.Value = Math.Min(1000D, o.WeightKg)
            card.Controls.Add(numWeight)

            Dim txtPrice = AddTextBox(card, "Price (GH₵). Fills in from the price list; you can change it.", 20, 116, 240)
            txtPrice.Text = o.Amount.ToString("0.00", CultureInfo.InvariantCulture)
            Dim lblBand As New Label With {.Text = o.PricingNote, .AutoSize = False, .Size = New Size(480, 22), .Location = New Point(20, 172), .Font = UiFont(9.5F), .ForeColor = Theme.Muted}
            card.Controls.Add(lblBand)

            Dim dpDue = AddDatePicker(card, "Collection date", 20, 204, 200)
            dpDue.Value = If(o.DueDate < dpDue.MinDate, DateTime.Today, o.DueDate)
            Dim txtItems = AddTextBox(card, "Items received", 20, 268, 480, 56)
            txtItems.Text = o.ItemsDescription
            Dim txtNotes = AddTextBox(card, "Notes", 20, 350, 480)
            txtNotes.Text = o.Notes
            card.Controls.Add(New Label With {.Text = "Already paid: " & Money(o.PaidAmount) & ".  Record new payments on the Payments page.", .AutoSize = False, .Size = New Size(480, 22),
                                              .Location = New Point(20, 410), .Font = SemiFont(9.5F), .ForeColor = Theme.TextDark})

            Dim recalc = Sub()
                             Dim quote = Pricing.Quote(Db.Services, Convert.ToString(cmbService.SelectedItem), numWeight.Value)
                             If quote IsNot Nothing Then
                                 txtPrice.Text = quote.Price.ToString("0.00", CultureInfo.InvariantCulture)
                                 lblBand.Text = quote.Note
                             End If
                         End Sub
            AddHandler cmbService.SelectedIndexChanged, Sub() recalc()
            AddHandler numWeight.ValueChanged, Sub() recalc()

            Dim bSave = AddButton(card, "Save changes", ButtonKind.Primary, Theme.GlyphSave, 20, 456, 200)
            Dim bCancel = AddButton(card, "Cancel", ButtonKind.Secondary, "", 232, 456, 140)
            AddHandler bCancel.Click, Sub() dlg.Close()
            AddHandler bSave.Click,
                Sub()
                    Dim price As Decimal
                    If Not TryParseMoney(txtPrice.Text, price) OrElse price < 0D Then
                        Warn("Enter the price as a number, for example 90 or 90.50.")
                        Return
                    End If
                    If price < o.PaidAmount Then
                        Warn("The customer has already paid " & Money(o.PaidAmount) & ", so the price can't be lower than that." & Environment.NewLine &
                             "Delete the extra payment on the Payments page first if it was a mistake.")
                        Return
                    End If
                    If numWeight.Value <= 0D AndAlso o.WeightKg > 0D Then
                        Warn("Enter the weight in kg.")
                        Return
                    End If
                    Dim oldPrice = o.Amount
                    o.Service = Convert.ToString(cmbService.SelectedItem)
                    o.WeightKg = numWeight.Value
                    o.Amount = price
                    o.UnitPrice = price
                    Dim quote = Pricing.Quote(Db.Services, o.Service, o.WeightKg)
                    o.PricingNote = If(quote IsNot Nothing AndAlso quote.Price = price, quote.Note, "Price set by " & currentUser.FullName)
                    o.DueDate = dpDue.Value.Date
                    o.ItemsDescription = txtItems.Text.Trim()
                    o.Notes = txtNotes.Text.Trim()
                    o.History.Add(New StatusChange With {.Status = "Order edited" & If(oldPrice <> price, " (price " & Money(oldPrice) & " to " & Money(price) & ")", ""),
                                                         .ChangedAt = DateTime.Now, .ChangedBy = currentUser.FullName})
                    DataStore.SaveData()
                    dlg.DialogResult = DialogResult.OK
                    dlg.Close()
                End Sub
            dlg.AcceptButton = bSave
            dlg.CancelButton = bCancel
            dlg.ShowDialog(Me)
        End Using
    End Sub

    ' =================================================================
    '  Receipts (print preview, then print)
    ' =================================================================
    Private Sub PrintReceipt(o As LaundryOrder, p As Payment)
        If o Is Nothing Then Return
        Using doc As New PrintDocument()
            doc.DocumentName = If(p Is Nothing, "Order slip " & o.Id, "Receipt " & p.ReceiptNo)
            AddHandler doc.PrintPage,
                Sub(s As Object, e As PrintPageEventArgs)
                    DrawReceipt(e.Graphics, o, p, e.MarginBounds)
                    e.HasMorePages = False
                End Sub
            Using preview As New PrintPreviewDialog With {.Document = doc, .Width = 760, .Height = 860, .StartPosition = FormStartPosition.CenterParent}
                preview.ShowDialog(Me)
            End Using
        End Using
    End Sub

    Private Sub DrawReceipt(g As Graphics, o As LaundryOrder, p As Payment, area As Rectangle)
        Dim x As Single = area.Left
        Dim w As Single = 340
        Dim y As Single = area.Top
        Dim biz = Db.Business
        Using titleFont As New Font("Segoe UI", 13.0F, FontStyle.Bold),
              normal As New Font("Segoe UI", 9.5F),
              bold As New Font("Segoe UI", 9.5F, FontStyle.Bold),
              small As New Font("Segoe UI", 8.5F),
              big As New Font("Segoe UI", 12.0F, FontStyle.Bold),
              center As New StringFormat With {.Alignment = StringAlignment.Center},
              right As New StringFormat With {.Alignment = StringAlignment.Far}

            Dim line = Sub()
                           y += 4
                           g.DrawLine(Pens.Gray, x, y, x + w, y)
                           y += 8
                       End Sub
            Dim row = Sub(label As String, value As String, valueFont As Font)
                          g.DrawString(label, normal, Brushes.DimGray, x, y)
                          g.DrawString(value, valueFont, Brushes.Black, New RectangleF(x + 110, y, w - 110, 20), right)
                          y += 20
                      End Sub

            Dim logo = GetLogo()
            If logo IsNot Nothing Then
                Dim logoHeight = w * logo.Height / logo.Width
                g.DrawImage(logo, x, y, w, CSng(logoHeight))
                y += CSng(logoHeight) + 4
            Else
                g.DrawString(biz.Name, titleFont, Brushes.Black, New RectangleF(x, y, w, 28), center)
                y += 28
            End If
            If biz.Address <> "" Then
                g.DrawString(biz.Address, small, Brushes.Black, New RectangleF(x, y, w, 18), center)
                y += 16
            End If
            If biz.Phone <> "" Then
                g.DrawString("Tel: " & biz.Phone, small, Brushes.Black, New RectangleF(x, y, w, 18), center)
                y += 16
            End If
            line()
            g.DrawString(If(p Is Nothing, "ORDER SLIP", "PAYMENT RECEIPT"), big, Brushes.Black, New RectangleF(x, y, w, 24), center)
            y += 28

            If p IsNot Nothing Then row("Receipt #", p.ReceiptNo, bold)
            row("Date", If(p Is Nothing, DateTime.Now, p.PaidAt).ToString("dd MMM yyyy, h:mm tt", CultureInfo.InvariantCulture), normal)
            row("Order #", o.Id, bold)
            row("Customer", o.CustomerName, normal)
            row("Phone", o.Phone, normal)
            line()
            row("Service", o.Service, normal)
            row(If(o.WeightKg > 0D, "Weight", "Items"), o.SizeText, normal)
            If o.PricingNote <> "" Then
                g.DrawString(o.PricingNote, small, Brushes.DimGray, New RectangleF(x, y, w, 18))
                y += 18
            End If
            If o.ItemsDescription <> "" Then
                g.DrawString(o.ItemsDescription, small, Brushes.Black, New RectangleF(x, y, w, 36))
                y += 34
            End If
            row("Collection date", o.DueDate.ToString("ddd dd MMM yyyy", CultureInfo.InvariantCulture), normal)
            row("Status", o.Status, normal)
            line()
            row("Order total", Money(o.Amount), bold)
            If p IsNot Nothing Then row("Paid now (" & p.Method & ")", Money(p.Amount), normal)
            row("Total paid", Money(o.PaidAmount), normal)
            row("Balance", Money(o.Balance), big)
            line()
            row("Served by", If(p Is Nothing, o.CreatedBy, p.ReceivedBy), normal)
            y += 8
            g.DrawString(biz.Tagline, bold, Brushes.Black, New RectangleF(x, y, w, 20), center)
            y += 20
            g.DrawString("Please bring this slip when you collect your clothes.", small, Brushes.DimGray, New RectangleF(x, y, w, 20), center)
        End Using
    End Sub
End Class
