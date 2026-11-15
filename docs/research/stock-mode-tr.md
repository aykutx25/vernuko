> **Araştırma notu.** Bu rapor 15 Kasım 2026'da Vernuko Stok için hazırlandı (rakipler, özellikler, mevzuat). Kaynaklar sonda numaralı; **(s)** ile işaretli iddialar sayfanın kendisinden değil arama motoru özetinden geliyor. Fiyatlar ve mevzuat değişir; hukuki ve vergisel konular mali müşavir veya avukat görüşü yerine geçmez. Rapor İngilizce bırakıldı.

# Vernuko "Stok" mode: competitors, features, regulation and MVP (as of 15 Nov 2026)

Some pages blocked direct fetching: Şikayetvar, Akınsoft and a few vendor PDFs. Claims marked **(s)** come from search-engine summaries, not from pages I opened. None of this is legal advice, so a mali müşavir should confirm section 3 before it goes into product copy.

## 1. Competitors

| Product | Deployment / offline | Headline features | Pricing | Complaints / gaps |
|---|---|---|---|---|
| **Akınsoft Wolvox** | Desktop / on-prem. Offline use is not stated anywhere I could check. | ERP family; serial/lot tracking, FIFO/LIFO stock valuation, bulk price updates [13](s); its fast-sales (Hızlı Satış) catalogue lists compatible cash-register models [13](s) | Perpetual licence plus an annual "WOL UP" update package. The first year is included. Core functions keep working without it, but legal updates need it [12](s). No prices published. | Refund and after-sales complaints, including one about a 26,050 TL purchase made in Nov 2025 [14](s) |
| **Logo GO 3 / GO Wings** | Desktop. GO Wings is a hybrid desktop + web product, for up to 16 users [10](s). | SME ERP with stock and cost tracking [10] | Logo publishes an advisory price list (Jan 2026), but I could not read the figures | One complaint is titled "licence expired → system locked" [11](s) |
| **Logo İşbaşı** | Cloud, web + mobile only. No desktop or offline mode [8]. | Stock, current accounts, cheques, e-Fatura/e-Arşiv | Each package is 463 TL/month or 5,550 TL/year; the combined package is 738 TL/month or 8,850 TL/year (+KDV) [8] | Support hard to reach, cancellation and charge disputes, an API-key delay [9](s) |
| **Mikro Run / Jump / Fly** | Run is desktop-only on SQL 2016 Express: one company, 1–2 users. Jump targets 5–50 staff and Fly 50+. Cloud is only available through Jump Bulut / İyi Bulut [1][2]. | Run includes Hızlı Satış (fast sales) and claims e-documents "free" (kontör terms not stated). Colour/size, serial and lot tracking need Jump or higher [1]. | Not published | Paid version upgrades (v17), long support waits, e-defter blocked by an unpaid update fee [3](s) |
| **Paraşüt** | Cloud; needs internet [4] | Pre-accounting + e-Fatura, stock; barcode scanning on mobile only [4] | 752 TL/month on the annual plan (9,024 TL/year +KDV); e-Fatura-only plan 150 TL/month [4] | Refund, cancellation and support complaints [5](s) |
| **Bizim Hesap** | Cloud, web + mobile [7] | Stock/warehouse, cheque/senet, current accounts, cash-register/POS integration [6][7] | Campaign prices 696 or 880 TL/month +KDV; yearly 8,352 or 10,560 TL +KDV [6] | No complaint page found |
| **Nebim V3** | Not stated | Its "Starter Edition" is described as store software for small retail and wholesale [15] | Not published | Positioned as ERP; fit for small shops not verified |
| **Barcode-sales programs** | Bilsoft Hızlı Satış is cloud-based, with scale and ÖKC-POS integration (Ingenico, Hugin, Verifone, Ödeal) [16]. BarkodSis is a Windows program; a download site says it works "online/offline" [17](s). BenimPOS is a mobile app [18]. | — | Bilsoft: 12,900 TL for the first year (2026 campaign), bundled with hardware, 24-month commitment, plus a 1,250 TL/month POS fee [16] | — |

**Pattern:** the cloud products (Paraşüt, Bizim Hesap, İşbaşı) do not work offline. The desktop products work offline, but their complaints cluster on update fees and licence locks. Nobody clearly offers a modern, offline, transparently priced desktop product for small shops.

## 2. Feature checklist

MVP = must have in v1. v1.x = soon after. Later = not now.

| Feature | Priority |
|---|---|
| Barcode sales screen: keyboard-first, several barcodes per product, mixed payment (nakit/kart/veresiye) | MVP, but see the ÖKC caveat in section 3 |
| Units and multipliers: buy by koli, sell by adet | MVP |
| KDV rate per product, from an editable rate table | MVP |
| Several price lists (perakende / toptan / per customer) and bulk % updates | MVP |
| Goods receipt (mal kabul) and purchase return | MVP |
| Customer and supplier current accounts: veresiye, vadeli satış, tahsilat/ödeme, ekstre as print or PDF | MVP |
| Cash register (kasa), basic bank accounts, end-of-day (gün sonu) report by payment type | MVP |
| Sales and purchase returns, posted as reversal documents | MVP |
| Stock count (sayım) with a scanner, posting the differences | MVP |
| Critical stock and order suggestions per supplier | MVP |
| Profit/margin, stock value and sales reports | MVP |
| Barcode and shelf label printing; in-store EAN-13 codes (prefixes 20–29 are reserved for in-store use [39]) | MVP |
| Excel import/export: products, price lists, opening balances | MVP |
| Shelf location (raf) field | MVP; multiple warehouses later |
| Scale barcodes (configurable 27/28/29 prefixes) [39] | v1.x |
| Cheque/senet portfolio (Mikro, İşbaşı and Bizim Hesap all have one [1][7][8]) | v1.x |
| e-Fatura / e-Arşiv / e-İrsaliye through an integrator | v1.x, high priority |
| ÖKC (GMP-3) integration | v1.x / v2 |
| OEM / equivalent-part (muadil) cross-reference for auto spare parts [40](s) | v1.x |
| Price lists in USD/EUR (my judgement, not researched) | v1.x |
| Serial/lot numbers | Later |
| Variants (beden/renk) — mostly apparel, not our target sectors | Later |

## 3. Regulation (2026)

### ÖKC / yeni nesil yazar kasa
- **Who must use an ÖKC:** first- and second-class merchants must use one for retail sales that do not require an invoice. Pure wholesalers do not. Simplified-procedure (basit usul) taxpayers are outside this law [21](s).
- **A PC program cannot issue a legal receipt.** VUK Genel Tebliği 483, Art. 8 requires that any sales software or hardware used for retail sales be connected to the ÖKC under the GMP-3 protocol, so that every sale becomes a fiscal receipt. Devices that print non-fiscal slips are included. Using such software without the connection carries a special irregularity fine (Art. 13/4) [19][20].
- **What GMP-3 is:** GİB's communication protocol between an ÖKC and external hardware or software. In practice each device brand integrates differently:
  - Ingenico requires a GMP3 service to be activated on the device using its serial number, and Mikro charges an annual GMP3 integration fee [24](s).
  - Token/Beko provides an "Integration Hub DLL" that uses JSON/TLV messages [25].
  - I found no public fee schedule and no GİB certification for software vendors (not verified).
  - My own estimate: several weeks per brand, plus buying real devices for testing.
- **New since May 2026:** VUK 593 (Official Gazette, 8 May 2026) lets approved yeni nesil ÖKCs issue e-Arşiv and e-Fatura themselves. The "bilgi fişi" practice is to be replaced by the ÖKC issuing the invoice itself. The guide on how this works was published only as a draft [22][23].
- **What a program without ÖKC integration can honestly do:** back office, current accounts and veresiye, and wholesale documents. It cannot replace the retail checkout. Whether a shop may type ÖKC sales into Vernuko afterwards needs a mali müşavir's opinion (not verified).

### Invoices and e-documents
- **Invoice limit for 2026 is 12,000 TL** (VUK 588) [30].
- **Paper invoices are ending.** From 1 Jan 2026, taxpayers who are not e-Fatura users must issue every invoice as e-Arşiv, whatever the amount, through the GİB portal or an integrator [26][28].
- **Exception:** basit usul and işletme hesabı taxpayers may still issue paper invoices up to 3,000 TL (tax included) until 31 Dec 2026. Full e-Arşiv applies to them from 1 Jan 2027 [27].
- **e-Fatura obligation:** gross sales of 3M TL or more (2022 and later) means e-Fatura from 1 July of the following year. The threshold is 500k TL for e-commerce, real estate and motor vehicles [29].
  - The 3M TL threshold is not indexed, while the 2026 revaluation rate was 25.49% [30]. My inference is that most wholesalers are already obliged.
- **e-İrsaliye** applies to e-Fatura users with 10M TL or more in gross sales [29].
- **How small programs integrate:**
  - Through private integrator (özel entegratör) APIs, for example Nilvera's OAuth2 API [31].
  - Each document costs a credit (kontör): Paraşüt sells 50 for 210 TL +KDV [4], Bizim Hesap 1,000 for 1,800 TL +KDV [6].
  - GİB's e-Arşiv portal is free but has no public API [31](s).
- **What works offline:**
  - Drafting and queuing documents works offline; sending them does not.
  - An invoice must be issued within 7 days of delivery (VUK 231/5) [32], so a short offline queue is workable.
  - I believe an e-İrsaliye must exist before the goods move, so it cannot wait in a queue (not verified).

### KDV rates
Confirmed: 1%, 10% and 20% since 10 Jul 2023 (Presidential Decree 7346) [33]. I found no 2026 change. Keep the rates editable.

### Record keeping (VUK/TTK)
- TTK 65/3: an entry may not be crossed out or changed so that its original content can no longer be seen [34](s).
- Retention: 5 years under VUK 253 and 10 years under TTK 82 [35].
- Vernuko's pre-accounting data is not the legal books, but the same rules should apply:
  - documents are append-only;
  - corrections are made by a reversal or cancellation document with a reason;
  - there is an audit log;
  - records are kept for at least 10 years;
  - there is an export for the accountant or a tax inspection.

### KVKK
- **The shop is the data controller.** It must inform customers how their data is used and keep that data secure [41](s).
- **Marketing messages:** sending SMS marketing requires registration in İYS (the commercial message system). Messages to merchants (tacir/esnaf) need no prior consent but still need İYS [38].
- **VERBİS registration:** shops with fewer than 50 employees and a balance sheet under 100M TL are exempt. Where the main activity is processing special-category data, as in Vernuko's clinic mode, the exemption is narrower: fewer than 10 employees and under 10M TL (Board decision 2025/1572) [36].
- **Cloud backups abroad:** these count as a transfer abroad under the amended KVKK Art. 9. Explicit consent is no longer a sustainable legal basis; standard contracts with notification are needed [37]. Keeping data local avoids the issue entirely.

## 4. Recommendation

**MVP:** all MVP rows in section 2, plus:
- LAN multi-PC running on one host database;
- user roles and append-only ledger;
- encrypted backups to local disk, USB or NAS;
- Excel import wizards.

**v1.x, in this order:**
1. e-Arşiv / e-Fatura through one integrator, with an offline queue.
2. Cheque/senet portfolio.
3. Scale barcodes, OEM/equivalent parts, USD/EUR price lists.
4. GMP-3 integration with one or two ÖKC brands.

**Do NOT promise:**
- "Issues legal receipts" (yasal fiş), or that the retail counter is compliant without ÖKC integration.
- e-Fatura, e-Arşiv or e-İrsaliye without an integrator and an internet connection.
- Paper invoices printed from Vernuko. Basit usul / işletme hesabı taxpayers may still issue paper invoices up to 3,000 TL until the end of 2026, but those must be printed by an approved printer, and whether Vernuko can print onto such forms is not verified.
- "GİB-approved software", keeping the legal books or filing tax returns, or "KVKK-compliant" on its own.

**Differentiators that fit Vernuko's offline, local-data stance:**
1. The counter and stock room keep working during an internet outage; e-documents send later, within the legal time limits.
2. Data stays on the shop's own PC: no question of transfers abroad, full export in one click, no lock-in. This answers the licence-lock and cancellation complaints above.
3. Transparent pricing: a perpetual core licence, legal updates included for a stated period, integrations priced separately. This answers the update-fee complaints.
4. A tamper-evident ledger (corrections by reversal only) and an export the accountant can use.
5. Sector features:
   - koli/adet units and supplier price-list import for hardware stores and wholesalers;
   - OEM/equivalent-part search for auto spare parts;
   - veresiye statements as PDF or WhatsApp;
   - reuse of customers shared with the salon mode.

## Sources
1. https://www.mikro.com.tr/mikro-run/
2. https://www.mikro.com.tr/
3. https://www.sikayetvar.com/mikro-yazilim
4. https://www.parasut.com/on-muhasebe-fiyatlari
5. https://www.sikayetvar.com/parasut
6. https://www.bizimhesap.com/fiyatlar
7. https://www.bizimhesap.com/
8. https://www.isbasi.com/fiyatlar
9. https://www.sikayetvar.com/logo-isbasi/api-anahtari-gecikmesi-ve-musteri-desteginde-yasanan-sorun ; https://www.sikayetvar.com/logo-isbasi/musteri-hizmetleri-sikayeti-ve-e-imza-sifre-yenileme-talebi
10. https://www.logo.com.tr/en/product/logo-go-wings ; https://cdn.logo.com.tr/files/logocomtr//Uploads/Documents/logo-go-wings-fiyat-listesi.pdf ; https://cdn.logo.com.tr/files/logocomtr/Uploads/Documents/logo-go-3-tan%C4%B1t%C4%B1m-bro%C5%9F%C3%BCr%C3%BC-tr.pdf
11. https://www.sikayetvar.com/logo-yazilim/lisans-suresi-bitince-sistem-kilitlendi-90-gunluk-tolerans-ihlali
12. https://www.akinsoft.com/as/genel/license_system/
13. https://akinsoft.com.tr/programlar/e-katalog/pdf/servis-seri_lot_v6.pdf ; https://www.akinsoft.com.tr/programlar/e-katalog/pdf/supermarket_v4.pdf
14. https://www.sikayetvar.com/akinsoft/akinsoft-bayilik-basvurusunda-cevapsizlik-ve-ucret-iadesi-talebi
15. https://www.nebim.com.tr/en
16. https://www.bilsoft.com/en/urunler/perakende-hizli-satis ; https://www.bilsoft.com/en/entegrasyonlar/okc-pos
17. https://www.tamindir.com/windows/barkodsis/
18. https://apps.apple.com/app/id1577947015
19. https://www.alomaliye.com/2017/09/30/vergi-usul-kanunu-genel-tebligi-483/amp/
20. https://www.alomaliye.com/2018/03/14/gib-bilgilendirme-okc/ ; https://www.muhasebenews.com/?p=66625
21. https://alomaliye.com/wp-content/uploads/2016/04/okc-rehberi.pdf ; https://www.dunya.com/gundem/yeni-nesil-odeme-kaydedici-cihaz-haberi-317196
22. https://www.alomaliye.com/2026/05/08/vergi-usul-kanunu-genel-tebligi-sira-no-593/
23. https://www.ekonomigazetesi.com/ekonomi/perakende-ticarette-bilgi-fisi-uygulamasi-kaldiriliyor-78920
24. https://uploads.mikro.com.tr/mikro_hizli_satis_1_40_0f7acdf625.pdf
25. https://developer.tokeninc.com/token-developer-portal-1/x-platform/token-x-connect-wire/genel-tanitim
26. https://www.alomaliye.com/2024/11/12/vergi-usul-kanunu-genel-tebligi-sira-no-573/
27. https://www.alomaliye.com/2025/12/31/vergi-usul-kanunu-genel-tebligi-sira-no-589/ ; https://www.legal500.com/intelligence/turkey/tax/e-ar%C5%9Fi%CC%87v-fatura-zorunlulu%C4%9Fu-2027-yilina-ertelenen-m%C3%BCkellefler
28. https://www.turmob.org.tr/ekutuphane/Read/9208c1d3-f311-468d-9dd7-0d50a202bd6c
29. https://sovos.com/tr/blog/kdv/sirketler-icin-2026-e-donusum-takvimi-ve-zorunluluklar/ ; https://turmob.org.tr/arsiv/mbs/resmigazete/-Duyuru-Tablo-Duyurusu.pdf ; https://sovos.com/tr/blog/kdv/e-irsaliye-uygulamasina-zorunlu-gecis-siniri-10-milyon-tl-oldu/
30. https://www.turmob.org.tr/ekutuphane/Read/90823c28-8ec0-4e98-bf82-2aff97f505ff ; https://www.alomaliye.com/2025/12/31/vergi-usul-kanunu-genel-tebligi-sira-no-588-vuk-588/amp/
31. https://developer.nilvera.com/en ; https://sovos.com/tr/blog/kdv/e-arsiv-fatura-zorunlulugu-ve-entegrasyon-yontemleri/ ; https://sovos.com/tr/blog/kdv/e-arsiv-fatura-uygulamasina-dahil-olmayan-mukellefler-icin-yeni-uygulama/
32. https://www.alomaliye.com/2026/01/08/vergi-usul-kanunu-kapsaminda-fatura-duzenlenmesinde-7-gun-suresi-karmasasi/
33. https://www.pwc.com.tr/tr/hizmetlerimiz/vergi/dolayli-vergi/bultenler/kdv-otv-bultenleri/2023/kdv-oranlarinda-degisiklik.html ; https://www.alomaliye.com/2023/07/07/katma-deger-vergisi-oranlari-karar-sayisi-7346/
34. https://www.lexpera.com.tr/literatur/kitaplar/b-hareket-978-625-7334-15-0
35. https://www.turmob.org.tr/BasinOdasi/BasinArsiv/Detay/9682a497-ca2e-487e-abb6-fe3440cdfa69/vuk-a-gore-defter-ve-belgelerin-saklanma-sureleri-ve-ozellikli-durumlar
36. https://cottgroup.com/tr/blog/kvkk-gdpr/item/verbise-kayit-zorunlulugu-istisnalarinda-degisiklik ; https://www.alomaliye.com/2026/01/13/kisisel-verileri-koruma-kurulunun-2025-1572-sayili-kararinin-uygulama-esaslari/
37. https://www.hukukihaber.net/kvkk-yurt-disina-aktarim-kapsaminda-yapilan-kanun-degisikligi ; https://www.kvkk.gov.tr/Icerik/7938/Standart-Sozlesmeler-ve-Baglayici-Sirket-Kurallarina-Iliskin-Dokumanlar-Hakkinda-Kamuoyu-Duyurusu
38. https://www.lexpera.com.tr/mevzuat/yonetmelikler/ticari-iletisim-ve-ticari-elektronik-iletiler-hakkinda-yonetmelik
39. https://softmatic.com/barcode-ean-13 ; https://elementarypos.com/en/product-weight-encoded-in-barcode/
40. https://ecosire.com/tr/blog/erp-for-automotive-parts-distribution
41. https://www.kvkk.gov.tr/Icerik/4192/Kanun-Kapsamindaki-Hak-ve-Yukumlulukler