mergeInto(LibraryManager.library, {
    JSEnterFullscreen: function() {
        var elem = document.documentElement;
        if (elem.requestFullscreen) elem.requestFullscreen();
        else if (elem.webkitRequestFullscreen) elem.webkitRequestFullscreen();
    },

    JSExitFullscreen: function() {
        if (document.exitFullscreen) document.exitFullscreen();
        else if (document.webkitExitFullscreen) document.webkitExitFullscreen();
    },

    JSIsFullscreen: function() {
        var fullscreenElement = document.fullscreenElement || document.webkitFullscreenElement;
        return fullscreenElement !== null;
    }
});