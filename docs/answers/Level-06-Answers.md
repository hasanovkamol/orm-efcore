# Level 6 — Middle-Senior (Advanced Querying): Test javoblari

---

**1.** To'g'ri javob: **b) JOIN natijasida dublikat qatorlar geometrik o'sishi**
> 1 to'plamda ko'plab bog'liq jadvallar (1:N) birdaniga `Include` qilinganda, SQL generator ularni bitta yirik SQL JOIN ga aylantiradi. Natijada qaytadigan satrlar soni barcha bog'liqliklar ko'paytmasi shaklida ko'payib ketadi va tarmoq/xotira yuklamasini oshiradi.

**2.** Ochiq javob:
> Ushbu composite index `CategoryId` bo'yicha filterlash va `Price` bo'yicha o'sish/kamayish tartibida saralash bajariladigan so'rovlar uchun o'ta foydali. Masalan:
> ```csharp
> context.Products.Where(p => p.CategoryId == 5).OrderBy(p => p.Price);
> ```

**3.** To'g'ri javob: **b) Faqat Foreign Key nullable bo'lganda**
> Parents o'chirilganda child obyektning FK qiymati `null` ga o'zgartirilishi uchun shu Foreign Key ustuni ma'lumotlar bazasida `NULL` bo'lishiga ruxsat berilgan (`int?`) bo'lishi shart.

**4.** Ochiq javob:
> `.IgnoreQueryFilters()` metodi chaqiriladi:
> ```csharp
> var allItems = await context.Products.IgnoreQueryFilters().ToListAsync();
> ```

**5.** Ochiq javob:
> Bu yerda 4 ta to'plamli `Include` ishlatilgan. Bu bitta SQL so'roviga birlashtirilib **Cartesian Explosion** keltirib chiqaradi va juda sekin ishlaydi yoki xotirani to'ldirib yuboradi.
> **Yechim:** `.AsSplitQuery()` metodidan foydalanish.

**6.** Ochiq javob:
> Filtered Index ma'lum bir `WHERE` shartiga mos keladigan qatorlar uchun shakllantiriladi (masalan, `WHERE IsActive = 1`). U indeks hajmini kichik ushlab turadi, xotirani tejaydi va faqat aktiv ma'lumotlar ustida qidiruv bajariladigan so'rovlarni juda tezlashtiradi.

**7.** To'g'ri javob: **b) `OnModelCreating` da reflection bilan avtomatik qo'llash**
> Reflection yordamida `modelBuilder.Model.GetEntityTypes()` orqali barcha `ISoftDelete` interfeysini amalga oshirgan sinflar aylanib chiqiladi va dinamik ravishda Expression Tree yordamida query filter biriktiriladi.
