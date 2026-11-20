# استقرار

یک image، دو سرویس بیرونی: SQL Server و (اختیاری) درگاه پیامک.

## image

```bash
docker build -t nobat .
docker run --rm -p 8080:8080 \
  -e ConnectionStrings__Default="Server=…;Database=Nobat;User Id=…;Password=…;TrustServerCertificate=True" \
  -e Auth__AllowPublicSignup=false \
  nobat
```

`deploy.yml` همین image را روی tag (`v*`) یا با اجرای دستی به `ghcr.io/sepehrsdq/nobat` می‌فرستد.
تگ‌ها: `sha-…` برای هر build، `latest` و نسخهٔ semver روی tag. اگر راز `DEPLOY_WEBHOOK_URL` ست شده باشد،
در پایان یک `POST` به آن می‌زند تا میزبان image تازه را pull کند؛ بدون آن، image منتشر می‌شود ولی roll-out دستی است.

## تنظیمات اجرا

| کلید | پیش‌فرض | توضیح |
|---|---|---|
| `ConnectionStrings__Default` | — | اجباری در Production؛ نبودش خطا می‌دهد |
| `ASPNETCORE_ENVIRONMENT` | `Production` | در image ست شده |
| `Auth__AllowPublicSignup` | `false` در Production | دموی بسته؛ برای مشتری واقعی `true` یا حساب دستی |
| `Sms__ExposeCodeInResponse` | `false` | فقط دموی بدون پیامک؛ کد را روی صفحه نشان می‌دهد |
| `Proxy__TrustForwardedHeaders` | `false` | پشت پروکسی میزبان `true` شود تا rate limit آی‌پی واقعی را ببیند |

## دیتابیس

مایگریشن **خودکار اجرا نمی‌شود**؛ عمدی است. یک بار، از ماشینی که به دیتابیس دسترسی دارد:

```bash
dotnet tool restore
dotnet tool run dotnet-ef database update -p src/Nobat.Infrastructure -s src/Nobat.Api
```

یا از image مخصوص build:

```bash
docker build -t nobat-migrate --target build .
docker run --rm nobat-migrate \
  dotnet tool run dotnet-ef database update -p src/Nobat.Infrastructure -s src/Nobat.Api
```

## اولین حساب دمو

1. با `Auth__AllowPublicSignup=true` بالا بیاورید و در `/login` تب «ثبت مطب» را بزنید.
2. مطب، پرسنل، خدمت و ساعت کاری را بسازید.
3. مقدار را به `false` برگردانید و سرویس را restart کنید تا دمو فقط با حساب آزمایشی باز شود.

## بررسی سلامت

```bash
curl -fsS http://localhost:8080/health
# {"status":"ok","utc":"…"}
```

نکته‌ها: پورت داخل image `8080` است؛ پروکسی میزبان باید همان را هدف بگیرد و هدرهای
`X-Forwarded-For`/`X-Forwarded-Proto` را ست کند. `appsettings.Production.json` ثبت‌نام عمومی را
بسته و نمایش کد پیامک را خاموش نگه می‌دارد.
