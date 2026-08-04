# Level 8 — Senior+ (Scale & performance): Test javoblari

---

**1.** To'g'ri javob: **c) `BulkInsertAsync` (SqlBulkCopy)**
> `EFCore.BulkExtensions` kutubxonasidagi `BulkInsertAsync` SQL Server ning past darajadagi tezkor `SqlBulkCopy` protokolini ishlatadi va 100,000 yozuvni soniyalar ichida (Change Tracker siz) ma'lumotlar bazasiga yozadi.

**2.** Ochiq javob:
> Global Query Filter lar ishlatiladi. `DbContext` da `OnModelCreating` metodi ichida barcha `IMultiTenant` subyektlariga `HasQueryFilter(e => e.TenantId == _currentTenantId)` sharti avtomatik o'rnatiladi.

**3.** To'g'ri javob: **b) DbContext thread-safe emas — race condition**
> EF Core `DbContext` birdan ortiq thread/task tomonidan bir vaqtda ishlatilishini qo'llab-quvvatlamaydi. Concurrent amallar `InvalidOperationException` yoki ma'lumotlar buzilishiga (race condition) olib keladi.

**4.** To'g'ri javob: **b) LINQ → Expression Tree → SQL tarjimani cache qiladi**
> `CompiledQuery` har bir so'rov bajarilganda LINQ expression larini SQL matniga o'girish (compilation/parsing) xarajatini teflaydi va tayyor SQL daraxtini xotiradan ishlatadi.

**5.** To'g'ri javob: **b) Cross-shard query va JOIN**
> Sharding da ma ma'lumotlar har xil fizik bazalarga bo'lingan bo'ladi. Bir nechta shardlar bo'ylab ma'lumotlarni umumlashtirish, JOIN qilish yoki tranzaksiya o'tkazish juda murakkab va sekin jarayondir.

**6.** Ochiq javob:
> Parallel ishlov berishda bir vaqtning o'zida ochiladigan ma'lumotlar bazasi ulanishlari (connections) yoki ochilgan `DbContext` lar sonini (Concurrency degree) cheklab turish va resurslar tugab qolishining oldini olish uchun.

**7.** To'g me'moriy javob: **c) Row-level (TenantId)**
> Row-level modelida yangi tenant qo'shilganda yangi ma'lumotlar bazasi yoki yangi sxema yaratish, migration o'tkazish talab etilmaydi. Shunchaki yangi `TenantId` bilan yozuvlar kiritilaveradi.
