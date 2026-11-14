/* Editor side of the theme's two server-rendered blocks: a live preview, nothing saved but the block comment. */
(function (blocks, element, ServerSideRender, blockEditor) {
	'use strict';
	var el = element.createElement;
	function onizleme(name) {
		return function () {
			return el('div', blockEditor.useBlockProps(), el(ServerSideRender, { block: name }));
		};
	}
	blocks.registerBlockType('vernuko/pano', { apiVersion: 3, title: 'Vernuko gün ekranı', icon: 'calendar-alt', category: 'widgets', edit: onizleme('vernuko/pano'), save: function () { return null; } });
	blocks.registerBlockType('vernuko/fis', { apiVersion: 3, title: 'Vernuko fiyat fişi', icon: 'money-alt', category: 'widgets', edit: onizleme('vernuko/fis'), save: function () { return null; } });
})(window.wp.blocks, window.wp.element, window.wp.serverSideRender, window.wp.blockEditor);
