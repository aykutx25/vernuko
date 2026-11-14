/* The home page's day board: switch business type, switch the internet off. No dependencies, no network. */
(function () {
	'use strict';

	function el(tag, cls, text) {
		var e = document.createElement(tag);
		if (cls) { e.className = cls; }
		if (text !== undefined) { e.textContent = text; }
		return e;
	}

	function satir(r, durumlar) {
		var li = el('li', 'vk-satir');
		li.dataset.durum = r[6];
		li.appendChild(el('span', 'vk-satir__saat', r[0]));
		var kisi = el('span', 'vk-satir__kisi');
		kisi.appendChild(el('b', '', r[1]));
		kisi.appendChild(el('span', '', r[2]));
		if (r[3]) { kisi.appendChild(el('span', 'vk-satir__not', r[3])); }
		li.appendChild(kisi);
		var kaynak = el('span', 'vk-satir__kaynak');
		kaynak.appendChild(el('span', '', r[4]));
		kaynak.appendChild(el('span', '', r[5]));
		li.appendChild(kaynak);
		li.appendChild(el('span', 'vk-durum', durumlar[r[6]] || r[6]));
		return li;
	}

	function simdi(saat) {
		var li = el('li', 'vk-simdi');
		li.appendChild(el('span', '', saat));
		return li;
	}

	function kur(pano) {
		var veriEl = pano.querySelector('.vk-pano__veri');
		if (!veriEl) { return; }
		var veri = JSON.parse(veriEl.textContent);
		var liste = pano.querySelector('.vk-pano__liste');
		var isletme = pano.querySelector('.vk-pano__isletme');
		var sayi = pano.querySelector('.vk-pano__sayi');
		var metin = pano.querySelector('.vk-pano__baglanti-metin');
		var internet = pano.querySelector('[data-vk-internet]');
		var dugmeler = pano.querySelectorAll('[data-meslek]');
		var secili = dugmeler.length ? dugmeler[0].dataset.meslek : null;

		function ciz() {
			var m = veri.meslekler[secili];
			var satirlar = m.satirlar.slice();
			// Offline: a walk-in is still booked — the program writes to this computer's database.
			if (internet && !internet.checked) {
				satirlar.push(['12:15', 'Kapıdan gelen ' + m.kisi, 'İnternetsiz kaydedildi', '', m.satirlar[0][4], m.satirlar[0][5], 'onay', true]);
				satirlar.sort(function (a, b) { return a[0] < b[0] ? -1 : a[0] > b[0] ? 1 : 0; });
			}
			liste.textContent = '';
			var cizildi = false;
			satirlar.forEach(function (r) {
				if (!cizildi && r[0] > veri.simdi) { liste.appendChild(simdi(veri.simdi)); cizildi = true; }
				var li = satir(r, veri.durumlar);
				if (r[7]) { li.dataset.yeni = ''; }
				liste.appendChild(li);
			});
			if (!cizildi) { liste.appendChild(simdi(veri.simdi)); }
			isletme.textContent = m.isletme;
			sayi.textContent = satirlar.length + ' randevu';
		}

		Array.prototype.forEach.call(dugmeler, function (b) {
			b.addEventListener('click', function () {
				secili = b.dataset.meslek;
				Array.prototype.forEach.call(dugmeler, function (x) { x.setAttribute('aria-pressed', x === b ? 'true' : 'false'); });
				ciz();
			});
		});

		if (internet) {
			internet.addEventListener('change', function () {
				pano.classList.toggle('is-cevrimdisi', !internet.checked);
				metin.textContent = internet.checked
					? 'Yerel veritabanı — internetsiz çalışır'
					: 'İnternet yok. Randevu, kasa ve yedek bu bilgisayarda çalışmaya devam ediyor.';
				ciz();
			});
		}
	}

	document.querySelectorAll('[data-vk-pano]').forEach(kur);
})();
