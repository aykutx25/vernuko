# Test Planı

## Test katmanları
| Katman | Nerede | Ne doğrular |
|---|---|---|
| Domain birim testleri | `tests/Vernuko.Tests/Domain` | Telefon, Türkçe metin, para, T.C. kimlik, parola, roller, randevu durumları, çakışma, müsaitlik, DST, paket defteri, cari hesap, yedek kripto/saklama/adlandırma, lisans imza/değerlendirme, mesaj şablonu, wa.me |
| Entegrasyon testleri | `tests/Vernuko.Tests/Integration` | Tam uygulama yığını + **gerçek SQLite dosyası** (geçici klasör): randevu akışları, eşzamanlılık, tetikleyiciler, kalıcılık, yetki, denetim, lisans, yedek/geri yükleme, migration, performans, başarı kriterleri |
| View model testleri | `tests/Vernuko.Tests/Presentation` | Kurulum sihirbazı, hızlı randevu paneli, hafta/ay görünümü, ödeme diyaloğu, demo verisi; Faz 2 ekranları (tekrarlayan seri + seri iptali, stok girişi/satışı, bekleme listesi önerisi, onam + şifreli fotoğraf önizleme, paket satışında satan personel primi), güncelleme öncesi yedek + XAML statik kontrolleri (kaynak anahtarları, bağlama yolları, VM↔görünüm eşlemesi) |
| Arayüz smoke testi | `Vernuko.exe --smoke-test <klasör>` (yalnızca Windows) | Tüm ekranlar 1366×768 ve 1920×1080'de açılır; her sayfanın **her sekmesi** ayrıca çizilir; PNG kaydedilir, WPF bağlama hatası varsa çıkış kodu 2 |
| Elle kabul | Windows 10/11 | REQUIREMENTS.md › I başarı kriterleri |

Çalıştırma:
```
dotnet test tests/Vernuko.Tests/Vernuko.Tests.csproj
```

## Talimattaki 30 senaryonun karşılığı
| # | Senaryo | Test | Durum |
|---|---|---|---|
| 1 | Aynı saate çakışan iki randevu | `Two_appointments_for_the_same_staff_and_time_cannot_both_be_saved`, `Back_to_back_bookings_do_not_conflict_but_overlaps_do` | ✅ |
| 2 | Tekrarlayan randevu düzenleme | `Weekly_series_reports_conflicts_skips_them_and_moves_or_cancels_the_rest`, `Recurrence_keeps_local_time_and_month_end`, `Editor_creates_a_weekly_series_and_row_menu_cancels_the_rest` | ✅ |
| 3 | Personelin izinli olduğu güne randevu | `Staff_on_leave_cannot_be_booked`, `Staff_leave_blocks_booking` | ✅ |
| 4 | Cihaz çakışması | `Room_and_device_conflicts_are_blocked_including_cleanup_buffer` | ✅ |
| 5 | Oda çakışması | aynı + `Cleanup_buffer_blocks_the_room` | ✅ |
| 6 | Lazer paketinin doğru azalması | `Laser_package_counts_down_only_on_completion_and_cancellation_keeps_the_session` | ✅ |
| 7 | İptalde seans hakkının korunması | aynı | ✅ |
| 8 | Ödemenin tekrar oluşturulmaması | `Payment_double_submit_creates_one_record`, `Payment_dialog_is_idempotent_on_double_click` | ✅ |
| 9 | Kapanıp açılınca veri korunması | `Data_survives_application_restart` | ✅ |
| 10 | İnternet kesilmesi | Faz 1 hiçbir ağ çağrısı yapmaz; tüm testler ağsız çalışır | ✅ (tasarım) |
| 11 | Google Drive bağlantısının kopması | — | ⬜ Faz 4 |
| 12 | Yedek dosyasının bozulması | `Corrupted_backup_is_rejected_and_live_data_untouched`, `Flipped_byte_truncation_and_appended_data_are_detected`, `Header_tampering_is_detected` | ✅ |
| 13 | Şifreli yedekten geri yükleme | `Encrypted_backup_restores_previous_state`, `Restore_on_a_new_computer_needs_the_recovery_key` | ✅ |
| 14 | Yanlış kurtarma parolası | `Restore_on_a_new_computer_needs_the_recovery_key`, `Wrong_key_is_detected` | ✅ |
| 15 | SMS sağlayıcısının cevap vermemesi | — | ⬜ Faz 5 |
| 16 | WhatsApp API hata yanıtları | — | ⬜ Faz 5 |
| 17 | Mükerrer mesaj engelleme | — | ⬜ Faz 5 |
| 18 | Lisans sunucusuna erişilememesi | `Expired_license_is_read_only_but_data_stays_readable_and_exportable` (çevrimdışı tolerans bölümü) | ✅ istemci |
| 19 | Offline lisans doğrulama | `Signed_license_verifies_and_tampering_fails`, `Offline_use_works_until_grace_ends_then_read_only` | ✅ |
| 20 | Abonelik bitişi | `Subscription_end_is_read_only_never_data_loss`, `Expired_license_is_read_only_…` | ✅ |
| 21 | Donanım değişikliği | `Device_change_requires_reactivation_but_keeps_read_access`, `Hardware_change_keeps_data_and_switches_to_read_only_until_reactivation` | ✅ |
| 22 | Yetkisiz personelin finans kayıtlarına erişmesi | `Secretary_cannot_see_financial_reports_and_attempt_is_audited`, `Sensitive_national_id_is_masked_without_permission` | ✅ |
| 23 | Veritabanı güncellemesinin başarısız olması | `Failed_migration_rolls_back_to_snapshot_and_newer_database_is_refused` | ✅ |
| 24 | Uygulama güncellemesinin yarıda kalması | `Update_takes_an_encrypted_backup_before_restarting`, `Update_is_not_applied_when_the_user_declines` (güncelleme öncesi yedek); yarıda kesilme Velopack'in atomik klasör değişimine dayanır | 🟡 Windows'ta elle denenmeli |
| 25 | Büyük müşteri kayıtlarıyla performans | `Search_stays_fast_with_twenty_thousand_customers` (eşik 1,5 sn; gerçek cihaz ölçümü Faz 8) | ✅ eşik |
| 26 | Windows ekran ölçeklendirmesi | Smoke test 1366×768 ve 1920×1080; DPI farkındalığı manifestte | 🟡 Windows'ta |
| 27 | Türkçe karakterler | `Search_keys_fold_Turkish_characters`, `Names_are_title_cased_with_Turkish_rules`, `Turkish_search_finds_names_and_phone_fragments`, CSV BOM testi | ✅ |
| 28 | Zaman dilimi ve yaz saati | `Daylight_saving_day_has_23_hours_and_slots_stay_on_local_clock`, `Istanbul_has_no_DST_and_local_conversion_is_exact` | ✅ |
| 29 | Çoklu bilgisayarda eşzamanlı rezervasyon | `Concurrent_bookings_for_same_slot_only_one_wins` (iki bağımsız uygulama yığını, aynı dosya), `Database_trigger_blocks_overlaps_even_if_application_check_is_bypassed` | ✅ tek makine / ⬜ Model B Faz 7 |
| 30 | Müşterinin verilerini dışa aktarabilmesi | `Customer_export_and_anonymization` | ✅ |

Faz 2 ek testleri: en özel prim kuralı ve geri almada ters prim, satan ≠ uygulayan prim, hizmette malzeme düşümü/geri almada iade/ürün satışı, bekleme listesi eşleşmesi, onam sürümü, sekreterin fotoğrafı görememesi, şifreli dosyanın yeni bilgisayarda yedekten açılması, anonimleştirmede dosya silme, dosya şifresinde kurcalama tespiti, kurulum↔veri klasörü ayrımı.

Ek testler: denetim zinciri kurcalama, iyimser eşzamanlılık, hesap kilidi + oturum kilidi, iptali geri almada çakışma, yeniden planlamada kendi slotu, seans etiketi ("3. Seans"), kurtarma anahtarı doğrulama ve zamanlama, aynı saniyede yedek adı çakışması (bulunup düzeltilen gerçek hata), DB seviyesinde append-only.

## Sonuçlar (bu sürüm)
v0.2.0 — Linux (.NET 10.0.15 çalışma zamanı, Release, temiz derleme, 0 uyarı): **136 test — 135 geçti, 1 atlandı (yalnızca Windows), 0 başarısız**, ~7 sn; üç ardışık çalıştırmada aynı sonuç. Windows CI işi (`.github/workflows/ci.yml`) aynı testleri Windows'ta çalıştırır, smoke testi koşar ve deneme kurulum paketi üretir; bu oturumdan CI sonuçlarına erişilemediği için sonuç görülmedi.

## Test edilmeyen / sınırlar
- Arayüz Windows'ta elle çalıştırılmadı; smoke test ve CI ile doğrulanacak. (Wine 9 ve Wine 11 .NET 10 uygulamalarını başlatamadı.)
- Kurulum paketi Linux'tan üretildi ama Windows'ta kurulup kaldırılmadı ve gerçek bir güncelleme denenmedi (bkz. RELEASE.md).
- Performans gerçek ön büro bilgisayarlarında ölçülmedi.
- Gerçek DPAPI testi (`Dpapi_protects_and_unprotects_for_the_current_user`) yalnızca Windows'ta çalışır; Linux'ta atlanır. Diğer testler hesap anahtarını taklit eden bir koruyucu kullanır (başka bilgisayar senaryosu dahil).
