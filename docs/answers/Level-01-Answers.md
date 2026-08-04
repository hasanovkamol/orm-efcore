# Level 1 — Kirish: Test javoblari

---

**1.** To'g'ri javob: **b) C# obyektlari va jadvallar o'rtasida mapping qilish**
> ORM ning asosiy vazifasi — dastur obyektlarini (classlar) ma'lumotlar bazasi jadvallariga moslashtirish (mapping). Bu SQL yozishni kamaytiradi va kodni soddalashtiradi.

**2.** To'g'ri javob: **b) `Products` jadvalini C# da ifodalaydi**
> `DbSet<Product>` — bu ma'lumotlar bazasidagi `Products` jadvalining C# dagi "ko'rinishi". U orqali LINQ so'rovlari yoziladi va CRUD amallar bajariladi.

**3.** Ochiq javob:
> **Dapper** — micro-ORM, SQL ni o'zingiz yozasiz, lekin natija avtomatik C# obyektiga map qilinadi. Change Tracking yo'q, tezroq.
> **EF Core** — full ORM, SQL yozish shart emas (LINQ ishlatiladi), Change Tracking bor, Migration bor. Katta loyihalar uchun qulayroq, lekin biroz sekinroq.

**4.** Ochiq javob:
> Bu kod `Products` jadvalidan narxi 500 dan yuqori bo'lgan barcha mahsulotlarni tanlab, ularni nomi bo'yicha alifbo tartibida saralaydi va `List<Product>` sifatida qaytaradi. EF Core bu LINQ ni quyidagi SQL ga tarjima qiladi:
> ```sql
> SELECT * FROM Products WHERE Price > 500 ORDER BY Name
> ```

**5.** To'g'ri javob: **b) `dotnet ef migrations add MigrationName`**
> Migration yaratish buyrug'i `dotnet ef migrations add` dan keyin migration nomi yoziladi.

**6.** To'g'ri javob: **c) O'zgarishlar ma'lumotlar bazasiga yozilmaydi**
> `SaveChangesAsync()` chaqirilmaguncha, barcha o'zgarishlar faqat DbContext ning xotirasida (Change Tracker da) saqlanadi. Ma'lumotlar bazasiga hech narsa yozilmaydi.

**7.** To'g'ri javob: **b) `SqlParameter`**
> SQL Injection dan himoya qilish uchun `SqlParameter` ishlatiladi. Bu parametr qiymatlari to'g'ri escape qilinishini ta'minlaydi. String concatenation (`"SELECT * FROM Users WHERE Name = '" + name + "'"`) — juda xavfli!
