# Level 7 — Senior (Arxitektura patternlari): Test javoblari

---

**1.** To'g'ri javob: **b) Bir nechta repository o'zgarishlarini bitta tranzaksiyada saqlash**
> Unit of Work tranzaksiyaviy konsistentlikni ta'minlaydi. U turli repository-larda bajarilgan o'zgarishlarni bitta `DbContext.SaveChangesAsync()` orqali atomik tarzda ma'lumotlar bazasiga yozadi.

**2.** To'g'ri javob: **c) Infrastructure**
> Clean Architecture qoidalariga ko'ra, ma me'moriy bog'liqliklar va ORM frameworklar (EF Core) tashqi Infrastructure qatlamida joylashishi kerak. Domain va Application qatlamlari ORM dan mutlaqo ozod bo'lishi lozim.

**3.** Ochiq javob:
> Encapsulation (inkapsulyatsiya) tamoyillarini saqlash va entity holatini asossiz tashqi o'zgarishlardan himoya qilish uchun. O'zgarishlar faqat entity ichida joylashgan aniq nomlangan maqsadi bor metodlar (masalan, `ConfirmOrder()`, `ChangePrice()`) orqali amalga oshirilishi shart.

**4.** Ochiq javob:
> 1. **Performance:** Read operatsiyalari uchun NoTracking va Read-Replica DB lardan unumli foydalanish.
> 2. **Scalability:** O'qish va yozish yuklamalarini alohida-alohida resurslarga bo'lish va mustaqil masshtablashtirish (scale) imkoniyati.
> 3. **Simplicity:** Murakkab domain modellarni o'qish uchun DTO lardan va yozish uchun komandalardan alohida foydalanish.

**5.** To'g'ri javob: **b) CQRS**
> O'qish (`ReadDbContext`) va yozish (`WriteDbContext`) mas'uliyatini ikkita alohida kontekst va modellarga ajratish CQRS (Command Query Responsibility Segregation) patterniga mos keladi.

**6.** To'g'ri javob: **b) SaveChanges dan oldin/keyin qo'shimcha logika bajarish (audit, validation)**
> Interceptorlar EF Core amallari (masalan, `SaveChangesAsync`) bajarilishi jarayoniga "suqilib kirib" (intercept), audit log yozish, ma'lumotlarni tekshirish yoki avtomatik qiymat berish ishlarini bajaradi.

**7.** Ochiq javob:
> EF Core ning `DbContext` obyekti o'zi aslida tayyor Repository va Unit of Work patternlarining realizatsiyasidir (`DbSet<T>` — Repository, `DbContext` — Unit of Work). Shu sababli uning ustiga yana qo'shimcha Repository qatlami qurish **Abstraktsiya ustiga abstraktsiya (Over-engineering)** va EF Core LINQ imkoniyatlarini cheklab qo'yish (Leaky Abstraction) deb baholanadi.
