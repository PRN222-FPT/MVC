// Shared SignalR client for live document-processing status/progress updates.
// Used by Document/Library.cshtml (per-row) and Document/View.cshtml (single banner).
// One connection per page; callers subscribe per documentId and get called back with
// { documentId, status, progressPercent, stageLabel, chunkCount, errorMessage }.
window.DocumentProcessingHub = (() => {
    if (typeof signalR === 'undefined') {
        console.error('DocumentProcessingHub: the signalr client script did not load (~/lib/signalr/dist/browser/signalr.min.js); live updates are disabled.');
        return { subscribe: async () => {} };
    }

    let connection = null;
    let startPromise = null;
    const subscriptions = new Map();
    const joinedGroups = new Set();

    function ensureConnection() {
        if (connection) {
            return startPromise;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/document-processing')
            .withAutomaticReconnect()
            .build();

        connection.on('DocumentStatusChanged', payload => {
            const callbacks = subscriptions.get(payload.documentId);
            if (!callbacks) {
                return;
            }
            callbacks.forEach(callback => callback(payload));
        });

        connection.onreconnected(() => {
            joinedGroups.forEach(documentId => {
                connection.invoke('JoinDocumentGroup', documentId).catch(() => {});
            });
        });

        startPromise = connection.start().catch(err => {
            console.error('DocumentProcessingHub: connection failed', err);
        });

        return startPromise;
    }

    async function subscribe(documentId, onUpdate) {
        if (!subscriptions.has(documentId)) {
            subscriptions.set(documentId, new Set());
        }
        subscriptions.get(documentId).add(onUpdate);

        await ensureConnection();
        if (!connection || connection.state !== signalR.HubConnectionState.Connected) {
            return;
        }

        if (!joinedGroups.has(documentId)) {
            joinedGroups.add(documentId);
            try {
                await connection.invoke('JoinDocumentGroup', documentId);
            } catch (err) {
                console.error('DocumentProcessingHub: failed to join group', documentId, err);
            }
        }
    }

    return { subscribe };
})();
