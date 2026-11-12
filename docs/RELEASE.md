# Kurulum Paketi, Sürüm ve Güncelleme

## Özet
| Konu | Karar |
|---|---|
| Araç | Velopack 1.2.161 (MIT, ücretsiz) — `Velopack` NuGet paketi + `vpk` yerel aracı (`dotnet-tools.json`) |
| Kurulum türü | Kullanıcı başına (`%LOCALAPPDATA%\VernukoApp`), **yönetici yetkisi istemez**, masaüstü + Başlat menüsü kısayolu |
| Paket kimliği | `VernukoApp` — sabittir, değiştirilmez (`src/Vernuko.Infrastructure/AppIdentity.cs`) |
| Veri klasörü | `%LOCALAPPDATA%\Vernuko\Data` — kurulum klasöründen **ayrı**. Velopack kaldırırken yalnızca `VernukoApp` klasörünü siler; müşteri verisi, yedekler (Belgeler\Vernuko Yedekler) ve dosyalar kalır. Test: `Installer_folder_never_contains_the_customer_data_folder` |
| Giriş noktası | `src/Vernuko.Desktop/Program.cs`: `VelopackApp.Build().Run()` WPF ve veritabanından önce çalışır (`vpk pack` bunu doğrular) |
| Güncelleme kaynağı | Derleme anında `-p:VernukoUpdateUrl=https://…` ile sabitlenir (assembly metadata). **Yalnızca HTTPS**; çalışma anında değiştirilemez. Boşsa uygulama "güncelleme kaynağı tanımlı değil" der ve ağa çıkmaz |
| Güncelleme akışı | Ayarlar › Güncellemeler: "Denetle" → "Yedek al ve güncelle". Önce şifreli **PreUpdate** yedeği alınır (yedek alınamazsa açık uyarı ve onay), sonra indirilir, uygulama kapanıp yeni sürümle açılır. Otomatik/sessiz indirme yok |
| Veritabanı | Yeni sürüm açılışta bekleyen migration'dan önce ayrıca güvenlik kopyası alır; hata olursa geri döner; daha yeni sürümün veritabanını eski sürüm açmaz |
| İmza | **Şu an imzasız — satıştan önce zorunlu.** İmzasız sürüm iki şekilde takılır: (1) SmartScreen "tanınmayan yayıncı" uyarısı; (2) **Windows 11 Akıllı Uygulama Denetimi (Smart App Control) açıksa hiç çalışmaz** — "blocked by your organization's Device Guard policy" (2026-11-12'de yeni bir Windows 11 bilgisayarda `Vernuko.exe` için görüldü). Akıllı Uygulama Denetimi bilinmeyen uygulamalarda geçerli bir imza arar; yeni Windows 11 kurulumlarında varsayılan olarak açık olabilir. Müşteriye SmartScreen'i, Defender'ı veya Akıllı Uygulama Denetimi'ni kapatması **asla** söylenmez. OV sertifika + bulut HSM (bkz. LICENSING.md) alınınca `vpk pack --signParams …` veya `--azureTrustedSignFile …` ile hem `Vernuko.exe`/DLL'ler hem `Setup.exe` imzalanır. Geliştirme sırasında geçici çözüm: `dotnet Vernuko.dll` (`baslat.cmd`, `demo.cmd`, `kontrol.cmd` bunu kullanır) |

## Sürüm yayınlama (sahip için adımlar)
1. `Directory.Build.props` içindeki `<Version>` değerini ve `CHANGELOG.md`'yi güncelleyin.
2. (İsteğe bağlı) GitHub deposunda **Settings › Variables › Actions** altında `VERNUKO_UPDATE_URL` değişkenini tanımlayın (gizli bilgi değildir). Örnekler:
   - Herkese açık bir "sürümler" deposu: `https://github.com/<hesap>/<vernuko-releases>` (özel depoda istemci token olmadan okuyamaz; token'ı uygulamaya gömmek **yasaktır**).
   - Statik barındırma (Cloudflare R2, S3, kendi sunucunuz): `https://guncelleme.ornek.com/vernuko` — klasöre `releases.win.json`, `*.nupkg` ve kurulum dosyası yüklenir.
3. `v0.2.0` gibi bir etiket gönderin. `.github/workflows/release.yml` derler, testleri ve smoke testi çalıştırır, kurulum paketini üretir ve **taslak (draft)** GitHub sürümü oluşturur. Taslağı inceleyip siz yayınlamadan hiçbir müşteriye gitmez.
4. Statik barındırma kullanıyorsanız taslaktaki dosyaları o klasöre yükleyin (veya `vpk upload s3` / `vpk upload local`).

Her `push`'ta çalışan CI de imzasız bir **deneme kurulum paketi** üretir (`vernuko-installer-ci` artefaktı); müşteriye dağıtılmaz.

## Yerelde paket üretme
En kolayı: depo kökündeki **`kurulum-olustur.cmd`** dosyasına çift tıklayın (veya PowerShell'de `.\kurulum-olustur.cmd`). Derler, paketler, `releases\VernukoApp-win-Setup.exe` dosyasını üretir ve kurulumu başlatır; hata olursa durup gösterir.

Elle, Windows'ta:
```bash
dotnet publish src/Vernuko.Desktop/Vernuko.Desktop.csproj -c Release -r win-x64 --self-contained -o publish
```
```bash
dotnet tool restore
```
```bash
dotnet vpk pack --packId VernukoApp --packVersion 0.2.0 --packDir publish --mainExe Vernuko.exe --packTitle Vernuko --packAuthors Vernuko --icon src/Vernuko.Desktop/Assets/vernuko.ico --runtime win-x64 --outputDir releases
```
Linux/macOS'tan çapraz paketleme için `vpk` komutunun **ilk** argümanı `[win]` olmalıdır: `dotnet vpk "[win]" pack …`.

Çıktı: `VernukoApp-win-Setup.exe` (kurulum), `VernukoApp-win-Portable.zip` (taşınabilir; otomatik güncelleme kapalı), `VernukoApp-<sürüm>-full.nupkg` ve `releases.win.json` (güncelleme beslemesi).

## Doğrulama durumu (dürüst)
| Kontrol | Durum |
|---|---|
| Uygulama win-x64 olarak yayımlanıyor (Linux'tan çapraz) | ✅ 145 MB, kendi içinde .NET 10 |
| `vpk pack` kurulum paketi üretiyor ve `VelopackApp.Run()` çağrısını doğruluyor | ✅ (Linux'tan `[win]` ile; Setup.exe ≈ 76 MB) |
| Güncelleme öncesi yedek, kullanıcı reddederse güncellememe | ✅ view model testleri (sahte güncelleme servisiyle) |
| Kurulum klasörü ↔ veri klasörü ayrımı | ✅ test |
| Temiz Windows 10/11'de kurulum, kaldırma, yeniden kurulum, gerçek bir güncelleme | ⬜ **denenmedi** — Windows makinesi yok. İlk sürümden önce elle yapılmalı |
| Yarıda kalan güncelleme (senaryo 24) | ⬜ Velopack paketi ayrı klasöre indirir ve uygulama kapanınca değiştirir; Windows'ta elle denenmeli |
| Authenticode imzası | ⬜ sertifika yok |
