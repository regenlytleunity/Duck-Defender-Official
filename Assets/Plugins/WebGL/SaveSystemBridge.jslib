mergeInto(LibraryManager.library, {
    SyncFiles: function() {
        // Flushes the in-memory Unity filesystem to IndexedDB.
        // Without this, writes made via File.WriteAllText only live in memory 
        // until the page closes, and get lost.
        FS.syncfs(false, function(err) {
            if (err) {
                console.error('[SaveSystem] WebGL sync failed:', err);
            } else {
                console.log('[SaveSystem] WebGL save synced to IndexedDB.');
            }
        });
    }
});