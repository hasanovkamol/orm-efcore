# Level 3 — Junior+ / Middle boshlang'ich: Test javoblari

---

**1.** To'g'ri javob: **b) N+1 so'rov muammosi (Lazy Loading yoqilgan bo'lsa)**
> Agar Lazy Loading yoqilgan bo'lsa, `p.Category.Name` ga murojaat qilingan har bir iteratsiyada alohida SQL `SELECT` so'rov yuboriladi. 100 ta product = 1 (products) + 100 (har bir category) = 101 so'rov! Eager Loading (`Include(p => p.Category)`) bilan bu muammo hal qilinadi.
> Agar Lazy Loading yoqilmagan bo'lsa, `Category` `null` bo'ladi va `NullReferenceException` yuz beradi.

**2.** To'g'ri javob: **b) Assembly dagi barcha `IEntityTypeConfiguration<T>` implementatsiyalarini topib qo'llaydi**
> Bu metod reflection orqali ko'rsatilgan assembly dagi barcha `IEntityTypeConfiguration<T>` interfeysi implementatsiyalarini avtomatik topadi va ularning `Configure` metodlarini chaqiradi. Har bir entity uchun alohida konfiguratsiya class yozish imkonini beradi.

**3.** Ochiq javob:
> `OnDelete(DeleteBehavior.Restrict)` — parent entity (masalan, Category) o'chirilmoqchi bo'lganda, agar unga bog'langan child entitylar (masalan, Products) mavjud bo'lsa, o'chirishga **ruxsat bermaydi** va exception tashlaydi. Bu ma'lumotlarning tasodifan o'chirilishining oldini oladi.
> Boshqa variantlar:
> - `Cascade` — parent o'chirilsa, children ham o'chadi
> - `SetNull` — foreign key `null` ga o'zgaradi
> - `NoAction` — DB darajasida hech narsa qilmaydi

**4.** Ochiq javob:
> Bu so'rov mahsulotlarni `CategoryId` bo'yicha guruhlaydi, har bir guruhning sonini hisoblaydi, va faqat 5 tadan ko'p mahsuloti bor kategoriyalarni qaytaradi. SQL ekvivalenti:
> ```sql
> SELECT CategoryId, COUNT(*) AS Count
> FROM Products
> GROUP BY CategoryId
> HAVING COUNT(*) > 5
> ```

**5.** To'g'ri javob: **b) Ha, `IgnoreQueryFilters()` bilan**
> ```csharp
> // Query filter qo'yilgan
> modelBuilder.Entity<Product>().HasQueryFilter(p => !p.IsDeleted);
>
> // Vaqtincha o'chirish
> var allProducts = await context.Products
>     .IgnoreQueryFilters()
>     .ToListAsync(); // O'chirilganlar ham chiqadi
> ```

**6.** To'g'ri javob: **c) `dotnet ef database update` ishga tushirilganda**
> `HasData()` bilan qo'shilgan seed data migration fayliga yoziladi. Ma'lumotlar bazasiga faqat `dotnet ef database update` buyrug'i yoki `context.Database.MigrateAsync()` chaqirilganda kiritiladi.

**7.** Ochiq javob — optimallashtirilgan variant:
> **Muammo:** Har bir author uchun alohida `context.Books.Count()` so'rovi yuborilmoqda — bu N+1 muammo.
>
> **Tuzatilgan kod:**
> ```csharp
> var result = await context.Authors
>     .Select(a => new AuthorDto
>     {
>         Name = a.FullName,
>         BookCount = a.Books.Count
>     })
>     .ToListAsync();
> ```
> Bu bitta SQL so'rov (subquery yoki LEFT JOIN bilan) generatsiya qiladi va barcha ma'lumotni bir marta oladi. N+1 muammo yo'qoladi, tezlik 10-50x oshishi mumkin.
