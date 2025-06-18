const http = require("http");
const WebSocket = require("ws");
const readline = require("readline");

const server = http.createServer();
const wss = new WebSocket.Server({ noServer: true });

const PORT = 8087;
const PATH = "/timy3";

let startTime = null;
let activeStartTimeString = null; // Stores the active start time string sent to clients
let isRunning = false; // Tracks if timing is currently active
let runningInterval = null; // Interval for sending running status updates

wss.on("connection", (ws) => {
  console.log("Client connected");

  // If timing is active, send start signal to new client
  if (activeStartTimeString) {
    const startEvent = {
      event: "start",
      time: activeStartTimeString,
    };
    ws.send(JSON.stringify(startEvent));
    console.log(
      `Sent active start signal to new client: ${activeStartTimeString}`,
    );
  }

  // Send current running status to new client
  const runningEvent = {
    event: "running",
    value: isRunning,
  };
  ws.send(JSON.stringify(runningEvent));

  ws.on("close", () => {
    console.log("Client disconnected");
  });
});

function broadcast(data) {
  const message = JSON.stringify(data);
  console.log(`Broadcasting: ${message}`);
  wss.clients.forEach((client) => {
    if (client.readyState === WebSocket.OPEN) {
      client.send(message);
    }
  });
}

server.on("upgrade", (request, socket, head) => {
  if (request.url === PATH) {
    wss.handleUpgrade(request, socket, head, (ws) => {
      wss.emit("connection", ws, request);
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

process.stdin.on("keypress", (str, key) => {
  if ((key.ctrl && key.name === "c") || key.name === "q") {
    console.log("Exiting...");
    process.exit();
  }

  if (key.name === "s") {
    startTime = new Date();
    const timeString =
      startTime.toLocaleTimeString("en-GB", {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit",
      }) +
      "." +
      String(startTime.getMilliseconds()).padStart(3, "0");

    activeStartTimeString = timeString; // Store the active start time
    isRunning = true; // Set running state to true
    const startEvent = {
      event: "start",
      time: timeString,
    };
    broadcast(startEvent);

    // Broadcast running status
    const runningEvent = {
      event: "running",
      value: true,
    };
    broadcast(runningEvent);

    // Start interval to send running status every second
    runningInterval = setInterval(() => {
      if (isRunning) {
        const runningEvent = {
          event: "running",
          value: true,
        };
        broadcast(runningEvent);
      }
    }, 1000);
  }

  if (key.name === "f") {
    if (!startTime) {
      console.log("Cannot send FINISH signal without a START signal first.");
      return;
    }
    const finishTime = new Date();
    const elapsedTimeMs = finishTime - startTime;
    const elapsedTimeSec = (elapsedTimeMs / 1000).toFixed(2);

    const finishEvent = {
      event: "finish",
      time: `${String(elapsedTimeSec).padStart(7, "0")}`, // Padded to look similar to device output
    };
    broadcast(finishEvent);
    startTime = null; // Reset start time
    activeStartTimeString = null; // Clear active start time
    isRunning = false; // Set running state to false

    // Clear the running interval
    if (runningInterval) {
      clearInterval(runningInterval);
      runningInterval = null;
    }

    // Broadcast running status
    const runningEvent = {
      event: "running",
      value: false,
    };
    broadcast(runningEvent);
  }
});
