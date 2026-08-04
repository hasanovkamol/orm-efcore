# Level 5 — Middle+ (Performance & Indexing): Test javoblari

---

**1.** To'g'ri javob: **b) Async/await bilan to'g'ri ishlashi uchun**
> `TransactionScope` asinxron kodda ishlatilganda, execution context (tranzaksiya konteksti) bir ipdan ikkinchisiga to'g'ri o'tishi uchun `TransactionScopeAsyncFlowOption.Enabled` bayrog'i shart. Aks holda tranzaksiya async chegaralarda yo'qolib qolishi mumkin.

**2.** Ochiq javob:
> `AppDbContext` **thread-safe emas** va u o'z ichida Change Tracker ni saqlaydi. Singleton qilinganda:
> 1. Barcha foydalanuvchilar va requestlar bitta DbContext ni ishlatadi — Concurrency crash / race condition yuz beradi.
> 2. Change Tracker cheksiz o'sib ketadi, bu ma'lumotlar chalkashishiga va Memory Leak ga olib keladi.

**3.** To'g'ri javob: **b) `ComplexType` null bo'la olmaydi va alohida jadvalda bo'lmaydi**
> `ComplexType` (EF Core 8) har doim asosiy entity jadvali ichida joylashadi (inline) va `null` bo'lishi mumkin emas. `OwnedType` esa alohida jadval sifatida ham sozlanishi mumkin hamda `null` qiymatni qabul qila oladi.

**4.** To'g'ri javob: **b) Ha, `FromSqlInterpolated` avtomatik parametrlaydi**
> `FromSqlInterpolated` ishlatilganda, string ichidagi `{input}` ifodasi SQL ga to'g'ridan-to'g me'moriy ravishda yozilmaydi, balki `SqlParameter` obyektiga aylantiriladi. Шу sababli u SQL Injection dan to'liq himoyalangan.

**5.** Ochiq javob:
> `ChangeTracker.Clear()` DbContext tomonidan kuzatilayotgan (tracked) barcha entity obyektlarini Change Tracker dan uzadi (detach qiladi). Katta hajmdagi batch amallarni bajarishda xotirani bo'shatish va unumdorlikni pasayib ketmasligi uchun ishlatiladi.

**6.** To'g'ri javob: **b) Parallel va background operatsiyalarda thread-safe DbContext yaratish**
> `IDbContextFactory<T>` har bir chaqiriqda yangi `DbContext` obyekti yaratib beradi. Bu background servicelar yoki parallel bajariladigan ishlov berish jarayonlarida thread concurrency muammolarini oldini oladi.

**7.** Ochiq javob:
> Real loyihada `SaveChangesAsync` ni override qilish orqali:
> 1. **Automatic Auditing:** Yangilangan va yaratilgan sanalarni (`CreatedAt`, `UpdatedAt`) hamda foydalanuvchi ID sini avtomatik to'ldirish.
> 2. **Soft Delete:** `Remove` amallarini ushlab qolib, ularni `IsDeleted = true` ga o'zgartirish.
> 3. **Domain Events:** Entity ichida toplangan hodisalarni (events) saqlashdan oldin yoki keyin avtomatik chqarish.
