# Level 2 — Amaliyot: Test javoblari

---

**1.** To'g'ri javob: **b) `ExecuteDeleteAsync` obyektni oldindan yuklamasdan to'g'ridan-to'g'ri o'chiradi**
> `Remove` + `SaveChanges` — avval `FindAsync` bilan obyektni xotiraga yuklash, keyin o'chirish kerak. `ExecuteDeleteAsync` esa to'g'ridan-to'g'ri SQL `DELETE` generatsiya qiladi — tezroq va xotira tejamkor.

**2.** Ochiq javob:
> Bu kod `Products` jadvalidagi narxi 50 dan past bo'lgan barcha mahsulotlarning narxini 2 barobar oshiradi. SQL ekvivalenti:
> ```sql
> UPDATE Products SET Price = Price * 2 WHERE Price < 50
> ```
> `SaveChangesAsync()` chaqirish shart emas, chunki `ExecuteUpdateAsync` to'g'ridan-to'g'ri SQL bajaradi.

**3.** To'g'ri javob: **b) Barcha o'zgarishlar bitta transaction ichida saqlanadi**
> `SaveChangesAsync()` barcha kutilayotgan o'zgarishlarni (Add, Remove, Update) bitta database transaction ga o'rab yuboradi. Agar birontasi xato bersa — hammasi rollback qilinadi.

**4.** To'g'ri javob: **b) `dotnet ef dbcontext scaffold`**
> Database First yondashuvida mavjud ma'lumotlar bazasidan C# entity classlarni va DbContext ni avtomatik yaratish uchun `scaffold` buyrug'i ishlatiladi.

**5.** Ochiq javob:
> SQL ekvivalenti:
> ```sql
> SELECT TOP(5) p.[Name], p.[Price]
> FROM [Products] AS p
> WHERE p.[CategoryId] = 3
> ORDER BY p.[Price] DESC
> ```
> CategoryId = 3 bo'lgan mahsulotlardan faqat Name va Price ni olib, narx bo'yicha kamayish tartibida 5 tasini qaytaradi.

**6.** Ochiq javob:
> **Foreign Key** (`CategoryId`) — ma'lumotlar bazasidagi jismoniy bog'lanish (ustun). SQL JOIN lar uchun ishlatiladi.
> **Navigation Property** (`Category`, `Products`) — C# dagi mantiqiy bog'lanish (property). EF Core orqali bog'langan obyektlarga qulay murojaat qilish imkonini beradi. Navigation property "orqasida" foreign key turadi.

**7.** To'g'ri javob: **c) EF Core provider ga bog'liq (SQL Server da batch, SQLite da alohida)**
> SQL Server provayderida EF Core 7+ bir nechta INSERT larni bitta batch so'rovga birlashtiradi (`MERGE` yoki `INSERT ... VALUES (...), (...)` shaklida). SQLite kabi ba'zi provayderlar batch ni qo'llab-quvvatlamaydi va har biri uchun alohida INSERT yuboradi.
