/* Price receipt: picking a plan updates the itemised receipt and the buy link. */
(function () {
	'use strict';
	document.querySelectorAll('[data-vk-fis]').forEach(function (fis) {
		fis.querySelectorAll('.vk-fis__secenek input[type="radio"]').forEach(function (r) {
			r.addEventListener('change', function () {
				if (!r.checked) { return; }
				fis.querySelectorAll('[data-fis="ad"]').forEach(function (e) { e.textContent = r.dataset.ad; });
				fis.querySelectorAll('[data-fis="fiyat"]').forEach(function (e) { e.textContent = r.dataset.fiyat; });
				fis.querySelectorAll('[data-fis="sure"]').forEach(function (e) { e.textContent = r.dataset.sure; });
				fis.querySelectorAll('[data-fis="cihaz"]').forEach(function (e) { e.textContent = r.dataset.cihaz; });
				fis.querySelectorAll('[data-fis="href"]').forEach(function (a) { if (r.dataset.href) { a.href = r.dataset.href; } });
			});
		});
	});
})();
