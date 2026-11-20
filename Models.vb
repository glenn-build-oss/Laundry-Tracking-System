Option Strict On

Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq

' =====================================================================
'  Data models. These are saved to data.xml by DataStore.
' =====================================================================

Public Module OrderStatus
    Public Const Pending As String = "Pending"
    Public Const Washing As String = "Washing"
    Public Const Ironing As String = "Ironing"
    Public Const Ready As String = "Ready for Collection"
    Public Const Collected As String = "Collected"
    Public ReadOnly All As String() = {Pending, Washing, Ironing, Ready, Collected}

    ' The status that comes after this one, or "" if the order is finished.
    Public Function NextOf(status As String) As String
        Dim i = Array.IndexOf(All, status)
        If i < 0 OrElse i >= All.Length - 1 Then Return ""
        Return All(i + 1)
    End Function
End Module

Public Class StaffUser
    Public Property FullName As String = ""
    Public Property Phone As String = ""
    Public Property Role As String = "Staff"
    Public Property PasswordHash As String = ""
    Public Property Salt As String = ""
    Public Property Active As Boolean = True
    Public Property CreatedAt As DateTime = DateTime.Now

    Public ReadOnly Property IsAdmin As Boolean
        Get
            Return Role = "Administrator"
        End Get
    End Property

    Public ReadOnly Property Initials As String
        Get
            Return Helpers.MakeInitials(FullName)
        End Get
    End Property
End Class

Public Class Customer
    Public Property Id As Integer
    Public Property Name As String = ""
    Public Property Phone As String = ""
    Public Property Notes As String = ""
    Public Property CreatedAt As DateTime = DateTime.Now
End Class

Public Class Payment
    Public Property ReceiptNo As String = ""
    Public Property OrderId As String = ""
    Public Property Amount As Decimal
    Public Property Method As String = "Cash"
    Public Property PaidAt As DateTime = DateTime.Now
    Public Property ReceivedBy As String = ""
End Class

Public Class StatusChange
    Public Property Status As String = ""
    Public Property ChangedAt As DateTime = DateTime.Now
    Public Property ChangedBy As String = ""
End Class

' One price band. Example: Washing, up to 80 kg, GH₵ 90.
Public Class ServicePrice
    Public Property Name As String = ""
    Public Property MaxKg As Decimal
    Public Property Price As Decimal
End Class

Public Class PriceQuote
    Public Property Price As Decimal
    Public Property Note As String = ""
End Class

Public Module Pricing
    ' Finds the price for a weight. Uses the smallest band that fits the weight.
    ' Heavier than the biggest band: charged as several loads of the biggest band.
    Public Function Quote(services As List(Of ServicePrice), serviceName As String, weightKg As Decimal) As PriceQuote
        If weightKg <= 0D OrElse String.IsNullOrEmpty(serviceName) Then Return Nothing
        Dim bands = services.Where(Function(s) String.Equals(s.Name, serviceName, StringComparison.OrdinalIgnoreCase) AndAlso s.MaxKg > 0D).OrderBy(Function(s) s.MaxKg).ToList()
        If bands.Count = 0 Then Return Nothing
        Dim band = bands.FirstOrDefault(Function(b) weightKg <= b.MaxKg)
        If band IsNot Nothing Then
            Return New PriceQuote With {.Price = band.Price, .Note = "Up to " & Helpers.Kg(band.MaxKg) & " band"}
        End If
        Dim biggest = bands.Last()
        Dim loads = CInt(Math.Ceiling(weightKg / biggest.MaxKg))
        Return New PriceQuote With {.Price = biggest.Price * loads, .Note = loads & " loads of " & Helpers.Kg(biggest.MaxKg) & " (" & Helpers.Money(biggest.Price) & " each)"}
    End Function

    Public Function ServiceNames(services As List(Of ServicePrice)) As List(Of String)
        Return services.Select(Function(s) s.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(Function(n) n).ToList()
    End Function
End Module

Public Class BusinessInfo
    Public Property Name As String = "Laundry Tracking System"
    Public Property Address As String = ""
    Public Property Phone As String = ""
    Public Property Tagline As String = "Clean Clothes. Happy Customers."
    Public Property DefaultCollectionDays As Integer = 2
End Class

Public Class LaundryOrder
    Public Property Id As String = ""
    Public Property CustomerId As Integer
    Public Property CustomerName As String = ""
    Public Property Phone As String = ""
    Public Property Service As String = ""
    Public Property ItemCount As Integer = 1          ' only used by old orders priced per item
    Public Property WeightKg As Decimal
    Public Property PricingNote As String = ""
    Public Property ItemsDescription As String = ""
    Public Property UnitPrice As Decimal
    Public Property Amount As Decimal
    Public Property Notes As String = ""
    Public Property OrderDate As DateTime = DateTime.Now
    Public Property DueDate As DateTime = DateTime.Today
    Public Property Status As String = OrderStatus.Pending
    Public Property StatusChangedAt As DateTime = DateTime.Now
    Public Property CreatedBy As String = ""
    Public Property Payments As New List(Of Payment)()
    Public Property History As New List(Of StatusChange)()

    Public ReadOnly Property PaidAmount As Decimal
        Get
            Return Payments.Sum(Function(p) p.Amount)
        End Get
    End Property

    Public ReadOnly Property Balance As Decimal
        Get
            Return Math.Max(0D, Amount - PaidAmount)
        End Get
    End Property

    Public ReadOnly Property PaymentStatus As String
        Get
            If Balance <= 0D Then Return "Paid"
            If PaidAmount <= 0D Then Return "Unpaid"
            Return "Part paid"
        End Get
    End Property

    ' "80 kg" for weighed orders, "3 items" for older orders.
    Public ReadOnly Property SizeText As String
        Get
            If WeightKg > 0D Then Return Helpers.Kg(WeightKg)
            Return ItemCount & " item" & If(ItemCount = 1, "", "s")
        End Get
    End Property

    Public ReadOnly Property IsOverdue As Boolean
        Get
            Return Status <> OrderStatus.Ready AndAlso Status <> OrderStatus.Collected AndAlso DueDate.Date < DateTime.Today
        End Get
    End Property
End Class

Public Class AppData
    Public Property Users As New List(Of StaffUser)()
    Public Property Customers As New List(Of Customer)()
    Public Property Orders As New List(Of LaundryOrder)()
    Public Property Services As New List(Of ServicePrice)()
    Public Property Business As New BusinessInfo()
    Public Property NextOrderNumber As Integer = 1
    Public Property NextCustomerId As Integer = 1
    Public Property NextReceiptNumber As Integer = 1
    Public Property LastBackup As DateTime = DateTime.MinValue
End Class

' Item shown in a ComboBox: displays Text, remembers Key.
Public Class ComboItem
    Public Property Text As String
    Public Property Key As String

    Public Sub New(text As String, key As String)
        Me.Text = text
        Me.Key = key
    End Sub

    Public Overrides Function ToString() As String
        Return Text
    End Function
End Class

Public Module Helpers
    Public Function Money(value As Decimal) As String
        Return "GH₵ " & value.ToString("N2", CultureInfo.InvariantCulture)
    End Function

    ' Accepts "20", "20.50", "GH₵ 1,200.00" and similar.
    Public Function TryParseMoney(text As String, ByRef value As Decimal) As Boolean
        Dim cleaned = text.Replace("GH₵", "").Replace("₵", "").Replace("GHS", "").Replace(",", "").Trim()
        Return Decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, value)
    End Function

    Public Function Kg(value As Decimal) As String
        Return value.ToString("0.##", CultureInfo.InvariantCulture) & " kg"
    End Function

    Public Function MakeInitials(name As String) As String
        Dim parts = name.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        If parts.Length = 0 Then Return "?"
        If parts.Length = 1 Then Return parts(0).Substring(0, 1).ToUpperInvariant()
        Return (parts(0).Substring(0, 1) & parts(parts.Length - 1).Substring(0, 1)).ToUpperInvariant()
    End Function

    Public Function FirstName(name As String) As String
        Dim parts = name.Split(New Char() {" "c}, StringSplitOptions.RemoveEmptyEntries)
        Return If(parts.Length > 0, parts(0), name)
    End Function

    Public Function DigitsOnly(text As String) As String
        Return New String(text.Where(Function(c) Char.IsDigit(c)).ToArray())
    End Function
End Module
