// Puente WebGL -> página web Sentir Nacer (ver Assets/Scripts/Obstetricia/Desktop/WebBridge.cs).
// Unity lo incluye automáticamente en la build WebGL (los .jslib dentro de Plugins/WebGL).
mergeInto(LibraryManager.library, {
  // Envía un objeto JSON a la página que contiene el iframe (mismo origen).
  ObstetriciaPostMessage: function (jsonPtr) {
    var json = UTF8ToString(jsonPtr);
    try {
      var data = JSON.parse(json);
      var target = window.parent && window.parent !== window ? window.parent : window;
      target.postMessage(data, window.location.origin);
    } catch (e) {
      console.error('[Obstetricia] postMessage', e);
    }
  },

  // Narración con la voz del navegador (español).
  ObstetriciaSpeak: function (textPtr) {
    var text = UTF8ToString(textPtr).replace(/<[^>]*>/g, '');
    if (!('speechSynthesis' in window) || !text) return;
    try {
      window.speechSynthesis.cancel();
      var u = new SpeechSynthesisUtterance(text);
      u.lang = 'es-CL';
      u.rate = 0.95;
      var voices = window.speechSynthesis.getVoices() || [];
      for (var i = 0; i < voices.length; i++) {
        if (voices[i].lang && voices[i].lang.toLowerCase().indexOf('es') === 0) { u.voice = voices[i]; break; }
      }
      window.speechSynthesis.speak(u);
    } catch (e) {
      console.warn('[Obstetricia] speechSynthesis', e);
    }
  },

  ObstetriciaStopSpeaking: function () {
    if ('speechSynthesis' in window) window.speechSynthesis.cancel();
  }
});
