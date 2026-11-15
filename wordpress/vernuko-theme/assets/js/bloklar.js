/* Editor side of the theme's server-rendered blocks: a live preview and, where it applies, a program picker.
   Nothing is saved but the block comment and its attributes. */
(function (blocks, element, ServerSideRender, blockEditor, components) {
	'use strict';
	var el = element.createElement;
	var programlar = [
		{ label: 'Vernuko Randevu', value: 'vernuko-desktop' },
		{ label: 'Vernuko Stok', value: 'vernuko-stok' },
		{ label: 'Vernuko Teknik Servis', value: 'vernuko-teknik-servis' }
	];
	var ekranlar = [
		{ label: 'Randevu (gün ekranı)', value: 'randevu' },
		{ label: 'Stok (planlanan ekran)', value: 'stok' },
		{ label: 'Teknik Servis (planlanan ekran)', value: 'servis' }
	];

	function blok(name, title, icon, alan, secenekler, etiket) {
		var attributes = {};
		if (alan) {
			attributes[alan] = { type: 'string', default: secenekler === null ? '' : secenekler[0].value };
		}
		blocks.registerBlockType(name, {
			apiVersion: 3,
			title: title,
			icon: icon,
			category: 'widgets',
			attributes: attributes,
			edit: function (props) {
				var ayar = null;
				if (alan) {
					var options = secenekler === null ? [{ label: 'Hepsi', value: '' }].concat(programlar) : secenekler;
					var degistir = {};
					ayar = el(blockEditor.InspectorControls, null,
						el(components.PanelBody, { title: 'Vernuko' },
							el(components.SelectControl, {
								label: etiket,
								value: props.attributes[alan],
								options: options,
								onChange: function (v) { degistir[alan] = v; props.setAttributes(degistir); }
							})));
				}
				return el(element.Fragment, null, ayar,
					el('div', blockEditor.useBlockProps(), el(ServerSideRender, { block: name, attributes: props.attributes })));
			},
			save: function () { return null; }
		});
	}

	blok('vernuko/pano', 'Vernuko gün ekranı', 'calendar-alt', 'ekran', ekranlar, 'Ekran');
	blok('vernuko/fis', 'Vernuko fiyat fişi', 'money-alt', 'urun', programlar, 'Program');
	blok('vernuko/urunler', 'Vernuko programları', 'screenoptions', 'haric', null, 'Listede gösterme');
	blok('vernuko/urun-satis', 'Vernuko program satışı', 'cart', 'urun', programlar, 'Program');
	blok('vernuko/urun-logo', 'Vernuko program logosu', 'awards', 'urun', programlar, 'Program');
})(window.wp.blocks, window.wp.element, window.wp.serverSideRender, window.wp.blockEditor, window.wp.components);
