> **Araştırma notu.** Bu rapor 15 Kasım 2026'da Vernuko Teknik Servis için hazırlandı (rakipler, özellikler, 6502 sayılı Kanun ve Satış Sonrası Hizmetler Yönetmeliği, KVKK). Kaynaklar sonda numaralı; "not verified" yazanlar doğrulanamadı. Hukuki konular avukat görüşü yerine geçmez. Rapor İngilizce bırakıldı.

# Teknik Servis Mode Research Report (as of 2026-11-15)

## 1. Competitors

### Turkey

| Product | Type / offline | Headline features | Pricing (published) | Gaps / complaints |
|---|---|---|---|---|
| **Akınsoft Wolvox Servis Yönetimi** | Module of the installed Wolvox ERP; web/mobile access through WebConnect [1]. Offline running: not verified. | Service history and warranty by serial number, SMS/e-mail when a slip's status changes, field teams and work orders, maintenance contracts, parts stock, e-Fatura/e-Arşiv inside the ERP [1] | Dealer campaign (updated 14.11.2026): Paket 1 ₺35.070, Paket 2 ₺55.705, both + KDV. List prices ₺53.950 / ₺85.700 [1]. Updates and legal fixes need a paid "WOLVOX UP" package [2] | A heavy ERP for a small repair shop. No user reviews found. |
| **Bilsoft Bulut Teknik Servis** | "Hybrid" cloud (web, mobile, desktop installer). Offline not mentioned [3]. | Request and delivery forms, barcode labels, custom statuses with days left, automatic SMS/e-mail by status, optional web status lookup, image gallery, periodic maintenance, stock and invoicing through Bilsoft accounting [3] | ₺10.200/year shown, 14-day trial [3] | Şikayetvar posts about Bilsoft products (not specifically this one): 3 days with no access, freezing, a "VIP package" extra-fee demand [4] |
| **TamirTakip** (tamirtakip.com.tr) | Browser only [5] | Status lookup by record number, WhatsApp templates, barcoded labels, parts stock (Premium) [5] | ₺130 / ₺210 / ₺300 per month [5] | No IMEI, photos or e-invoice mentioned |
| **Online Servis Takip** | Cloud web + mobile [6] | IMEI, photos, **pattern lock drawn and kept on the receipt**, QR status lookup, SMS/WhatsApp, price approval online or by SMS, thermal printing, Excel export [6] | ₺110 per month, ₺1.100 per year [6] | Keeping the unlock pattern on the receipt is a privacy risk |
| **Servisinitakipet** | Web/PWA, hosted in Turkey [7] | IMEI/serial history, photos, signature, customer portal with SMS-code login, e-Arşiv through a BirFatura integration [7] | ₺1.000 per month or ₺10.000 per year, KDV included [7] | Offline not mentioned |
| **Logo Diva SERVICE** | Cloud, aimed at service networks [8] | Real-time service steps, photo and document upload [8] | Not published | Enterprise-oriented |
| **Servis Pozitif** (Beyes) | Mobile field-service app [9] | Requests, work orders, technician assignment [9] | Listed free on the App Store. Subscription not verified. | — |
| Legacy desktop tools (Fenesoft, Teknoasistan) | Windows [10] | Basic fault records and stock [10] | Trial or paid | Fenesoft's download listing dates from 2015. Teknoasistan was pulled from Tamindir for "zararlı içerik" [10] |

On the names you suggested:
- **TamirTakip** exists (above).
- **Teknik Servis Pro**: not found.
- **ServisPlus**: only a BSH (Siemens home appliances) warranty package of that name, plus an unrelated app. Not a competitor.
- **Mobil Servis**: not verified.
- **Servis Takip**: a generic phrase, not one product.

None of the Turkish cloud tools above mentions offline operation [3][5][6][7].

### International (for feature ideas)

- **RepairDesk** ($99 or $149 per month; $79 or $119 billed yearly): digital signatures, a repair-tracker widget and self check-in [11]. Pre/post-repair condition checklists can be set per category. Admins can require a passcode or pattern before a ticket is booked, and at least one photo before repair [12].
- **RepairShopr** ($69.99 / $139.99 / $149.99 per month): custom intake forms that include the shop's terms, a customer portal, SMS, estimates and inventory [13].
- **Orderry** ($39 to $199+ per month): estimates with online approval and e-signature, a "Job Funnel" with a time limit per status, two-way WhatsApp, payroll and commissions, serial tracking [14].

## 2. Feature checklist

| Feature | Phase |
|---|---|
| Intake slip: customer, device type/brand/model/serial/IMEI, complaint, accessories checklist, condition checklist, intake date | **MVP** |
| Device photos at intake and delivery (reuse the encrypted attachments) | **MVP** |
| Unlock code / pattern storage (see 3c) | **MVP** |
| Statuses: received, diagnosing, waiting for approval, waiting for parts, repairing, ready, delivered, returned unrepaired, with aging and due-date warnings | **MVP** |
| Estimate and approval log (amount, channel, time, who approved; SMS/WhatsApp text or signature) | **MVP** |
| Technician assignment and labor lines; parts taken from stock | **MVP** |
| Repair warranty set automatically per job (see 3a) | **MVP** |
| Device history by serial/IMEI | **MVP** |
| A5 slip and thermal label with barcode/QR (the QR holds only the ticket ID) | **MVP** |
| "Ready for pickup" message through the shop's own provider | **MVP** |
| Deposit (kapora) through the existing kasa | **MVP** |
| On-site visit (adreste servis): ticket plus existing appointment plus address | **MVP-lite** |
| Reports: open jobs by status, turnaround, per-technician counts and revenue | MVP basic; detailed later |
| Online status lookup | Later |
| Maintenance contracts / periodic klima service | Later |
| Commissions, IMEI lookup APIs, e-invoice connector | Later |

## 3. Regulatory environment

### a) Law 6502 and the Satış Sonrası Hizmetler Yönetmeliği (SSHY)

The latest consolidated SSHY version I found is dated 01.01.2021. I found no 2025–2026 amendments, but that is not verified [17].

**Who it covers**
- SSHY covers independent services. "Özel servis" means a service station not tied to any producer or importer, and "servis istasyonu" includes both authorized and özel services (m.2, m.4/h, m.4/j) [16].
- Since the 2022 amendment (Law 7392), an independent shop must show the words **"özel servis"** clearly in every channel it uses (6502 m.58/5) [15]. The software should print this on its documents.

**Intake document (SSHY m.11/1)**
- Names and signatures of the person handing over the device and the person receiving it.
- The customer's complaints and requests.
- Device type, brand and model.
- Date of delivery or fault notice.
- The "similar device within 10 iş günü" commitment is not required from özel servis (m.11/3) [17].
- No intake document is needed when the repair is done on site (m.11/5) [17].

**Service slip at return (servis fişi, m.11/2)**
- The shop's name and contact details.
- Delivery date.
- The fault and the work done.
- The fee, if outside warranty.
- Signature of the shop's official.
- The m.10 responsibilities.
- The customer's name, address, phone and signature [17].

**Copies and electronic delivery**
- The shop must give the customer a copy (m.10/3) [17].
- It may deliver documents electronically on a durable medium (kalıcı veri saklayıcı), but must give paper if the customer asks. It must get the customer's confirmation of receipt and bears the burden of proving it. No signature is needed in that case (m.11/6) [17].

**Repair warranty**
- If the same fault comes back within 1 year after an out-of-warranty repair, no fee may be charged (m.10/6).
- Parts the shop replaces or sells get at least a 6-month warranty (m.10/7) [17].
- The shop must display a spare-parts price list (m.12/2) [17].

**Maximum repair time**
- The 20 iş günü limit is generally 20 business days (45 for vehicles) [18]. SSHY m.10/4 words it for *authorized* stations [17].
- For a defective service by the shop itself, the customer can demand redoing or free repair within 30 iş günü (6502 m.15/4). Claims for defective service expire after 2 years (m.16) [15].
- I found no explicit maximum for a paid out-of-warranty repair at an özel servis (not verified). I recommend a configurable due date with a warning at 20 iş günü.

**Device cannot be repaired**
- A "cannot be repaired" report must be produced within the maximum repair time (Garanti Belgesi Yönetmeliği m.9/5) [25]. This applies mainly to warranty cases.
- For özel servis I found no specific rule (not verified). The "returned unrepaired" status should record the reason and any diagnostic fee the customer agreed to in advance.

**Record keeping**
- Records of every transaction under SSHY must be kept for **3 years** (m.16) [17].

**Unclaimed devices**
- I found no specific rule (not verified). Do not hard-code any disposal clause.

### b) Servis yeterlilik belgesi

This certificate is an obligation on producers and importers (6502 m.58/2; SSHY m.6, valid for 2 years) [15][16]. It does not apply to independent shops, so nothing is needed in the software.

### c) KVKK

**What the law requires**
- Data must be limited and proportionate to its purpose (m.4/2) [19].
- It must be erased when the purpose ends (m.7) [19].
- Customers must be informed (m.10) [19].
- Appropriate technical and organizational security measures are required (m.12) [19].

**Precedent that matters for status lookup**
- A technical-service firm was fined **150.000 TL**. Changing the last digits of its tracking numbers exposed other customers' data, and it did not comply with the Board's decision (published 27.05.2019) [20].
- So any lookup must use random tokens plus a second check such as the phone number.

**Unlock codes**
- I found no KVKK decision specifically about device PINs (not verified).
- Recommended practice:
  - Ask for the code only when needed.
  - Store it encrypted and shown masked.
  - Never print it or put it in SMS.
  - Log who viewed it.
  - Delete it automatically at delivery.
- KVKK's 2026 bulletin advises users to wipe a device before *transferring* it [21]. Adapt this into a "back up / remove accounts" note on the consent form. That is my inference; the bulletin does not cover repair.

**Photos**
- Photograph the device's exterior. Avoid capturing what is on the screen.

### d) Invoicing

- From 1.1.2026, taxpayers not on e-Fatura must issue e-Arşiv for every invoice regardless of amount [23].
- Basit usul and işletme hesabı taxpayers have until 31.12.2026; for them the full obligation starts 1.1.2027 (VUK Tebliğ 589, RG 31.12.2025) [22][23].
- The e-Fatura threshold is ₺3M gross sales [23].
- An offline program cannot issue e-Arşiv or e-Fatura by itself. These need GİB (the tax authority) or a licensed integrator, which means internet (general knowledge, not separately verified).
- What Vernuko can honestly do:
  - Produce the servis fişi and a proforma marked as not an invoice.
  - Export lines (labor separate from parts, with KDV) to accounting.
  - Later, an optional integrator connector that runs only when online.

## 4. Recommendation

**MVP, in priority order**
1. Intake fiş with the m.11 fields, templates per device type (phone, PC, beyaz eşya/klima), the "Özel Servis" label, and a copy given on paper or through the shop's own SMS/WhatsApp, with the receipt-confirmation log.
2. Status workflow with aging and 20 / 30 iş günü warnings.
3. Estimate and approval log; deposit in kasa.
4. Parts from stock, labor, technician assignment.
5. Automatic repair warranty (12 months same fault, 6 months parts); history by serial/IMEI.
6. Encrypted code storage and photos.
7. Delivery and "returned unrepaired" slips.
8. Labels with barcode/QR.
9. On-site visits using existing appointments.
10. Basic reports; 3-year retention; export to accounting.

**Do not promise**
- Issuing e-Fatura/e-Arşiv offline.
- A customer web portal while offline.
- Being "100% KVKK / legally compliant" (say "helps you comply").
- Stolen-device or IMEI blacklist checks.
- Backup or recovery of the customer's device data.
- SMS delivery (it depends on the customer's provider).
- Manufacturer warranty integrations.

**Differentiators that fit offline and local data**
1. **Works with no internet at all**: intake, printing, labels, approvals and kasa keep running. Competitors don't advertise offline mode [3][5][6][7], and one vendor's users report multi-day lockouts [4].
2. **Unlock-code storage**: encrypted, never printed, purged automatically, access logged. This contrasts with keeping the pattern on the receipt [6].
3. **Compliance built into the documents**: the m.11 fields, "özel servis" wording, electronic copy with proof of receipt, warranty timers and 3-year retention are on by default.
4. **Customer data never leaves the shop**: there is no hosted lookup to enumerate (the 2019 KVKK fine) [20]. Status goes out as messages from the shop's own account.
5. **LAN workflow**: front-desk PC plus workbench PC on one local database.

## Sources
1. https://akinsoftzeytinburnu.com/yazilim/wolvox-servis.html
2. https://www.akinsoft.com/as/genel/license_system/ (search snippet only; the site blocked direct fetch)
3. https://www.bilsoft.com/en/urunler/bulut-teknik-servis-programi
4. https://www.sikayetvar.com/bilsoft-yazilim/bilsoft-yazilim-program-acilmiyor-ve-3-gundur-hicbir-sekilde-sisteme-erisemiyorum ; https://www.sikayetvar.com/bilsoft-yazilim/surekli-hatalar-ve-vip-paket-sarti-ile-gizli-ek-ucret-talebi
5. https://www.tamirtakip.com.tr/landing
6. https://onlineservistakip.com/
7. https://servisinitakipet.com/
8. https://cdn.logo.com.tr/files/logocomtr/Uploads/Documents/logo-diva-service-tanitim-brosuru-tr.pdf (search summary)
9. https://apps.apple.com/us/app/id1494965284
10. https://www.tamindir.com/indir/fenesoft-teknik-servis/ ; https://www.tamindir.com/indir/teknoasistan-teknik-servis-takip/
11. https://www.repairdesk.co/pricing/
12. https://help.repairdesk.co/portal/en/kb/articles/how-to-add-device-pre-post-repair-condition-12-8-2025 ; https://help.repairdesk.co/knowledgebase/articles/1894876-how-to-make-it-compulsory-to-add-network-device-l
13. https://www.repairshopr.com/pricing
14. https://orderry.com/pricing/
15. https://www.mevzuat.gov.tr/mevzuatmetin/1.5.6502.pdf
16. https://www.resmigazete.gov.tr/eskiler/2014/06/20140613-3.htm
17. https://www.lexpera.com.tr/mevzuat/yonetmelikler/satis-sonrasi-hizmetler-yonetmeligi-2
18. https://www.alomaliye.com/2022/08/11/tuketicileri-bilgilendirme-sikca-sorulan-sorular/
19. https://www.kvkk.gov.tr/Icerik/6649/Personal-Data-Protection-Law
20. https://afyonluoglu.org/PublicWebFiles/Reports-TR/KVKK-Kararlar.pdf
21. https://www.alomaliye.com/2026/09/30/akilli-cihaz-kullanicilarina-10-temel-oneri/
22. https://www.alomaliye.com/2025/12/31/vergi-usul-kanunu-genel-tebligi-sira-no-589/
23. https://experteamsolutions.com/blog-post-e-fatura-2026
24. https://forum.donanimhaber.com/teknik-servis-programi--160138540
25. https://www.resmigazete.gov.tr/eskiler/2014/06/20140613-2.htm