const http = require('http');
const WebSocket = require('ws');
const readline = require('readline');

const server = http.createServer();
const wss = new WebSocket.Server({ noServer: true });

const PORT = 8087;
const PATH = '/timy3';

let startTime = null;

wss.on('connection', ws => {
    console.log('Client connected');
    ws.on('close', () => {
        console.log('Client disconnected');
    });
});

function broadcast(data) {
    const message = JSON.stringify(data);
    console.log(`Broadcasting: ${message}`);
    wss.clients.forEach(client => {
        if (client.readyState === WebSocket.OPEN) {
            client.send(message);
        }
    });
}

server.on('upgrade', (request, socket, head) => {
    if (request.url === PATH) {
        wss.handleUpgrade(request, socket, head, ws => {
            wss.emit('connection', ws, request);
        });
    } else {
        console.log(`Rejecting connection to ${request.url}`);
        socket.destroy();
    }
});

server.listen(PORT, () => {
    console.log(`WebSocket server started on ws://localhost:${PORT}${PATH}`);
    console.log('Press "s" to send a START signal.');
    console.log('Press "f" to send a FINISH signal.');
    console.log('Press "q" or CTRL+C to quit.');
});

// --- Keypress handling ---
readline.emitKeypressEvents(process.stdin);
if (process.stdin.isTTY) {
    process.stdin.setRawMode(true);
}

process.stdin.on('keypress', (str, key) => {
    if (key.ctrl && key.name === 'c' || key.name === 'q') {
        console.log('Exiting...');
        process.exit();
    }

    if (key.name === 's') {
        startTime = new Date();
        const timeString = startTime.toLocaleTimeString('en-GB', {
            hour: '2-digit',
            minute: '2-digit',
            second: '2-digit'
        }) + '.' + String(startTime.getMilliseconds()).padStart(3, '0');

        const startEvent = {
            event: 'start',
            time: timeString
        };
        broadcast(startEvent);
    }

    if (key.name === 'f') {
        if (!startTime) {
            console.log('Cannot send FINISH signal without a START signal first.');
            return;
        }
        const finishTime = new Date();
        const elapsedTimeMs = finishTime - startTime;
        const elapsedTimeSec = (elapsedTimeMs / 1000).toFixed(2);

        const finishEvent = {
            event: 'finish',
            time: `${String(elapsedTimeSec).padStart(7, '0')}` // Padded to look similar to device output
        };
        broadcast(finishEvent);
        startTime = null; // Reset start time
    }
}); 