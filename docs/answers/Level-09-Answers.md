# Level 9 — Architect (Enterprise darajasi): Test javoblari

---

**1.** To'g'ri javob: **b) Entity turi ni aniqlaydigan ustun**
> TPH (Table-Per-Hierarchy) da barcha iyerarxiya bitta jadvalda saqlanadi. Discriminator ustuni (masalan `PaymentType`) shu qatordagi ma'lumot C# ning qaysi aniq sub-klasiga (masalan `CreditCardPayment`) tegishli ekanini ko'rsatadi.

**2.** Ochiq javob:
> Temporal Table (SQL Server) ma'lumotlar ustidagi barcha o'zgarishlar va o'chirishlar tarixini avtomatik ravishda alohida history jadvalida saqlaydi. Bu ma'lumotlarning istalgan o'tgan vaqtdagi holatini tiklash (`TemporalAsOf`) va o'zgarishlar auditini yuritish imkonini beradi.

**3.** To'g'ri javob: **b) Distributed tizimda atomik operatsiyalarni compensating transaction orqali ta'minlash**
> Microservice lar va taqsimlangan bazalar o'rtasida 2-Phase Commit o'rniga har bir servisda amallarni bajarish hamda xatolik yuz berganda teskari kompensatsiya amallarini (`Rollback` analogi) bajarish uchun Saga pattern qo'llaniladi.

**4.** To'g'ri javob: **b) `Product` da `[Timestamp]` bo'lsa va boshqa process ham o'zgartirgan bo'lsa**
> Optimistic Concurrency da `[Timestamp]` (RowVersion) ustunidan foydalaniladi. Agar obyekt xotiraga yuklangandan so'ng, u `SaveChangesAsync` bo'lgunga qadar boshqa so'rov/ip tomonidan bazada o'zgartirilgan bo'lsa, Concurrency Exception yuz beradi.

**5.** Ochiq javob:
> Outbox Pattern ma'lumotlar bazasiga yozish va unga bog'liq Event/Message larni message brokerga (RabbitMQ/Kafka) yozish amallarining tranzaksiyaviy konsistentligini ta'minlaydi. Event darhol brokerga yuborilmay, avval shu DB tranzaksiyasi ichida `OutboxMessages` jadvaliga yoziladi, so'ngra background worker orqali ishonchli yuboriladi (At-least-once delivery).

**6.** Ochiq javob:
> `Remove()` metodidan foydalanilganda EF Core obyekt holatini `EntityState.Deleted` ga o'tkazadi va SQL `DELETE` generatsiya qiladi. Soft Delete da esa interceptor yoki DbContext orqali bu holat `EntityState.Modified` ga o'zgartirilib, `IsDeleted = true`, `DeletedAt = DateTime.UtcNow` atributlari o'rnatiladi.

**7.** Ochiq javob:
> 1. **Indexation & Cover Indexes:** Indekslarni optimallashtirish va so'rovga mos composite/covering indexlar yaratish.
> 2. **Materialized Views / CQRS Read Model:** Aggregatsiyalangan ma'lumotlarni oldindan hisoblab, read-model jadvali yoki SQL Materialized View da saqlash.
> 3. **Caching & AsNoTracking:** So'rovlarni NoTracking bilan bajarish va o'zgarmas ma'lumotlarni Distributed Cache (Redis) ga joylash.
