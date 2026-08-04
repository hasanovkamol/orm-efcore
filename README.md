# ORM / EF Core — Bosqichma-bosqich O'quv Kursi va Amaliy Loyiha

## 📚 Kurs haqida

Ushbu kurs **ORM, ADO.NET, Dapper va Entity Framework Core** bo'yicha **Junior dan Senior/Architect darajasigacha** bo'lgan to'liq o'quv dasturi hamda .NET 8 amaliy loyihasini o'z ichiga oladi.

**Texnologiyalar:** .NET 8 / EF Core 8 / MSSQL Server / Angular 18 Standalone / Docker Compose / BenchmarkDotNet

---

## 🎯 Kurs va Loyiha tuzilmasi

```
orm-efcore/
├── README.md
├── docker-compose.yml                       # 🐳 Single Docker Compose configuration
├── EfCoreMastery.slnx                       # .NET Solution fayli
├── docs/                                    # 📚 Kurs darsliklari va test javoblari
│   ├── Level-01-Kirish.md
│   ├── Level-02-Amaliyot.md
│   ├── Level-03-Junior-Plus.md
│   ├── Level-04-Middle.md
│   ├── Level-05-Performance.md
│   ├── Level-06-Advanced-Querying.md
│   ├── Level-07-Architecture.md
│   ├── Level-08-Scale.md
│   ├── Level-09-Architect.md
│   └── answers/
├── src/                                     # 💻 Amaliy loyiha kodi
│   ├── EfCoreMastery.Domain/
│   ├── EfCoreMastery.Application/
│   ├── EfCoreMastery.Infrastructure/
│   ├── EfCoreMastery.Api/                   # ASP.NET Core Web API (Dockerfile)
│   └── EfCoreMasteryClient/                # Angular 18 Standalone Dashboard (Dockerfile + Nginx)
└── tests/                                   # ⚡ BenchmarkDotNet unumdorlik testlari
    └── EfCoreMastery.Benchmarks/
```

---

## 🐳 Docker Compose Orqali Barchasini Ishga Tushirish

Bitta buyruq bilan SQL Server 2022, .NET 8 Web API va Angular Frontend containerlarini parallel ishga tushirish:

```bash
docker-compose up --build -d
```

- **Angular Dashboard UI:** `http://localhost:4200`
- **.NET Web API / Swagger:** `http://localhost:5000/swagger`
- **MSSQL Server:** `localhost:1433` (User: `sa`, Pass: `YourSecurePass123!`)

---

## 📖 Darsliklar Mundarijasi

| Daraja | Nomi | Darslik Fayli | Test Javoblari |
|--------|------|---------------|----------------|
| **Level 1** | Kirish | [Level-01-Kirish.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-01-Kirish.md) | [Level-01-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-01-Answers.md) |
| **Level 2** | Amaliyot | [Level-02-Amaliyot.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-02-Amaliyot.md) | [Level-02-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-02-Answers.md) |
| **Level 3** | Junior+ | [Level-03-Junior-Plus.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-03-Junior-Plus.md) | [Level-03-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-03-Answers.md) |
| **Level 4** | Middle | [Level-04-Middle.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-04-Middle.md) | [Level-04-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-04-Answers.md) |
| **Level 5** | Performance & Indexing | [Level-05-Performance.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-05-Performance.md) | [Level-05-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-05-Answers.md) |
| **Level 6** | Advanced Querying | [Level-06-Advanced-Querying.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-06-Advanced-Querying.md) | [Level-06-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-06-Answers.md) |
| **Level 7** | Arxitektura patternlari | [Level-07-Architecture.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-07-Architecture.md) | [Level-07-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-07-Answers.md) |
| **Level 8** | Scale & Performance | [Level-08-Scale.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-08-Scale.md) | [Level-08-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-08-Answers.md) |
| **Level 9** | Enterprise darajasi | [Level-09-Architect.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/Level-09-Architect.md) | [Level-09-Answers.md](file:///home/user02/Projects/AI%20Projects/orm-efcore/docs/answers/Level-09-Answers.md) |
