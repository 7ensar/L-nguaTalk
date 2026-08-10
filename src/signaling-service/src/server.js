import "dotenv/config";
import http from "node:http";
import express from "express";
import cors from "cors";
import { Server } from "socket.io";
import { createAdapter } from "@socket.io/redis-adapter";
import { createClient } from "redis";
import { v4 as uuidv4 } from "uuid";
import { MatchQueue } from "./matchQueue.js";
import { RoomRegistry } from "./rooms.js";
import { RateLimiter } from "./rateLimit.js";

const PORT = Number(process.env.PORT || 5050);
const MODERATION_KEY = process.env.MODERATION_KEY || "dev-moderation-key";
const MAX_CONNECTIONS = Number(process.env.MAX_CONNECTIONS || 1500);
const VERBOSE_LOGS = process.env.VERBOSE_LOGS === "1";
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

const queue = new MatchQueue({
  maxPerLanguage: Number(process.env.MAX_QUEUE_PER_LANG || 500),
  maxWaitMs: Number(process.env.QUEUE_MAX_WAIT_MS || 10 * 60_000)
});
const rooms = new RoomRegistry({
  maxRoomAgeMs: Number(process.env.MAX_ROOM_AGE_MS || 3 * 60 * 60_000)
});

const ipHttpLimiter = new RateLimiter({ windowMs: 60_000, max: 120 });
const joinLimiter = new RateLimiter({ windowMs: 60_000, max: 20 });
const signalLimiter = new RateLimiter({ windowMs: 10_000, max: 80 });
const chatLimiter = new RateLimiter({ windowMs: 10_000, max: 25 });

function log(...args) {
  if (VERBOSE_LOGS) {
    console.log(...args);
  }
}

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
    uptimeSec: Math.round(process.uptime()),
    connections: io.engine?.clientsCount ?? 0,
    queued: queue.totalSize(),
    rooms: rooms.rooms.size
  });
});

const server = http.createServer(app);
const io = new Server(server, {
  cors: {
    origin: allowAllOrigins ? true : corsOrigin,
    methods: ["GET", "POST"],
    credentials: true
  },
  // Production'da websocket öncelikli; polling fallback maliyetli
  transports: process.env.NODE_ENV === "production"
    ? ["websocket"]
    : ["websocket", "polling"],
  allowUpgrades: true,
  maxHttpBufferSize: 1e5,
  connectTimeout: 10_000,
  pingInterval: 25_000,
  pingTimeout: 20_000,
  perMessageDeflate: false
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

function parseIntOrNull(value) {
  if (value === null || value === undefined || value === "") return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

function joinQueue(socket, payload = {}) {
  const name = (payload.displayName || "Guest").toString().slice(0, 80);
  const lang = (payload.languageCode || "en").toString().toLowerCase();
  const userId = payload.userId ? String(payload.userId).slice(0, 128) : null;
  const guestSessionId = payload.guestSessionId ? String(payload.guestSessionId).slice(0, 64) : null;
  const interests = Array.isArray(payload.interests)
    ? payload.interests.map((x) => String(x).toLowerCase().slice(0, 40)).slice(0, 12)
    : [];
  const blockedUserIds = Array.isArray(payload.blockedUserIds)
    ? payload.blockedUserIds.map((x) => String(x).slice(0, 128)).slice(0, 200)
    : [];
  const rematchWithUserId = payload.rematchWithUserId
    ? String(payload.rematchWithUserId).slice(0, 128)
    : null;

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
    guestSessionId,
    languageLevel: parseIntOrNull(payload.languageLevel),
    gender: parseIntOrNull(payload.gender),
    preferredPartnerGender: parseIntOrNull(payload.preferredPartnerGender),
    interests,
    preferSimilarLevel: payload.preferSimilarLevel !== false,
    preferSharedInterests: payload.preferSharedInterests !== false,
    isPremium: !!payload.isPremium,
    blockedUserIds,
    rematchWithUserId
  });

  if (match?.overflow) {
    socket.emit("error:message", { message: "Queue is full for this language. Try again shortly." });
    return;
  }

  if (!match) {
    const position = queue.size(lang);
    socket.emit("queue:waiting", {
      languageCode: lang,
      positionHint: position,
      estimatedWaitSec: Math.round(queue.estimatedWaitMs(lang) / 1000),
      tip: position <= 1
        ? "You're first in line — hang tight while we find a partner."
        : `${position} people waiting in this language queue.`
    });
    return;
  }

  const roomId = uuidv4();
  rooms.create(roomId, match.languageCode, [match.a.socketId, match.b.socketId]);

  const socketA = io.sockets.sockets.get(match.a.socketId);
  const socketB = io.sockets.sockets.get(match.b.socketId);

  // Eşleşme anında peer kopmuşsa odayı kurma
  if (!socketA || !socketB) {
    if (socketA) {
      rooms.leave(match.a.socketId);
      queue.enqueue({ ...match.a, languageCode: match.languageCode });
      socketA.emit("queue:waiting", {
        languageCode: match.languageCode,
        positionHint: queue.size(match.languageCode),
        estimatedWaitSec: Math.round(queue.estimatedWaitMs(match.languageCode) / 1000)
      });
    }
    if (socketB) {
      rooms.leave(match.b.socketId);
      queue.enqueue({ ...match.b, languageCode: match.languageCode });
      socketB.emit("queue:waiting", {
        languageCode: match.languageCode,
        positionHint: queue.size(match.languageCode),
        estimatedWaitSec: Math.round(queue.estimatedWaitMs(match.languageCode) / 1000)
      });
    }
    return;
  }

  socketA.join(roomId);
  socketB.join(roomId);

  socketA.emit("match:found", {
    roomId,
    peerName: match.b.displayName,
    peerSocketId: match.b.socketId,
    peerUserId: match.b.userId || null,
    peerGuestSessionId: match.b.guestSessionId || null,
    languageCode: match.languageCode,
    isOfferer: true
  });

  socketB.emit("match:found", {
    roomId,
    peerName: match.a.displayName,
    peerSocketId: match.a.socketId,
    peerUserId: match.a.userId || null,
    peerGuestSessionId: match.a.guestSessionId || null,
    languageCode: match.languageCode,
    isOfferer: false
  });

  log(`[match] room=${roomId} lang=${match.languageCode}`);

  void notifyAspNetMatch({
    roomId,
    languageCode: match.languageCode,
    userAId: match.a.userId || null,
    userBId: match.b.userId || null,
    guestSessionAId: match.a.guestSessionId || null,
    guestSessionBId: match.b.guestSessionId || null
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
  if (!knownRoom || knownRoom !== roomId) {
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

io.use((socket, next) => {
  const count = io.engine?.clientsCount ?? 0;
  if (count >= MAX_CONNECTIONS) {
    return next(new Error("Server at capacity"));
  }
  return next();
});

io.on("connection", (socket) => {
  log(`[socket] connected ${socket.id}`);

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
    log(`[socket] disconnected ${socket.id}`);
  });
});

async function notifyAspNetMatch(payload) {
  const base = process.env.ASPNET_API_BASE_URL;
  if (!base) {
    return;
  }

  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), 2500);

  try {
    await fetch(`${base}/api/matches`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        "X-Moderation-Key": MODERATION_KEY
      },
      body: JSON.stringify({
        roomId: payload.roomId,
        languageCode: payload.languageCode,
        userAId: payload.userAId || null,
        userBId: payload.userBId || null,
        guestSessionAId: payload.guestSessionAId || null,
        guestSessionBId: payload.guestSessionBId || null
      }),
      signal: controller.signal
    });
  } catch (err) {
    console.warn("[aspnet] match notify failed:", err.message);
  } finally {
    clearTimeout(timer);
  }
}

setInterval(() => {
  ipHttpLimiter.prune();
  joinLimiter.prune();
  signalLimiter.prune();
  chatLimiter.prune();

  const expired = queue.pruneStale();
  for (const socketId of expired) {
    io.to(socketId).emit("queue:left");
    io.to(socketId).emit("error:message", {
      message: "Queue wait timed out. Please join again."
    });
  }

  const closedRooms = rooms.pruneStale();
  for (const closed of closedRooms) {
    for (const memberId of closed.members) {
      const sock = io.sockets.sockets.get(memberId);
      sock?.leave(closed.roomId);
      sock?.emit("match:peer-left", { roomId: closed.roomId, reason: "stale_room" });
    }
  }
}, 60_000).unref?.();

async function setupRedisAdapter() {
  const redisUrl = process.env.REDIS_URL;
  if (!redisUrl) {
    console.log("REDIS_URL not set — single-instance in-memory adapter");
    return;
  }

  try {
    const pubClient = createClient({ url: redisUrl });
    const subClient = pubClient.duplicate();
    pubClient.on("error", (err) => console.error("[redis pub]", err.message));
    subClient.on("error", (err) => console.error("[redis sub]", err.message));
    await Promise.all([pubClient.connect(), subClient.connect()]);
    io.adapter(createAdapter(pubClient, subClient));
    console.log("Socket.IO Redis adapter enabled");
  } catch (err) {
    console.error("Redis adapter failed, continuing in-memory:", err.message);
  }
}

await setupRedisAdapter();

server.listen(PORT, "0.0.0.0", () => {
  console.log(`LinguaTalk signaling listening on 0.0.0.0:${PORT}`);
  console.log(`CORS_ORIGIN=${corsOrigin.join(",")}`);
  console.log(`MAX_CONNECTIONS=${MAX_CONNECTIONS}`);
});
