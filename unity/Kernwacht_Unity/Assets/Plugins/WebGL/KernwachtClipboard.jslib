// Kernwacht 4.1: Zwischenablage im Browser (Text und PNG-Bild). Fällt auf Download zurück, wenn der Browser es verweigert.
mergeInto(LibraryManager.library, {
  KW_CopyText: function (p) {
    var t = UTF8ToString(p);
    var fallback = function () {
      try {
        var ta = document.createElement('textarea');
        ta.value = t; ta.style.position = 'fixed'; ta.style.left = '-9999px';
        document.body.appendChild(ta); ta.select();
        document.execCommand('copy'); ta.remove();
      } catch (e) {}
    };
    try {
      if (navigator.clipboard && navigator.clipboard.writeText) { navigator.clipboard.writeText(t).then(function () {}, fallback); return 1; }
    } catch (e) {}
    fallback();
    return 1;
  },
  KW_CopyPng: function (ptr, len, namePtr) {
    var bytes = HEAPU8.slice(ptr, ptr + len);
    var name = UTF8ToString(namePtr);
    var blob = new Blob([bytes], { type: 'image/png' });
    var download = function () {
      var a = document.createElement('a');
      a.href = URL.createObjectURL(blob); a.download = name;
      document.body.appendChild(a); a.click();
      setTimeout(function () { URL.revokeObjectURL(a.href); a.remove(); }, 3000);
    };
    try {
      if (navigator.clipboard && navigator.clipboard.write && window.ClipboardItem) {
        navigator.clipboard.write([new ClipboardItem({ 'image/png': blob })]).then(function () {}, download);
        return 1;
      }
    } catch (e) {}
    download();
    return 2;
  }
});
