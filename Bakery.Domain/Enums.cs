namespace Bakery.Domain.Enums
{
    public enum PaymentMethod
    {
        Cash = 1,           // كاش
        BankTransfer = 2,   // تحويل بنكي
        PartiallyPaid = 3,  // مدفوع جزئياً
        Unpaid = 4          // غير مدفوع
    }

    public enum TransactionType
    {
        Purchase = 1,              // توريد / شراء
        ProductionDeduction = 2,   // خصم إنتاج
        ManualAdjustment = 3       // تسوية يدوية
    }

    public enum ProductionStatus
    {
        Draft = 1,       // مسودة
        Confirmed = 2    // مؤكد
    }

    public enum ProductType
    {
        Mabroum = 1,    // مبروم
        Pane = 2,       // بانيه
        Sandwich = 3    // ساندوتش
    }

    public enum TreasuryTransactionType
    {
        Income = 1,   // إيراد
        Expense = 2   // مصروف
    }
}
