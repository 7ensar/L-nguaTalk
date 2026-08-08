import "dotenv/config";
import http from "node:http";
import express from "express";
import cors from "cors";
import { Server } from "socket.io";
import { v4 as uuidv4 } from "uuid";
import { MatchQueue } from "./matchQueue.js";
import { RoomRegistry } from "./rooms.js";
import { RateLimiter } from "./rateLimit.js";

const PORT = Number(process.env.PORT || 5050);
const MODERATION_KEY = process.env.MODERATION_KEY || "dev-moderation-key";
const corsOrigin = (process.env.CORS_ORIGIN || "https://l-nguatalk.onrender.com")
  .split(",")
  .map((x) => x.trim())
  .filter(Boolean);
const allowAllOrigins = corsOrigin.includes("*");

const app = express();
app.set("trust proxy", 1);
app.use(cors({
  origin: allowAllOrigins ? true : corsOrigin,
  credentials: true
}));
app.use(express.json({ limit: "32kb" }));

const queue = new MatchQueue();
const rooms = new RoomRegistry();

const ipHttpLimiter = new RateLimiter({ windowMs: 60_000, max: 120 });
const joinLimiter = new RateLimiter({ windowMs: 60_000, max: 20 });
const signalLimiter = new RateLimiter({ windowMs: 10_000, max: 80 });
const chatLimiter = new RateLimiter({ windowMs: 10_000, max: 25 });

app.use((req, res, next) => {
  const ip = req.headers["x-forwarded-for"]?.toString().split(",")[0]?.trim() || req.socket.remoteAddress || "unknown";
  const result = ipHttpLimiter.check(`http:${ip}`);
  if (!result.allowed) {
    res.setHeader("Retry-After", Math.ceil(result.retryAfterMs / 1000));
    return res.status(429).json({ error: "Too many requests" });
  }
  return next();
});

app.get("/health", (_req, res) => {
  res.json({
    ok: true,
    service: "linguatalk-signaling",
    uptimeSec: Math.round(process.uptime())
  });
});

const server = http.createServer(app);
const io = new Server(server, {
  cors: {
    origin: allowAllOrigins ? true : corsOrigin,
    methods: ["GET", "POST"],
    credentials: true
  },
  transports: ["websocket", "polling"],
  maxHttpBufferSize: 1e5,
  connectTimeout: 10000
});

function mergeLanguageCounts(...maps) {
  /** @type {Record<string, number>} */
  const result = {};
  for (const map of maps) {
    for (const [lang, count] of Object.entries(map || {})) {
      result[lang] = (result[lang] || 0) + Number(count || 0);
    }
  }
  return result;
}

app.get("/stats", (_req, res) => {
  const queuedByLanguage = queue.snapshotByLanguage();
  const inCallByLanguage = rooms.snapshotByLanguage();
  res.json({
    connections: io.engine?.clientsCount ?? 0,
    rooms: rooms.rooms.size,
    queuedTotal: queue.totalSize(),
    queuedByLanguage,
    inCallByLanguage,
    activeByLanguage: mergeLanguageCounts(queuedByLanguage, inCallByLanguage),
    queuedEn: queue.size("en"),
    queuedTr: queue.size("tr")
  });
});

app.post("/moderation/force-disconnect", (req, res) => {
  const key = req.header("X-Moderation-Key");
  if (!key || key !== MODERATION_KEY) {
    return res.status(401).json({ error: "Unauthorized" });
  }

  const { roomId, reportedSocketId, reason } = req.body || {};
  let affected = 0;

  if (reportedSocketId) {
    const target = io.sockets.sockets.get(reportedSocketId);
    if (target) {
      queue.remove(reportedSocketId);
      const left = rooms.leave(reportedSocketId);
      target.emit("moderation:banned", {
        reason: reason || "reported",
        roomId: left?.roomId || roomId || null
      });
      if (left) {
        for (const peerId of left.remaining) {
          io.to(peerId).emit("match:peer-left", { roomId: left.roomId, reason: "reported" });
          io.to(peerId).emit("moderation:call-ended", {
            roomId: left.roomId,
            reason: "peer_reported"
          });
        }
      }
      target.disconnect(true);
      affected += 1;
    }
  }

  if (roomId) {
    const room = rooms.rooms.get(roomId);
    if (room) {
      for (const memberId of [...room.members]) {
        const sock = io.sockets.sockets.get(memberId);
        queue.remove(memberId);
        rooms.leave(memberId);
        sock?.emit("moderation:call-ended", { roomId, reason: reason || "report" });
        sock?.leave(roomId);
        affected += 1;
      }
      rooms.rooms.delete(roomId);
    }
  }

  return res.json({ ok: true, affected });
});

function joinQueue(socket, payload = {}) {
  const name = (payload.displayName || "Guest").toString().slice(0, 80);
  const lang = (payload.languageCode || "en").toString().toLowerCase();
  const userId = payload.userId ? String(payload.userId).slice(0, 128) : null;
  const guestSessionId = payload.guestSessionId ? String(payload.guestSessionId).slice(0, 64) : null;

  const existingRoom = rooms.getRoomIdForSocket(socket.id);
  if (existingRoom) {
    handleLeave(socket, existingRoom);
  }

  queue.remove(socket.id);
  socket.data.displayName = name;
  socket.data.languageCode = lang;
  socket.data.userId = userId;
  socket.data.guestSessionId = guestSessionId;

  const match = queue.enqueue({
    socketId: socket.id,
    displayName: name,
    languageCode: lang,
    userId,
    guestSessionId
  });

  if (!match) {
    socket.emit("queue:waiting", {
      languageCode: lang,
      positionHint: queue.size(lang)
    });
    return;
  }

  const roomId = uuidv4();
  rooms.create(roomId, match.languageCode, [match.a.socketId, match.b.socketId]);

  const socketA = io.sockets.sockets.get(match.a.socketId);
  const socketB = io.sockets.sockets.get(match.b.socketId);

  socketA?.join(roomId);
  socketB?.join(roomId);

  socketA?.emit("match:found", {
    roomId,
    peerName: match.b.displayName,
    peerSocketId: match.b.socketId,
    peerUserId: match.b.userId || null,
    peerGuestSessionId: match.b.guestSessionId || null,
    languageCode: match.languageCode,
    isOfferer: true
  });

  socketB?.emit("match:found", {
    roomId,
    peerName: match.a.displayName,
    peerSocketId: match.a.socketId,
    peerUserId: match.a.userId || null,
    peerGuestSessionId: match.a.guestSessionId || null,
    languageCode: match.languageCode,
    isOfferer: false
  });

  console.log(`[match] room=${roomId} lang=${match.languageCode}`);

  void notifyAspNetMatch({
    roomId,
    languageCode: match.languageCode
  });
}

function handleLeave(socket, roomId) {
  if (roomId) {
    socket.leave(roomId);
  }

  const left = rooms.leave(socket.id);
  if (!left) {
    return;
  }

  for (const peerId of left.remaining) {
    io.to(peerId).emit("match:peer-left", { roomId: left.roomId });
  }
}

function relaySignal(socket, eventName, payload = {}) {
  const roomId = payload.roomId;
  if (!roomId) {
    socket.emit("error:message", { message: "Missing roomId." });
    return;
  }

  const knownRoom = rooms.getRoomIdForSocket(socket.id);
  if (knownRoom && knownRoom !== roomId) {
    socket.emit("error:message", { message: "You are not in this room." });
    return;
  }

  const limit = signalLimiter.check(`sig:${socket.id}:${eventName}`);
  if (!limit.allowed) {
    socket.emit("error:message", { message: "Rate limit exceeded." });
    return;
  }

  socket.to(roomId).emit(eventName, {
    ...payload,
    from: socket.id
  });
}

io.on("connection", (socket) => {
  console.log(`[socket] connected ${socket.id}`);

  socket.on("queue:join", (payload) => {
    try {
      const limit = joinLimiter.check(`join:${socket.id}`);
      if (!limit.allowed) {
        socket.emit("error:message", { message: "Too many queue joins. Slow down." });
        return;
      }
      joinQueue(socket, payload || {});
    } catch (err) {
      console.error("[queue:join]", err);
      socket.emit("error:message", { message: "Could not join queue." });
    }
  });

  socket.on("queue:next", (payload) => {
    try {
      const limit = joinLimiter.check(`next:${socket.id}`);
      if (!limit.allowed) {
        socket.emit("error:message", { message: "Too many next requests." });
        return;
      }
      queue.remove(socket.id);
      const current = rooms.getRoomIdForSocket(socket.id);
      if (current) {
        handleLeave(socket, current);
      }
      joinQueue(socket, payload || {
        displayName: socket.data.displayName,
        languageCode: socket.data.languageCode,
        userId: socket.data.userId,
        guestSessionId: socket.data.guestSessionId
      });
    } catch (err) {
      console.error("[queue:next]", err);
      socket.emit("error:message", { message: "Could not find next partner." });
    }
  });

  socket.on("queue:leave", () => {
    queue.remove(socket.id);
    socket.emit("queue:left");
  });

  socket.on("webrtc:offer", (payload) => relaySignal(socket, "webrtc:offer", payload));
  socket.on("webrtc:answer", (payload) => relaySignal(socket, "webrtc:answer", payload));
  socket.on("webrtc:ice-candidate", (payload) => relaySignal(socket, "webrtc:ice-candidate", payload));

  socket.on("chat:message", (payload = {}) => {
    try {
      const roomId = rooms.getRoomIdForSocket(socket.id);
      if (!roomId) {
        socket.emit("error:message", { message: "Not in a call." });
        return;
      }

      const limit = chatLimiter.check(`chat:${socket.id}`);
      if (!limit.allowed) {
        socket.emit("error:message", { message: "Chat rate limit exceeded." });
        return;
      }

      const text = String(payload.text || "")
        .replace(/[\u0000-\u001F\u007F]/g, " ")
        .trim()
        .slice(0, 500);

      if (!text) {
        return;
      }

      const message = {
        id: uuidv4(),
        roomId,
        text,
        fromSocketId: socket.id,
        fromName: socket.data.displayName || "Peer",
        sentAt: Date.now()
      };

      io.to(roomId).emit("chat:message", message);
    } catch (err) {
      console.error("[chat:message]", err);
    }
  });

  socket.on("room:leave", ({ roomId } = {}) => {
    queue.remove(socket.id);
    handleLeave(socket, roomId || rooms.getRoomIdForSocket(socket.id));
  });

  socket.on("disconnect", () => {
    queue.remove(socket.id);
    const left = rooms.leave(socket.id);
    if (left) {
      for (const peerId of left.remaining) {
        io.to(peerId).emit("match:peer-left", { roomId: left.roomId });
      }
    }
    console.log(`[socket] disconnected ${socket.id}`);
  });
});

async function notifyAspNetMatch(payload) {
  const base = process.env.ASPNET_API_BASE_URL;
  if (!base) {
    return;
  }

  try {
    await fetch(`${base}/api/matches`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        roomId: payload.roomId,
        languageCode: payload.languageCode,
        userAId: null,
        userBId: null,
        guestSessionAId: null,
        guestSessionBId: null
      })
    });
  } catch (err) {
    console.warn("[aspnet] match notify failed:", err.message);
  }
}

server.listen(PORT, "0.0.0.0", () => {
  console.log(`LinguaTalk signaling listening on 0.0.0.0:${PORT}`);
  console.log(`CORS_ORIGIN=${corsOrigin.join(",")}`);
});
