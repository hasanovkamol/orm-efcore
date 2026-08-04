# ORM / EF Core — Bosqichma-bosqich O'quv Kursi va Amaliy Loyiha

## 📚 Kurs haqida

Ushbu kurs **ORM, ADO.NET, Dapper va Entity Framework Core** bo'yicha **Junior dan Senior/Architect darajasigacha** bo'lgan to'liq o'quv dasturi hamda .NET 10 amaliy loyihasini o'z ichiga oladi.

**Texnologiyalar:** .NET 10 / EF Core 10 / MSSQL Server / Angular 18 Standalone / Docker Compose / GitHub Actions CI/CD / BenchmarkDotNet

---

## 🎯 Kurs va Loyiha tuzilmasi

```
orm-efcore/
├── README.md
├── docker-compose.yml                       # 🐳 Single Docker Compose configuration
├── .github/workflows/ci-cd.yml              # 🚀 GitHub Actions CI/CD Pipeline (.NET 10 & Angular)
├── EfCoreMastery.slnx                       # .NET 10 Solution fayli
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
├── src/                                     # 💻 Amaliy loyiha kodi (.NET 10)
│   ├── EfCoreMastery.Domain/                # Target: net10.0
│   ├── EfCoreMastery.Application/           # Target: net10.0
│   ├── EfCoreMastery.Infrastructure/        # Target: net10.0
│   ├── EfCoreMastery.Api/                   # Target: net10.0 Web API (Dockerfile)
│   └── EfCoreMasteryClient/                # Angular 18 Standalone Dashboard (Dockerfile + Nginx)
└── tests/                                   # ⚡ BenchmarkDotNet unumdorlik testlari (.NET 10)
    └── EfCoreMastery.Benchmarks/
```

---

## 🚀 CI/CD Pipeline (GitHub Actions)

Loyiha uchun [.github/workflows/ci-cd.yml](file:///home/user02/Projects/AI%20Projects/orm-efcore/.github/workflows/ci-cd.yml) fayli orqali quyidagi avtomatik CI/CD bosqichlari yo'lga qo'yildi:
1. **.NET 10 Build & Validation:** .NET 10 SDK orqali backend va benchmark loyihalarini build qilish.
2. **Angular 18 Client Build:** Node.js 24 va Angular CLI yordamida frontendni build qilish.
3. **Docker Compose Validation:** Docker image va konteynerlarini avtomatik yig'ish va tekshirish.

---

## 🐳 Docker Compose Orqali Ishga Tushirish

```bash
docker-compose up --build -d
```

- **Angular Dashboard UI:** `http://localhost:4205`
- **.NET 10 Web API / Swagger:** `http://localhost:5050/swagger`
- **MSSQL Server:** `localhost:1434` (User: `sa`, Pass: `YourSecurePass123!`)
