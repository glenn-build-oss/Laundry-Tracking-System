Option Strict On

Imports System.Collections.Generic
Imports System.IO
Imports System.Linq
Imports System.Security.Cryptography
Imports System.Xml.Serialization

' =====================================================================
'  Saves everything to:  %AppData%\LaundryTrackingSystem\data.xml
'  Daily backups go to:  %AppData%\LaundryTrackingSystem\Backups
'  Nothing to install: it uses only what comes with .NET.
' =====================================================================
Public Module DataStore
    Public Data As New AppData()
    Private ReadOnly serializer As New XmlSerializer(GetType(AppData))

    Public Function DataFolder() As String
        Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LaundryTrackingSystem")
        Directory.CreateDirectory(folder)
        Return folder
    End Function

    Public Function DataFile() As String
        Return Path.Combine(DataFolder(), "data.xml")
    End Function

    Public Function BackupFolder() As String
        Dim folder = Path.Combine(DataFolder(), "Backups")
        Directory.CreateDirectory(folder)
        Return folder
    End Function

    Public Sub LoadData()
        Dim filePath = DataFile()
        If File.Exists(filePath) Then
            Try
                Using stream = File.OpenRead(filePath)
                    Data = DirectCast(serializer.Deserialize(stream), AppData)
                End Using
            Catch ex As Exception
                Dim brokenCopy = filePath & ".unreadable-" & DateTime.Now.ToString("yyyyMMdd-HHmmss")
                Try
                    File.Copy(filePath, brokenCopy, True)
                Catch
                End Try
                MessageBox.Show("The saved data could not be read, so the system started with empty data." & Environment.NewLine &
                                "Your old file was kept here:" & Environment.NewLine & brokenCopy & Environment.NewLine & Environment.NewLine &
                                "You can restore a backup from the Backups folder in Settings.",
                                "Laundry Tracking System", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Data = New AppData()
            End Try
        End If
        Seed()
        SaveData()
    End Sub

    Private Sub Seed()
        If Data.Users Is Nothing Then Data.Users = New List(Of StaffUser)()
        If Data.Customers Is Nothing Then Data.Customers = New List(Of Customer)()
        If Data.Orders Is Nothing Then Data.Orders = New List(Of LaundryOrder)()
        If Data.Services Is Nothing Then Data.Services = New List(Of ServicePrice)()
        If Data.Business Is Nothing Then Data.Business = New BusinessInfo()

        ' Default profiles. Each one is added only if no profile with that name exists yet,
        ' so passwords changed later in the app are never overwritten.
        AddProfileIfMissing("Glenn Adjei", "Administrator", "10329091")
        AddProfileIfMissing("Promise Lapoa", "Staff", "10334435")
        AddProfileIfMissing("Jerry Adjah Torgbor", "Staff", "10334547")
        AddProfileIfMissing("Husana Abubakar", "Staff", "10334592")
        AddProfileIfMissing("Quartey Ibrahim", "Staff", "10333961")
        AddProfileIfMissing("Duke Bonsu", "Staff", "10324787")
        AddProfileIfMissing("Kwafo Eugene", "Staff", "10318698")

        ' One-time fix: if Duke's profile was saved with the earlier typo password, correct it.
        Dim duke = Data.Users.FirstOrDefault(Function(u) u.FullName = "Duke Bonsu")
        If duke IsNot Nothing AndAlso CheckPassword(duke, "1032787") Then SetPassword(duke, "10324787")

        ' Prices are by weight. Old per-item prices (no weight band) are replaced with a starter band
        ' that the administrator can change in Settings.
        Data.Services.RemoveAll(Function(s) s.MaxKg <= 0D)
        If Data.Services.Count = 0 Then
            Data.Services.Add(New ServicePrice With {.Name = "Washing", .MaxKg = 80D, .Price = 90D})
        End If
        If Data.Business.DefaultCollectionDays < 0 Then Data.Business.DefaultCollectionDays = 2
    End Sub

    Private Sub AddProfileIfMissing(fullName As String, role As String, password As String)
        If Data.Users.Any(Function(u) String.Equals(u.FullName, fullName, StringComparison.OrdinalIgnoreCase)) Then Return
        Dim user As New StaffUser With {.FullName = fullName, .Role = role, .Active = True}
        SetPassword(user, password)
        Data.Users.Add(user)
    End Sub

    Public Sub SaveData()
        Dim filePath = DataFile()
        Dim tempPath = filePath & ".tmp"
        Try
            Using stream = File.Create(tempPath)
                serializer.Serialize(stream, Data)
            End Using
            File.Copy(tempPath, filePath, True)
            File.Delete(tempPath)
        Catch ex As Exception
            MessageBox.Show("Your changes could not be saved: " & ex.Message, "Save failed", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    Public Function BackupNow() As String
        Data.LastBackup = DateTime.Now
        SaveData()
        Dim target = Path.Combine(BackupFolder(), "data-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".xml")
        File.Copy(DataFile(), target, True)
        ' Keep the 30 newest backups.
        Dim oldFiles = New DirectoryInfo(BackupFolder()).GetFiles("data-*.xml").OrderByDescending(Function(f) f.CreationTime).Skip(30).ToList()
        For Each f In oldFiles
            Try
                f.Delete()
            Catch
            End Try
        Next
        Return target
    End Function

    ' Replaces the current data with a backup file. The current data is backed up first.
    ' Returns False (and changes nothing) if the file is not a valid backup.
    Public Function RestoreBackup(sourcePath As String) As Boolean
        Dim restored As AppData = Nothing
        Try
            Using stream = File.OpenRead(sourcePath)
                restored = DirectCast(serializer.Deserialize(stream), AppData)
            End Using
        Catch
            Return False
        End Try
        If restored Is Nothing Then Return False
        BackupNow()
        Data = restored
        Seed()
        SaveData()
        Return True
    End Function

    ' Makes one backup per day, the first time the app opens that day.
    Public Sub AutoBackup()
        If Data.LastBackup.Date < DateTime.Today Then
            Try
                BackupNow()
            Catch
            End Try
        End If
    End Sub

    ' ---------- Passwords (salted PBKDF2 hash, never stored as plain text) ----------
    Public Sub SetPassword(user As StaffUser, password As String)
        Dim saltBytes(15) As Byte
        Using rng = RandomNumberGenerator.Create()
            rng.GetBytes(saltBytes)
        End Using
        user.Salt = Convert.ToBase64String(saltBytes)
        user.PasswordHash = HashPassword(password, saltBytes)
    End Sub

    Public Function CheckPassword(user As StaffUser, password As String) As Boolean
        If String.IsNullOrEmpty(user.Salt) Then Return False
        Return HashPassword(password, Convert.FromBase64String(user.Salt)) = user.PasswordHash
    End Function

    Private Function HashPassword(password As String, salt As Byte()) As String
        Using kdf As New Rfc2898DeriveBytes(password, salt, 20000)
            Return Convert.ToBase64String(kdf.GetBytes(32))
        End Using
    End Function

    ' ---------- IDs and lookups ----------
    Public Function NextOrderId() As String
        Dim id = "LND-" & Data.NextOrderNumber.ToString("0000")
        Data.NextOrderNumber += 1
        Return id
    End Function

    Public Function NextReceiptNo() As String
        Dim id = "RCT-" & Data.NextReceiptNumber.ToString("00000")
        Data.NextReceiptNumber += 1
        Return id
    End Function

    Public Function NextCustomerId() As Integer
        Dim id = Data.NextCustomerId
        Data.NextCustomerId += 1
        Return id
    End Function

    Public Function FindOrder(id As String) As LaundryOrder
        If String.IsNullOrEmpty(id) Then Return Nothing
        Return Data.Orders.FirstOrDefault(Function(o) o.Id = id)
    End Function

    Public Function FindCustomer(id As Integer) As Customer
        Return Data.Customers.FirstOrDefault(Function(c) c.Id = id)
    End Function

    Public Function FindCustomerByPhone(phone As String) As Customer
        Dim digits = Helpers.DigitsOnly(phone)
        If digits.Length < 6 Then Return Nothing
        Return Data.Customers.FirstOrDefault(Function(c) Helpers.DigitsOnly(c.Phone) = digits)
    End Function

    Public Function AllPayments() As List(Of Payment)
        Return Data.Orders.SelectMany(Function(o) o.Payments).ToList()
    End Function
End Module
