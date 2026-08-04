# Level 4 — Middle: Test javoblari

---

**1.** To'g'ri javob: **b) Tranzaksiya boshidan barcha amallar bekor qilinadi**
> `RollbackAsync()` chaqirilganda, barcha bajarilgan va saqlangan (`SaveChangesAsync`) o'zgarishlar bekor qilinadi va ma'lumotlar bazasi tranzaksiya boshlanishidan oldingi holatiga qaytadi.

**2.** Ochiq javob:
> **Xavfli.** `FromSqlRaw` ga string interpolatsiya (`$""`) bilan o'zgaruvchi uzatilgan. Bu to'g'ridan-to'g'ri SQL kodiga aylanadi va **SQL Injection** xavfini tug'diradi.
> **Tuzatish:** `FromSqlInterpolated` ishlatish kerak yoki parametr uzatish kerak:
> ```csharp
> var products = await context.Products
>     .FromSqlInterpolated($"SELECT * FROM Products WHERE Name = {name}")
>     .ToListAsync();
> ```

**3.** To'g'ri javob: **b) Faqat o'qish (read-only) so'rovlarida**
> `AsNoTracking()` ma'lumotlarni o'zgartirish niyati bo'lmagan, faqat ko'rish/o'qish uchun olinadigan so'rovlarda ishlatiladi. Bu Change Tracker xotirasini tejaydi va tezlikni oshiradi.

**4.** To'g'ri javob: **c) 3 ta**
> 1-so'rov: `FindAsync(1)` (Product ni olish)  
> 2-so'rov: `Reference(p => p.Category).LoadAsync()` (Category ni yuklash)  
> 3-so'rov: `Collection(p => p.Reviews).LoadAsync()` (Reviews ni yuklash)

**5.** To'g'ri javob: **b) Oraliq jadvalda qo'shimcha ma'lumot (masalan, sana, baho) kerak bo'lganda**
> Agar Many-to-Many munosabatida oraliq jadvalda faqat 2 ta FK dan tashqari qo'shimcha ustunlar (masalan `EnrolledAt`, `Grade`, `CreatedDate`) saqlanishi kerak bo'lsa, alohida entity (oraliq class) yaratiladi.

**6.** Ochiq javob:
> `FromSqlInterpolated` — string interpolatsiya sintaksisini avtomatik ravishda xavfsiz `SqlParameter` larga aylantiradi (SQL Injection dan himoya qiladi).
> `FromSqlRaw` — raw SQL string qabul qiladi. Parametrlarni `{0}`, `{1}` shaklida qo'lda uzatish kerak. Agarda string birlashtirilsa, SQL Injection xavfi mavjud.

**7.** Ochiq javob — optimallashtirilgan variant:
> **Muammo:** N+1 so'rov muammosi va ortiqcha entity yuklash. Loop ichida har bir kategoriya uchun alohida so'rov ketayotgan edi.
>
> **Tuzatilgan kod:**
> ```csharp
> var categoryCounts = await context.Categories
>     .Select(c => new
>     {
>         Category = c,
>         ProductCount = c.Products.Count()
>     })
>     .AsNoTracking()
>     .ToListAsync();
> ```
> Bu bitta SQL so'rov (`LEFT JOIN` va `GROUP BY`) bilan barobar barcha ma'lumotni samarali olib beradi.
